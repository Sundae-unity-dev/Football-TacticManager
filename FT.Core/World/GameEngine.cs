using System;
using System.Collections.Generic;
using System.Linq;
using FootballTactician.Diagnostics;
using FootballTactician.Balance;
using FootballTactician.Data;
using FootballTactician.Simulation;

namespace FootballTactician.World
{
    /// <summary>
    /// 시간 진행과 경기 처리. Unity 시절 GameManager 와 TimeManager 를 합쳐 다시 쓴 것이다.
    /// 화면은 여기 있는 함수만 호출하고, 상태는 GameWorld 에서 읽는다.
    /// </summary>
    public static class GameEngine
    {
        /// <summary>플레이어 구단의 다음 경기. 없으면 null.</summary>
        public static FixtureData NextUserFixture(GameWorld w)
        {
            var league = w.MyLeague;
            if (league?.currentSeason == null) return null;
            return league.currentSeason.fixtures
                .Where(f => !f.isPlayed &&
                            (f.homeClubId == w.ManagedClubId || f.awayClubId == w.ManagedClubId))
                .OrderBy(f => f.matchday)
                .FirstOrDefault();
        }

        public static FixtureData LastUserFixture(GameWorld w)
        {
            var league = w.MyLeague;
            if (league?.currentSeason == null) return null;
            return league.currentSeason.fixtures
                .Where(f => f.isPlayed &&
                            (f.homeClubId == w.ManagedClubId || f.awayClubId == w.ManagedClubId))
                .OrderByDescending(f => f.matchday)
                .FirstOrDefault();
        }

        /// <summary>
        /// 세계가 아직 치르지 않은 가장 이른 경기. 없으면 null.
        ///
        /// <b>내 경기가 아니라 세계를 기준으로 삼는 이유</b> — 리그마다 라운드 수가
        /// 다르고(잉글랜드 1부 38 · 2부 46), 홀수 팀 리그에서는 내가 쉬는 주가 있다.
        /// 내 경기만 보고 돌리면 내가 쉬는 라운드와 내 시즌이 끝난 뒤의 라운드를
        /// <b>어느 리그도 치르지 않는다</b> — 그 리그의 시즌은 영원히 끝나지 않는다.
        /// </summary>
        public static FixtureData NextWorldFixture(GameWorld w)
        {
            FixtureData earliest = null;

            foreach (var league in w.Leagues.Values)
            {
                var season = league.currentSeason;
                if (season == null) continue;

                foreach (var f in season.fixtures)
                    if (!f.isPlayed && (earliest == null || f.matchday < earliest.matchday))
                        earliest = f;
            }

            return earliest;
        }

        /// <summary>아직 치를 라운드가 남았는지.</summary>
        public static bool RoundPending(GameWorld w) => NextWorldFixture(w) != null;

        // ── 한 라운드 진행 ────────────────────────────────────────────────

        /// <summary>
        /// 플레이어 경기를 치르고, 같은 날 나머지 리그도 함께 돌린 뒤 한 주를 넘긴다.
        /// 반환값은 플레이어 경기 결과.
        /// </summary>
        public static MatchResultData PlayNextRound(GameWorld w, int seedSalt = 0)
        {
            var round = NextWorldFixture(w);
            if (round == null) return null;

            int matchday = round.matchday;
            w.Date = round.date;

            // 이번 라운드에 내 경기가 있는지. 부전승으로 쉬는 주면 없다.
            var fixture = NextUserFixture(w);
            if (fixture != null && fixture.matchday != matchday) fixture = null;

            var myLeague = w.MyLeague;
            MatchResultData mine = null;

            using (SimProfile.Measure("리그 경기"))
            {
                foreach (var league in w.Leagues.Values)
                {
                    var season = league.currentSeason;
                    if (season == null) continue;

                    foreach (var f in season.fixtures.Where(f => f.matchday == matchday && !f.isPlayed))
                    {
                        var result = MatchDay.SimulateFixture(w, f, seedSalt);
                        if (fixture != null && league == myLeague && f.id == fixture.id) mine = result;
                    }
                    season.currentMatchday = Math.Max(season.currentMatchday, matchday);
                    using (SimProfile.Measure("  ├ 순위 재계산"))
                        Standings.Recalculate(league);
                }
            }

            using (SimProfile.Measure("컵"))
                RunCupRound(w, matchday, new Random(SeedMix.Of(w.Seed, matchday * 101, 7331)), skipUserTie: false);

            // A매치 주간. 용도별로 난수를 따로 쓴다 — 같은 rng 를 쓰면 컵 추첨이 밀린다.
            if (NationalTeam.IsBreak(matchday))
                using (SimProfile.Measure("A매치"))
                    NationalTeam.RunBreak(w, new Random(SeedMix.Of(w.Seed, matchday * 211, 5501)));

            if (ReserveLeague.IsMatchday(matchday))
                using (SimProfile.Measure("2군"))
                    ReserveLeague.RunMatchday(w, new Random(SeedMix.Of(w.Seed, matchday * 307, 1511)));

            // 내가 쉬는 주에는 경기 뒷처리가 없다. 그래도 한 주는 지나간다.
            if (fixture != null)
                using (SimProfile.Measure("경기 뒷처리"))
                    AfterUserMatch(w, fixture, mine);

            WeekCycle.Advance(w, round.date.AddDays(7));
            return mine;
        }

