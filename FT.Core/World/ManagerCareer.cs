using System;
using System.Text.Json.Serialization;
using System.Collections.Generic;
using System.Linq;
using FootballTactician.Balance;
using FootballTactician.Data;
using FootballTactician.Simulation;

namespace FootballTactician.World
{
    /// <summary>
    /// 감독 커리어. 평판이 쌓이고, 자리가 나면 제안이 온다.
    ///
    /// 경질이 곧 게임 종료이면 한 판이 한 구단에서 끝난다. 경질은 커리어의 한 장면이어야 한다.
    /// </summary>
    public static class ManagerCareer
    {
        /// <summary>
        /// 한 시즌 성적으로 평판을 갱신한다.
        /// 큰 구단에서의 평범한 성적보다 작은 구단에서의 좋은 성적을 높게 친다.
        /// </summary>
        public static void UpdateReputation(GameWorld w, int finalPosition, int leagueSize, bool wonCup)
        {
            var club = w.MyClub;
            if (club == null) return;

            // 기대 순위 — 평판이 높을수록 위를 기대받는다.
            float expected = ExpectedPosition(club.reputation, leagueSize);
            float over = expected - finalPosition;            // 양수면 기대 초과

            int delta = (int)Math.Round(over * 1.6f);
            delta += (int)w.TierOf(club.id) == 1 ? 2 : 0;     // 1부에서 버틴 것 자체가 경력
            if (wonCup) delta += 10;
            delta += PressRoom.ReputationBonus(w.Manager);    // 언론과의 관계

            // 하부 리그에서 아무리 잘해도 평판이 폭증하지는 않는다.
            delta = Math.Clamp(delta, -18, 18);

            w.ManagerReputation = Math.Clamp(w.ManagerReputation + delta, 0, 100);
        }

        public static float ExpectedPosition(int stars, int leagueSize) => stars switch
        {
            5 => 1.5f,
            4 => leagueSize * 0.22f,
            3 => leagueSize * 0.45f,
            2 => leagueSize * 0.68f,
            _ => leagueSize * 0.82f,
        };

        /// <summary>그 구단이 이 감독을 고려할지. 평판이 모자라면 제안이 오지 않는다.</summary>
        public static int RequiredReputation(GameWorld w, ClubData club)
        {
            int baseReq = club.reputation switch
            {
                5 => 78,
                4 => 60,
                3 => 42,
                2 => 26,
                _ => 10,
            };
            // 2부 구단은 문턱이 낮다.
            if (w.TierOf(club.id) != LeagueTier.First) baseReq = (int)(baseReq * 0.55f);
            return baseReq;
        }

        /// <summary>
        /// 지금 감독을 구하는 구단들. 평판이 닿는 곳만 보여 준다.
        /// </summary>
        public static List<JobOffer> OpenJobs(GameWorld w, Random rng, int take = 6)
        {
            var offers = new List<JobOffer>();

            foreach (var club in w.Clubs.Values)
            {
                if (club.id == w.ManagedClubId) continue;
                if (!w.VacantClubs.Contains(club.id)) continue;

                int need = RequiredReputation(w, club);
                if (w.ManagerReputation < need) continue;

                offers.Add(new JobOffer
                {
                    clubId = club.id,
                    requiredReputation = need,
                    leagueId = w.LeagueOf(club.id)?.id ?? 0,
                });
            }

            return offers
                .OrderByDescending(o => w.Club(o.clubId)!.reputation)
                .ThenByDescending(o => w.Strength(o.clubId))
                .Take(take)
                .ToList();
        }

        /// <summary>
        /// 자리를 비워 둘 구단을 고른다. 매 시즌 몇 자리는 열려 있어야 커리어가 굴러간다.
        /// 성적이 나쁜 구단일수록 감독을 바꾼다.
        /// </summary>
        public static void RollVacancies(GameWorld w, Random rng)
        {
            w.VacantClubs.Clear();

            foreach (var league in w.Leagues.Values)
            {
                var standings = league.currentSeason?.standings;
                if (standings == null) continue;

                int size = standings.Count;
                foreach (var e in standings)
                {
                    if (e.clubId == w.ManagedClubId) continue;

                    var club = w.Club(e.clubId);
                    if (club == null) continue;

                    // 기대보다 한참 못 미친 구단이 감독을 바꾼다.
                    float expected = ExpectedPosition(club.reputation, size);
                    float shortfall = e.position - expected;

                    int chance = shortfall switch
                    {
                        >= 8 => 55,
                        >= 5 => 35,
                        >= 3 => 20,
                        >= 1 => 8,
                        _ => 3,
                    };

                    if (rng.Next(100) < chance) w.VacantClubs.Add(club.id);
                }
            }
        }

