using System;
using System.Text.Json.Serialization;
using System.Collections.Generic;
using System.Linq;
using FootballTactician.Data;

namespace FootballTactician.World
{

    public static class CupCompetition
    {
        /// <summary>국내 컵이 치러지는 리그 라운드. 리그 일정 사이에 끼워 넣는다.</summary>
        public static readonly int[] DomesticMatchdays = { 4, 9, 14, 20, 26, 32, 36 };

        /// <summary>대륙 대회 라운드. 국내 컵과 겹치지 않는 주를 쓴다.</summary>
        public static readonly int[] ContinentalMatchdays = { 7, 12, 18, 24, 30, 34 };

        /// <summary>
        /// 한 나라의 컵을 짠다. 1부와 2부가 모두 참가한다.
        /// </summary>
        public static CupState Build(GameWorld w, NationState nation, int year, Random rng)
        {
            var clubs = nation.LeagueIds
                .SelectMany(id => w.League(id)?.clubIds ?? new List<int>())
                .ToList();
            var nationNames = NameForge.ByCode(nation.Code);

            return Build(w, new CupState
            {
                id = nation.LeagueIds[0] * 1000 + 1,
                name = $"{nation.Country} 컵",
                shortName = $"{nationNames.Code}C",
                country = nation.Country,
                year = year,
                roundMatchdays = DomesticMatchdays,
            }, clubs, rng);
        }

        /// <summary>
        /// 참가 구단을 받아 대진을 짠다. 참가 수를 2의 거듭제곱으로 맞추고,
        /// 남는 자리는 평판이 높은 구단이 부전승으로 건너뛴다.
        /// </summary>
        public static CupState Build(GameWorld w, CupState cup, List<int> clubs, Random rng)
        {
            if (clubs.Count < 2) return null;
            cup.entrants = clubs;
            int year = cup.year;

            // 예선 라운드를 치르고 나면 참가 팀이 2의 거듭제곱이 되도록 맞춘다.
            // P = 40 이하 최대 거듭제곱(32)이면 예선은 40-32=8경기, 부전승은 40-16=24팀.
            // 거꾸로 잡으면 생존 팀이 홀수가 되는 라운드가 생겨 한 팀이 대진에서 사라진다.
            int power = 1;
            while (power * 2 <= clubs.Count) power *= 2;
            int prelimTies = clubs.Count - power;

            var seeded = clubs
                .OrderByDescending(id => w.Club(id)!.reputation)
                .ThenByDescending(id => w.Strength(id))
                .ToList();

            if (prelimTies == 0)
            {
                // 이미 거듭제곱이면 예선 없이 바로 본선
                var shuffledAll = seeded.OrderBy(_ => rng.Next()).ToList();
                cup.rounds.Add(MakeRound(cup, 0, shuffledAll, year));
                return cup;
            }

            // 평판이 낮은 쪽이 예선을 치른다.
            var prelimClubs = seeded.TakeLast(prelimTies * 2).OrderBy(_ => rng.Next()).ToList();
            var byeClubs = seeded.Take(clubs.Count - prelimTies * 2).ToList();

            cup.rounds.Add(MakeRound(cup, 0, prelimClubs, year, "예선"));
            cup.PendingByes = byeClubs;
            return cup;
        }

        /// <summary>나라마다 1부 상위 몇 팀이 대륙 대회에 나가는지.</summary>
        public const int ContinentalSpotsPerNation = 4;

        public const int ContinentalCupId = 90001;

        /// <summary>
        /// 대륙 대회. 각 나라 1부의 지난 시즌 상위 팀이 모인다.
        /// 첫 시즌은 지난 성적이 없으므로 전력으로 가린다.
        /// </summary>
        /// <summary>
        /// 지금 순위표로 본 대륙 대회 출전 구단.
        ///
        /// 시즌을 넘길 때는 순위표를 비우기 전에 불러야 한다. 비운 뒤에 부르면
        /// 모두 0승이라 전력 순으로 떨어지고, 지난 시즌 성적이 출전권과 무관해진다.
        /// </summary>
        public static List<int> Qualifiers(GameWorld w)
        {
            var qualified = new List<int>();

            foreach (var nation in w.Nations)
            {
                var top = w.League(nation.LeagueIds.FirstOrDefault());
                if (top?.currentSeason == null) continue;

                // 치른 경기가 있으면 순위를, 없으면(첫 시즌) 전력을 쓴다.
                bool played = top.currentSeason.standings.Any(s => s.played > 0);
                var order = played
                    ? top.currentSeason.standings.OrderBy(s => s.position).Select(s => s.clubId)
                    : top.clubIds.OrderByDescending(id => w.Strength(id));

                qualified.AddRange(order.Take(ContinentalSpotsPerNation));
            }

            return qualified;
        }