        /// <summary>
        /// 감독 없이 리그 한 라운드만 돌린다. 측정 하네스와, 플레이어가 없는 다른 나라 리그에 쓴다.
        /// 치른 경기 수를 돌려준다.
        /// </summary>
        public static int PlayRound(GameWorld w, LeagueData league, int seedSalt = 0)
        {
            var season = league?.currentSeason;
            if (season == null) return 0;

            var next = season.fixtures.Where(f => !f.isPlayed)
                                      .OrderBy(f => f.matchday)
                                      .FirstOrDefault();
            if (next == null) return 0;

            int played = 0;
            foreach (var f in season.fixtures.Where(f => f.matchday == next.matchday && !f.isPlayed).ToList())
            {
                if (MatchDay.SimulateFixture(w, f, seedSalt) != null) played++;
            }
            season.currentMatchday = Math.Max(season.currentMatchday, next.matchday);
            Standings.Recalculate(league);
            return played;
        }

        public static bool LeagueFinished(LeagueData league)
            => league?.currentSeason?.Finished ?? true;

        // ── 직접 치르는 경기 ──────────────────────────────────────────────

        /// <summary>
        /// 플레이어가 블록 단위로 진행할 경기를 연다. 아직 세계에는 아무것도 반영하지 않는다.
        /// 끝나면 CompleteUserMatch 를 불러야 결과가 기록되고 같은 라운드의 다른 경기가 치러진다.
        /// </summary>
        public static MatchSession BeginUserMatch(GameWorld w, FixtureData fixture, int seedSalt = 0)
        {
            var home = MatchDay.BuildLineup(w, fixture.homeClubId);
            var away = MatchDay.BuildLineup(w, fixture.awayClubId);
            if (home == null || away == null) return null;

            var ctx = new MatchSimContext
            {
                fixtureId = fixture.id,
                home = home,
                away = away,
                seed = SeedMix.Of(w.Seed, fixture.id, seedSalt),
                rules = w.LeagueOf(fixture.homeClubId)?.matchRules ?? new MatchRules(),
            };

            return MatchSession.Begin(ctx, w.Lookup);
        }

        /// <summary>
        /// 직접 치른 경기를 마무리하고, 같은 라운드의 나머지 경기를 돌린 뒤 한 주를 넘긴다.
        /// 자동 진행(PlayNextRound) 과 뒷처리가 같아야 한다.
        /// </summary>
        public static MatchResultData CompleteUserMatch(
            GameWorld w, FixtureData fixture, MatchSession session, int seedSalt = 0)
        {
            if (fixture.isPlayed) return fixture.result;

            var result = session.Finish();

            // 자동 진행과 같은 후처리를 쓴다. 두 벌로 두면 직접 치른 경기와
            // 자동으로 돌린 경기의 선수 기록이 서로 달라진다.
            MatchEngine.ApplyResult(result, session.Context, w.Lookup);

            fixture.result = result;
            fixture.isPlayed = true;

            MatchDay.ApplyMatchFatigue(w, session.Context, result);
            MatchDay.ApplyMatchMorale(w, fixture, result);

            int matchday = fixture.matchday;
            w.Date = fixture.date;

            foreach (var league in w.Leagues.Values)
            {
                var season = league.currentSeason;
                if (season == null) continue;

                foreach (var f in season.fixtures.Where(f => f.matchday == matchday && !f.isPlayed).ToList())
                    MatchDay.SimulateFixture(w, f, seedSalt);

                season.currentMatchday = Math.Max(season.currentMatchday, matchday);
                Standings.Recalculate(league);
            }

            RunCupRound(w, matchday, new Random(SeedMix.Of(w.Seed, matchday * 101, 7331)), skipUserTie: false);

            // A매치 주간. 용도별로 난수를 따로 쓴다 — 같은 rng 를 쓰면 컵 추첨이 밀린다.
            if (NationalTeam.IsBreak(matchday))
                NationalTeam.RunBreak(w, new Random(SeedMix.Of(w.Seed, matchday * 211, 5501)));

            if (ReserveLeague.IsMatchday(matchday))
                ReserveLeague.RunMatchday(w, new Random(SeedMix.Of(w.Seed, matchday * 307, 1511)));

            AfterUserMatch(w, fixture, result);
            WeekCycle.Advance(w, fixture.date.AddDays(7));
            return result;
        }

        // ── 경기 후 처리 ──────────────────────────────────────────────────