        /// <summary>새 구단을 맡는다. 이전 구단과의 관계는 끊긴다.</summary>
        public static void TakeJob(GameWorld w, int clubId)
        {
            int previous = w.ManagedClubId;

            w.CareerHistory.Add(new CareerSpell
            {
                clubId = previous,
                clubName = w.Club(previous)?.name ?? "—",
                fromYear = w.CurrentSpellStartYear,
                toYear = w.SeasonYear,
                wins = w.Manager.totalWins,
                draws = w.Manager.totalDraws,
                losses = w.Manager.totalLosses,
                endedSacked = w.Sacked,
            });

            w.VacantClubs.Remove(clubId);
            w.Sacked = false;
            w.SackReason = "";
            w.CurrentSpellStartYear = w.SeasonYear;

            TakeCharge(w, clubId, w.ManagerName, keepCareer: true);
        }
        /// <param name="keepCareer">이직이면 true. 평판과 커리어 기록을 유지한다.</param>
        public static void TakeCharge(
            GameWorld w, int clubId, string managerName, bool keepCareer = false)
        {
            w.ManagedClubId = clubId;
            w.ManagerName = string.IsNullOrWhiteSpace(managerName) ? "이름 없는 감독" : managerName.Trim();
            w.Sacked = false;
            w.SackReason = "";

            if (!keepCareer)
            {
                // 첫 부임 평판은 고른 구단 수준에 맞춘다.
                // 5성 구단에서 시작했는데 평판이 20 이면, 그 자리를 다시는 얻을 수 없다는 뜻이 된다.
                var picked = w.Club(clubId);
                w.ManagerReputation = picked == null
                    ? BalanceConfig.ManagerStartingReputation
                    : Math.Max(BalanceConfig.ManagerStartingReputation,
                               ManagerCareer.RequiredReputation(w, picked));
                w.CareerHistory.Clear();
                w.CurrentSpellStartYear = w.SeasonYear;
            }

            var club = w.Club(clubId);
            w.Manager = new ManagerProfile
            {
                staffId = -1,
                tacticalStyle = "균형",
                preferredFormations = new List<string> { "4-2-3-1" },
                attackingMentality = 3,
                pressingIntensity = 3,
                pressReputation = PressRoom.Neutral,
                fanApproval = PressRoom.Neutral,
                boardConfidence = 60,
                pressStyle = ManagerPressStyle.Diplomatic,
            };

            // 이사회 초기 신뢰는 감독 평판을 따른다. 이름값이 있으면 여유를 더 준다.
            // 막 뽑은 감독을 이사회가 의심하고 있으면 안 되므로 "지지"(60) 아래로는 내려가지 않는다.
            w.Manager.boardConfidence = Math.Clamp(60 + w.ManagerReputation / 4, 60, 85);

            // 새 구단에서는 남의 선수 정보를 처음부터 다시 모은다.
            w.ScoutReports.Clear();
            Scouting.StartWeek(w);

            // 내 임기 성과를 따로 세기 위한 기준점.
            var h = w.MyClub?.history;
            w.TitlesAtTakeover = h == null ? 0 : h.leagueTitles + h.cupTitles + h.continentalTitles;
            if (club != null) club.managerId = -1;

            w.Objectives = BoardObjectives.GenerateFor(w, clubId);

            w.Inbox.Clear();
            w.Post("감독 부임",
                   $"{club?.name} 감독으로 선임되었습니다. 이사회 목표: " +
                   string.Join(" / ", w.Objectives.Select(o => o.description)),
                   InboxKind.Board);
        }
}

}