        /// <summary>조별리그가 치러지는 리그 라운드.</summary>
        public static readonly int[] GroupMatchdays = { 3, 7, 11, 15, 19, 23 };

        /// <summary>조별리그 뒤 토너먼트가 치러지는 리그 라운드.</summary>
        public static readonly int[] ContinentalKnockoutMatchdays = { 27, 31, 35 };

        public const int GroupCount = 4;
        public const int GroupSize = 4;

        /// <summary>조마다 몇 팀이 토너먼트에 오르는지.</summary>
        public const int QualifyPerGroup = 2;

        public static CupState BuildContinental(
            GameWorld w, int year, Random rng, List<int> entrants = null)
        {
            var qualified = entrants ?? Qualifiers(w);
            if (qualified.Count < 2) return null;

            // 16팀이 모이면 조별리그부터 치른다. 모자라면 예전처럼 바로 토너먼트.
            if (qualified.Count == GroupCount * GroupSize)
                return BuildWithGroups(w, qualified, year, rng);

            return Build(w, new CupState
            {
                id = ContinentalCupId,
                name = "대륙 챔피언스컵",
                shortName = "CCC",
                country = "",
                year = year,
                roundMatchdays = ContinentalMatchdays,
                continental = true,
            }, qualified, rng);
        }

        /// <summary>
        /// 조별리그로 시작하는 대륙 대회.
        ///
        /// 같은 나라 팀은 한 조에 넣지 않는다 — 리그에서 매주 만나는 팀을 대륙 대회에서
        /// 또 만나면 나라를 넘는 대회라는 의미가 없다.
        /// </summary>
        static CupState BuildWithGroups(GameWorld w, List<int> qualified, int year, Random rng)
        {
            var cup = new CupState
            {
                id = ContinentalCupId,
                name = "대륙 챔피언스컵",
                shortName = "CCC",
                country = "",
                year = year,
                roundMatchdays = ContinentalKnockoutMatchdays,
                continental = true,
                entrants = qualified,
            };

            // 나라별로 나눠 놓고 돌아가며 뽑으면 같은 나라가 한 조에 겹치지 않는다.
            var byNation = qualified
                .GroupBy(id => w.NationOf(id)?.Country ?? "")
                .OrderByDescending(g => g.Count())
                .Select(g => new Queue<int>(g.OrderBy(_ => rng.Next())))
                .ToList();

            for (int i = 0; i < GroupCount; i++)
                cup.groups.Add(new CupGroup { name = $"{(char)('A' + i)}조" });

            // 나라 번호와 뽑은 횟수를 더해 조를 정한다.
            //
            // 순번을 하나씩 올리며 `% 조수` 로 돌리면 안 된다 — 나라 수와 조 수가 같으면
            // 한 바퀴 돌 때마다 같은 조로 되돌아와 한 조가 같은 나라로만 채워진다.
            // 실제로 그렇게 뒀더니 A조가 전부 노르셀 팀이었다.
            for (int n = 0; n < byNation.Count; n++)
            {
                int taken = 0;
                while (byNation[n].Count > 0)
                {
                    cup.groups[(n + taken) % GroupCount].clubIds.Add(byNation[n].Dequeue());
                    taken++;
                }
            }

            foreach (var group in cup.groups)
                BuildGroupFixtures(group, year, rng);

            return cup;
        }

        /// <summary>조 안의 홈·원정 일정. 네 팀이면 여섯 경기를 여섯 주차에 나눠 치른다.</summary>
        static void BuildGroupFixtures(CupGroup group, int year, Random rng)
        {
            var ids = group.clubIds.OrderBy(_ => rng.Next()).ToList();
            int day = 0;

            for (int i = 0; i < ids.Count; i++)
            for (int j = i + 1; j < ids.Count; j++)
            {
                // 홈·원정을 번갈아 줘서 한 팀이 홈만 치르지 않게 한다.
                bool flip = (i + j) % 2 == 1;
                group.fixtures.Add(new CupTie
                {
                    homeClubId = flip ? ids[j] : ids[i],
                    awayClubId = flip ? ids[i] : ids[j],
                    matchday = GroupMatchdays[day % GroupMatchdays.Length],
                    date = FixtureScheduler.MatchdayDate(year, GroupMatchdays[day % GroupMatchdays.Length]),
                });
                day++;
            }
        }