        static void AfterUserMatch(GameWorld w, FixtureData f, MatchResultData r)
        {
            if (r == null) return;

            bool home = f.homeClubId == w.ManagedClubId;
            int us = home ? r.homeGoals : r.awayGoals;
            int them = home ? r.awayGoals : r.homeGoals;
            int oppId = home ? f.awayClubId : f.homeClubId;
            var opp = w.Club(oppId);

            BoardSystem.ApplyMatchResult(w.Manager, new BoardMatchOutcome
            {
                wasWin = us > them,
                wasDraw = us == them,
                ourClubReputation = w.MyClub?.reputation ?? 3,
                opponentReputation = opp?.reputation ?? 3,
                wasHome = home,
            });

            if (us > them) w.Manager.totalWins++;
            else if (us == them) w.Manager.totalDraws++;
            else w.Manager.totalLosses++;

            AddYouthMinutes(w, f);

            string vs = $"{(home ? "홈" : "원정")} {opp?.name}";
            w.Post($"{us}-{them} {(us > them ? "승리" : us == them ? "무승부" : "패배")}",
                   $"{vs} 경기. 리그 {f.matchday}라운드.", InboxKind.Match);

            if (w.Manager.boardConfidence < BalanceConfig.SackThreshold && !w.Sacked)
            {
                w.Sacked = true;
                w.SackReason = "이사회가 신뢰를 거뒀습니다.";

                // 경질은 커리어의 한 장면이다. 바로 맡을 수 있는 자리를 띄워 준다.
                ManagerCareer.RollVacancies(w, new Random(SeedMix.Of(w.Seed, w.Date.year * 53 + w.Date.month, 909)));
                w.Post("경질",
                       "이사회가 감독직 해임을 통보했습니다. 다른 구단의 자리를 알아볼 수 있습니다.",
                       InboxKind.Board);
            }
        }

        // ── 주간 진행 ─────────────────────────────────────────────────────

        /// <summary>U21 선발 출전 시간을 이사회 유스 목표에 적립한다.</summary>
        static void AddYouthMinutes(GameWorld w, FixtureData f)
        {
            var youth = w.Objectives.FirstOrDefault(
                o => o.type == BoardObjectiveType.DevelopYouthMinutes);
            if (youth == null) return;

            var lineup = MatchDay.BuildLineup(w, w.ManagedClubId);
            if (lineup == null) return;

            int minutes = lineup.tactics.slots
                .Select(s => w.Player(s.assignedPlayerId))
                .Where(p => p != null && p.GetAge(w.Date) <= 21)
                .Sum(_ => 90);

            if (minutes > 0) BoardSystem.AddYouthMinutes(youth, minutes);
        }

        // ── 컵 ────────────────────────────────────────────────────────────

        /// <summary>이번 리그 라운드에 걸린 컵 라운드를 치른다.</summary>
        static void RunCupRound(GameWorld w, int matchday, Random rng, bool skipUserTie)
        {
            foreach (var cup in w.Cups.Values.ToList())
            {
                if (!CupCompetition.HasRound(cup, matchday)) continue;

                int skip = skipUserTie ? w.ManagedClubId : -1;

                // 조별리그가 남아 있으면 그쪽을, 아니면 토너먼트를 치른다.
                if (CupCompetition.HasGroupMatches(cup, matchday))
                    CupCompetition.PlayGroupMatchday(w, cup, matchday, rng, skip);
                else
                    CupCompetition.PlayRound(w, cup, rng, skip);

                // 내가 참가한 대회만 알린다. 대륙 대회는 나라가 비어 있으므로 참가 명단으로 본다.
                if (w.ManagedClubId > 0 && cup.entrants.Contains(w.ManagedClubId))
                    ReportCupRound(w, cup);
            }
        }

        static void ReportCupRound(GameWorld w, CupState cup)
        {
            // 직전에 끝난 라운드에서 플레이어 구단의 결과를 찾는다.
            var round = cup.rounds.LastOrDefault(r => r.ties.Any(t => t.played));
            var tie = round?.ties.FirstOrDefault(t =>
                t.played && (t.homeClubId == w.ManagedClubId || t.awayClubId == w.ManagedClubId));
            if (tie == null) return;

            bool home = tie.homeClubId == w.ManagedClubId;
            int us = home ? tie.homeGoals : tie.awayGoals;
            int them = home ? tie.awayGoals : tie.homeGoals;
            int oppId = home ? tie.awayClubId : tie.homeClubId;
            bool won = tie.WinnerClubId == w.ManagedClubId;

            string pens = tie.decidedOnPenalties
                ? $" (승부차기 {(home ? tie.homePens : tie.awayPens)}-{(home ? tie.awayPens : tie.homePens)})"
                : "";

            w.Post($"{cup.name} {round!.name} {(won ? "승리" : "탈락")}",
                   $"{w.Club(oppId)?.name} 와 {us}-{them}{pens}.",
                   InboxKind.Match);
        }

        // ── 이적 ──────────────────────────────────────────────────────────

        // ── 목표와 시즌 ───────────────────────────────────────────────────

        // ── 승강 ──────────────────────────────────────────────────────────

        // ── 시즌 전환 ─────────────────────────────────────────────────────

        // ── 감독 취임 ─────────────────────────────────────────────────────

    }
}