        /// <summary>참가 팀을 둘씩 묶어 라운드를 만든다. 홀수면 마지막 한 팀은 부전승으로 넘긴다.</summary>
        static CupRound MakeRound(CupState cup, int index, List<int> teams, int year, string name = null)
        {
            var days = cup.roundMatchdays ?? DomesticMatchdays;
            int matchday = index < days.Length ? days[index] : days[^1];

            var round = new CupRound
            {
                index = index,
                name = name ?? RoundName(teams.Count),
                matchday = matchday,
                date = FixtureScheduler.SeasonStart(year).AddDays((matchday - 1) * 7 + 3),
            };

            for (int i = 0; i + 1 < teams.Count; i += 2)
                round.ties.Add(new CupTie { homeClubId = teams[i], awayClubId = teams[i + 1] });

            // 홀수로 남은 팀은 버리지 않고 다음 라운드로 올린다.
            if (teams.Count % 2 == 1) round.ByeClubId = teams[^1];

            return round;
        }

        public static string RoundName(int teams) => teams switch
        {
            2 => "결승",
            4 => "준결승",
            8 => "8강",
            16 => "16강",
            32 => "32강",
            64 => "64강",
            _ => $"{teams}강",
        };

        /// <summary>그 리그 라운드에 치를 컵 경기가 있는지.</summary>
        public static bool HasRound(CupState cup, int matchday)
            => (cup?.Current != null && !cup.Finished && cup.Current.matchday == matchday)
               || HasGroupMatches(cup, matchday);

        /// <summary>이번 주차에 치를 조별리그 경기가 있는지.</summary>
        public static bool HasGroupMatches(CupState cup, int matchday)
            => cup is { continental: true }
               && cup.groups.Any(g => g.fixtures.Any(f => !f.played && f.matchday == matchday));

        /// <summary>그 구단의 이번 주차 조별리그 경기. 없으면 null.</summary>
        public static CupTie GroupMatchFor(CupState cup, int clubId, int matchday)
            => cup?.groups
                .SelectMany(g => g.fixtures)
                .FirstOrDefault(f => !f.played && f.matchday == matchday
                                  && (f.homeClubId == clubId || f.awayClubId == clubId));

        /// <summary>
        /// 이번 주차의 조별리그 경기를 치른다.
        /// 조별리그가 모두 끝나면 각 조 상위 팀으로 토너먼트 대진을 짠다.
        /// </summary>
        public static void PlayGroupMatchday(GameWorld w, CupState cup, int matchday, Random rng,
                                             int skipClubId = -1)
        {
            if (cup is not { continental: true } || !cup.HasGroups) return;

            foreach (var group in cup.groups)
            foreach (var f in group.fixtures.Where(x => !x.played && x.matchday == matchday).ToList())
            {
                if (skipClubId > 0 && (f.homeClubId == skipClubId || f.awayClubId == skipClubId))
                    continue;

                var r = MatchDay.SimulateCupTie(w, cup, f);
                ApplyGroupResult(f, r.home, r.away);
            }

            if (cup.GroupsDone && cup.rounds.Count == 0)
                BuildKnockoutFromGroups(cup, rng);
        }

        /// <summary>조별리그 경기 결과. 무승부가 그대로 남는다 — 승부차기는 토너먼트에서만.</summary>
        public static void ApplyGroupResult(CupTie tie, int homeGoals, int awayGoals)
        {
            tie.played = true;
            tie.homeGoals = homeGoals;
            tie.awayGoals = awayGoals;
            tie.WinnerClubId = homeGoals > awayGoals ? tie.homeClubId
                             : awayGoals > homeGoals ? tie.awayClubId
                             : 0;
        }

        /// <summary>
        /// 조별리그 결과로 8강 대진을 짠다.
        /// 1위는 다른 조 2위와 만난다 — 같은 조에서 올라온 둘이 곧바로 또 만나면 밋밋하다.
        /// </summary>
        static void BuildKnockoutFromGroups(CupState cup, Random rng)
        {
            var winners = new List<int>();
            var runnersUp = new List<int>();

            foreach (var group in cup.groups)
            {
                var standing = group.Standing();
                if (standing.Count > 0) winners.Add(standing[0]);
                if (standing.Count > 1) runnersUp.Add(standing[1]);
            }

            // 2위를 한 칸 밀어 같은 조끼리 1회전에서 만나지 않게 한다.
            runnersUp = runnersUp.Skip(1).Concat(runnersUp.Take(1)).ToList();

            var order = new List<int>();
            for (int i = 0; i < winners.Count; i++)
            {
                order.Add(winners[i]);
                if (i < runnersUp.Count) order.Add(runnersUp[i]);
            }

            cup.rounds.Add(MakeRound(cup, 0, order, cup.year));
        }

        /// <summary>
        /// 현재 라운드를 치른다. 플레이어 구단 경기는 skipClubId 로 건너뛸 수 있다.
        /// 건너뛴 경기는 호출한 쪽이 직접 치르고 ApplyTie 로 결과를 넣는다.
        /// </summary>
        public static void PlayRound(GameWorld w, CupState cup, Random rng, int skipClubId = -1)
        {
            var round = cup?.Current;
            if (round == null || cup.Finished) return;

            foreach (var tie in round.ties.Where(t => !t.played).ToList())
            {
                if (skipClubId > 0 && (tie.homeClubId == skipClubId || tie.awayClubId == skipClubId))
                    continue;

                var result = MatchDay.SimulateCupTie(w, cup, tie);
                ApplyTie(cup, tie, result.home, result.away, rng);
            }

            if (round.ties.All(t => t.played)) Advance(w, cup, rng);
        }

        /// <summary>한 경기의 결과를 넣고 승자를 가린다. 비기면 승부차기.</summary>
        public static void ApplyTie(CupState cup, CupTie tie, int homeGoals, int awayGoals, Random rng)
        {
            tie.homeGoals = homeGoals;
            tie.awayGoals = awayGoals;
            tie.played = true;

            if (homeGoals != awayGoals)
            {
                tie.WinnerClubId = homeGoals > awayGoals ? tie.homeClubId : tie.awayClubId;
                return;
            }

            // 승부차기 — 5번씩 차고 같으면 서든데스
            tie.decidedOnPenalties = true;
            int h = 0, a = 0;
            for (int i = 0; i < 5; i++)
            {
                if (rng.NextDouble() < 0.76) h++;
                if (rng.NextDouble() < 0.76) a++;
            }
            while (h == a)
            {
                bool hs = rng.NextDouble() < 0.76;
                bool as_ = rng.NextDouble() < 0.76;
                if (hs) h++;
                if (as_) a++;
                if (hs != as_) break;
            }
            tie.homePens = h;
            tie.awayPens = a;
            tie.WinnerClubId = h > a ? tie.homeClubId : tie.awayClubId;
        }

        /// <summary>라운드가 끝나면 다음 라운드를 짠다.</summary>
        public static void Advance(GameWorld w, CupState cup, Random rng)
        {
            var round = cup.Current;
            if (round == null || !round.Played) return;

            var survivors = round.ties.Select(t => t.WinnerClubId).Where(id => id > 0).ToList();
            if (round.ByeClubId > 0) survivors.Add(round.ByeClubId);

            if (cup.PendingByes is { Count: > 0 })
            {
                survivors.AddRange(cup.PendingByes);
                cup.PendingByes = new List<int>();
            }

            if (survivors.Count <= 1)
            {
                cup.winnerClubId = survivors.FirstOrDefault();
                cup.currentRound = cup.rounds.Count;
                if (cup.winnerClubId > 0)
                    w.Post($"{cup.name} 우승",
                           $"{w.Club(cup.winnerClubId)?.name} 이 {cup.name} 을 들어올렸습니다.",
                           InboxKind.News);
                return;
            }

            var next = MakeRound(cup, cup.rounds.Count, survivors.OrderBy(_ => rng.Next()).ToList(), cup.year);
            cup.rounds.Add(next);
            cup.currentRound = next.index;
        }

        /// <summary>그 구단이 이번 라운드에 치를 컵 경기.</summary>
        /// <summary>
        /// 그 구단의 치를 차례인 경기. 조별리그가 남아 있으면 그쪽을 먼저 본다 —
        /// 토너먼트만 보면 조별리그 여섯 경기를 플레이어가 직접 치르지 못한다.
        /// </summary>
        public static CupTie TieFor(CupState cup, int clubId)
        {
            if (cup == null) return null;

            if (cup.HasGroups && !cup.GroupsDone)
                return cup.groups
                    .SelectMany(g => g.fixtures)
                    .Where(f => !f.played && (f.homeClubId == clubId || f.awayClubId == clubId))
                    .OrderBy(f => f.matchday)
                    .FirstOrDefault();

            return cup.Current?.ties.FirstOrDefault(
                t => !t.played && (t.homeClubId == clubId || t.awayClubId == clubId));
        }

        /// <summary>그 구단이 아직 컵에 남아 있는지.</summary>
        public static bool StillIn(CupState cup, int clubId)
        {
            if (cup == null) return false;
            if (cup.Finished) return cup.winnerClubId == clubId;

            // 조별리그 중에는 조에 이름이 있으면 남아 있는 것이다.
            if (cup.HasGroups && !cup.GroupsDone)
                return cup.groups.Any(g => g.clubIds.Contains(clubId));

            if (cup.Current?.ties.Any(t => t.homeClubId == clubId || t.awayClubId == clubId) == true)
                return true;
            if (cup.Current?.ByeClubId == clubId) return true;
            return cup.PendingByes?.Contains(clubId) == true;
        }
        /// <summary>그 구단이 대회에서 이긴 라운드 수.</summary>
        public static int ContinentalRoundsWon(CupState cup, int clubId)
        {
            int won = 0;

            // 조별리그를 통과한 것도 한 단계 올라간 것이다.
            // 세지 않으면 "본선 진출" 목표가 8강을 이겨야만 달성된다.
            if (cup.HasGroups)
            {
                var group = cup.groups.FirstOrDefault(g => g.clubIds.Contains(clubId));
                if (group == null) return 0;
                if (!cup.GroupsDone) return 0;

                int place = group.Standing().IndexOf(clubId);
                if (place < 0 || place >= CupCompetition.QualifyPerGroup) return 0;
                won++;
            }
            foreach (var round in cup.rounds)
            {
                if (round.ByeClubId == clubId) { won++; continue; }
                var tie = round.ties.FirstOrDefault(
                    t => t.played && (t.homeClubId == clubId || t.awayClubId == clubId));
                if (tie?.WinnerClubId == clubId) won++;
            }
            return won;
        }

        /// <summary>플레이어 구단의 이번 컵 경기. 없으면 null.</summary>
        public static (CupState cup, CupTie tie) UserCupTie(GameWorld w)
        {
            foreach (var cup in w.Cups.Values)
            {
                var tie = CupCompetition.TieFor(cup, w.ManagedClubId);
                if (tie != null) return (cup, tie);
            }
            return (null, null);
        }

        /// <summary>플레이어 구단이 속한 나라의 국내 컵.</summary>
        /// <summary>감독이 맡은 구단이 나가는 국내 컵.</summary>
        public static CupState UserCup(GameWorld w) => CupFor(w, w.ManagedClubId);

        /// <summary>대륙 대회. 플레이어 구단이 참가했는지와 무관하게 돌려준다.</summary>
        public static CupState ContinentalCup(GameWorld w)
            => w.Cups.Values.FirstOrDefault(c => c.continental);

        /// <summary>그 구단이 나가는 국내 컵. 같은 나라의 대회 하나.</summary>
        public static CupState CupFor(GameWorld w, int clubId)
        {
            var nation = w.NationOf(clubId);
            return nation == null
                ? null
                : w.Cups.Values.FirstOrDefault(c => !c.continental && c.country == nation.Country);
        }
        /// <summary>시즌마다 나라별 컵을 새로 짠다.</summary>
        /// <param name="continentalEntrants">
        /// 대륙 대회 출전 구단. 시즌을 넘길 때는 순위표를 비우기 전에 잡아 둔 명단을 넘긴다.
        /// </param>
        /// <summary>한 시즌의 대회를 모두 만든다. 시즌 넘김이 부른다.</summary>
        public static void BuildAll(GameWorld w, Random rng, List<int> continentalEntrants = null)
        {
            w.Cups.Clear();
            foreach (var nation in w.Nations)
            {
                var cup = CupCompetition.Build(w, nation, w.SeasonYear, rng);
                if (cup != null) w.Cups[cup.id] = cup;
            }

            var continental = CupCompetition.BuildContinental(w, w.SeasonYear, rng, continentalEntrants);
            if (continental != null) w.Cups[continental.id] = continental;
        }
    }
}
