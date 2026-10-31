using System;
using System.Collections.Generic;
using System.Linq;
using FootballTactician.Data;

namespace FootballTactician.World
{
    /// <summary>
    /// 더블 라운드로빈 일정. 서클 방식으로 전반기 대진을 짜고, 후반기는 홈·원정을 뒤집는다.
    /// 한 라운드가 한 주이며, 시즌은 8월 둘째 주에 시작한다.
    /// </summary>
    public static class FixtureScheduler
    {
        public static List<FixtureData> Build(
            IReadOnlyList<int> clubIds, GameDate firstMatchday, int fixtureIdBase, Random rng)
        {
            // 구단 순서를 섞어 시드마다 대진 순서가 달라지게 한다.
            var ids = clubIds.OrderBy(_ => rng.Next()).ToList();
            if (ids.Count % 2 == 1) ids.Add(-1);              // 부전승 자리

            int n = ids.Count;
            int rounds = n - 1;

            var fixtures = new List<FixtureData>();
            int nextId = fixtureIdBase;

            // 홈 횟수와 직전 경기 장소를 보고 홈 팀을 정한다.
            //
            // 서클 방식의 짝 번호로 홈을 정하면, 회전 때문에 라운드 번호가 상쇄되어
            // 한 구단이 전반기 내내 홈만 치르는 일정이 나온다. 그래서 누적으로 판단한다.
            var homeCount = clubIds.ToDictionary(id => id, _ => 0);
            var lastWasHome = clubIds.ToDictionary(id => id, _ => (bool?)null);

            // 같은 장소가 몇 경기째 이어지는지. 홈 횟수만 맞추면 3연속, 4연속이 흔히 나온다.
            var runLength = clubIds.ToDictionary(id => id, _ => 0);

            var rot = ids.Skip(1).ToList();

            for (int r = 0; r < rounds; r++)
            {
                var ring = rot.Skip(r).Concat(rot.Take(r)).ToList();

                var pairs = new List<(int, int)> { (ids[0], ring[n - 2]) };
                for (int k = 0; k < n / 2 - 1; k++)
                    pairs.Add((ring[k], ring[n - 3 - k]));

                foreach (var (x, y) in pairs)
                {
                    if (x < 0 || y < 0) continue;

                    int home = PickHome(x, y, homeCount, lastWasHome, runLength);
                    int away = home == x ? y : x;

                    homeCount[home]++;
                    runLength[home] = lastWasHome[home] == true ? runLength[home] + 1 : 1;
                    runLength[away] = lastWasHome[away] == false ? runLength[away] + 1 : 1;
                    lastWasHome[home] = true;
                    lastWasHome[away] = false;

                    // 후반기는 같은 대진을 홈·원정만 뒤집는다. 다만 순서를 그대로 두면
                    // 전반기 장소 패턴이 뒤집힌 채 그대로 이어붙어 연속이 길어진다.
                    // 절반만큼 밀어 두 패턴이 어긋나게 한다.
                    int mirror = rounds + (r + rounds / 2) % rounds + 1;
                    fixtures.Add(Make(ref nextId, home, away, firstMatchday, r + 1));
                    fixtures.Add(Make(ref nextId, away, home, firstMatchday, mirror));
                }
            }

            var ordered = fixtures.OrderBy(f => f.matchday).ThenBy(f => f.id).ToList();
            RepairLongRuns(ordered, clubIds);
            return ordered;
        }

        /// <summary>
        /// 긴 연속을 국소 수선한다.
        ///
        /// 서클 방식은 대진이 고정이라, 홈을 고르는 것만으로는 드물게 4~5연속이 남는다
        /// (20개 시드에서 5연속이 0.5%). 한 쌍의 두 경기를 통째로 뒤집으면 홈 횟수는
        /// 그대로인 채 두 구단의 장소 순서만 바뀌므로, 전체가 나아질 때만 받아들인다.
        ///
        /// 벌점은 <b>구단마다 독립</b>이다. 그래서 한 쌍을 뒤집었을 때 다시 재야 하는 것은
        /// 그 두 구단뿐이고, 나머지 열여덟 구단의 값은 그대로 쓴다. 전에는 뒤집을 때마다
        /// 20구단 × 760경기를 다시 훑어, 리그 하나에 1,200만 번을 봤다(세계 생성의 40%).
        /// </summary>
        static void RepairLongRuns(List<FixtureData> fixtures, IReadOnlyList<int> clubIds)
        {
            // 구단별 경기만 따로 들고 간다. 홈·원정을 뒤집어도 그 경기가 어느 두 구단의
            // 경기인지는 바뀌지 않으므로, 이 목록은 수선이 끝날 때까지 그대로 쓸 수 있다.
            // 순서는 넘어온 그대로(라운드 → id) 유지한다 — 연속을 세는 순서가 곧 뜻이다.
            var mine = new Dictionary<int, List<FixtureData>>(clubIds.Count);
            foreach (int id in clubIds) mine[id] = new List<FixtureData>(fixtures.Count / 4);
            foreach (var f in fixtures)
            {
                if (mine.TryGetValue(f.homeClubId, out var h)) h.Add(f);
                if (mine.TryGetValue(f.awayClubId, out var a)) a.Add(f);
            }

            // 반기를 가르는 라운드. 뒤집어도 날짜는 바뀌지 않으니 한 번만 구한다.
            int last = 0;
            foreach (var f in fixtures) if (f.matchday > last) last = f.matchday;
            int half = last / 2;

            var cost = new Dictionary<int, int>(clubIds.Count);
            int total = 0;
            foreach (int id in clubIds)
            {
                cost[id] = ClubCost(mine[id], id, half);
                total += cost[id];
            }

            // 같은 두 구단이 만나는 두 경기를 짝지어 둔다.
            var pairs = fixtures
                .GroupBy(f => (Math.Min(f.homeClubId, f.awayClubId), Math.Max(f.homeClubId, f.awayClubId)))
                .Where(g => g.Count() == 2)
                .Select(g => g.ToList())
                .ToList();

            for (int pass = 0; pass < 4 && total > 0; pass++)
            {
                bool improved = false;

                foreach (var pair in pairs)
                {
                    int x = pair[0].homeClubId;
                    int y = pair[0].awayClubId;

                    Flip(pair);
                    int nx = ClubCost(mine[x], x, half);
                    int ny = ClubCost(mine[y], y, half);
                    int next = total - cost[x] - cost[y] + nx + ny;

                    if (next < total)
                    {
                        total = next;
                        cost[x] = nx;
                        cost[y] = ny;
                        improved = true;
                    }
                    else Flip(pair);          // 나아지지 않으면 되돌린다

                    if (total == 0) return;
                }

                if (!improved) return;
            }

            static void Flip(List<FixtureData> pair)
            {
                foreach (var f in pair)
                    (f.homeClubId, f.awayClubId) = (f.awayClubId, f.homeClubId);
            }
        }

        /// <summary>전반기 홈 경기 수가 절반에서 이만큼까지 벗어나는 것은 봐준다.</summary>
        const int HalfTolerance = 2;

        /// <summary>
        /// 한 구단의 벌점. 긴 연속과 전·후반 홈 쏠림을 함께 본다.
        ///
        /// 한 쌍을 뒤집으면 두 경기의 장소가 같이 바뀌는데, 두 경기가 서로 다른 반기에 있으면
        /// 홈 하나가 반기를 옮겨 간다. 연속만 보고 고치면 전반기가 원정으로 쏠린다.
        /// </summary>
        static int ClubCost(List<FixtureData> games, int id, int half)
        {
            int cost = 0;
            bool? last = null;
            int run = 0;
            int firstHalfHome = 0, homeTotal = 0;

            foreach (var f in games)
            {
                bool home = f.homeClubId == id;
                run = home == last ? run + 1 : 1;
                last = home;

                if (run > RunLimit) cost += (run - RunLimit) * (run - RunLimit);

                if (home)
                {
                    homeTotal++;
                    if (f.matchday <= half) firstHalfHome++;
                }
            }

            int drift = Math.Abs(firstHalfHome - homeTotal / 2);
            if (drift > HalfTolerance)
                cost += (drift - HalfTolerance) * (drift - HalfTolerance);

            return cost;
        }

        /// <summary>같은 장소를 이만큼 이어서 치르면 끊는 것을 우선한다.</summary>
        const int RunLimit = 3;

        /// <summary>
        /// 홈 팀을 고른다.
        ///
        /// 홈 횟수 균형만 보면 연속이 길어진다 — 홈을 덜 치른 쪽은 직전에 홈이었어도
        /// 또 홈을 받고, 원정 연속은 아예 보이지 않는다. 그래서 긴 연속을 끊는 쪽을
        /// 균형보다 먼저 본다. 끊을 연속이 없을 때만 홈 횟수로 가른다.
        /// </summary>
        static int PickHome(
            int x, int y,
            Dictionary<int, int> homeCount,
            Dictionary<int, bool?> lastWasHome,
            Dictionary<int, int> runLength)
        {
            // 한쪽이 홈 연속 한계에 닿았으면 그쪽을 원정으로 보낸다.
            bool xHomeRun = lastWasHome[x] == true && runLength[x] >= RunLimit;
            bool yHomeRun = lastWasHome[y] == true && runLength[y] >= RunLimit;
            if (xHomeRun != yHomeRun) return xHomeRun ? y : x;

            // 원정 연속이 길어진 쪽에 홈을 준다.
            bool xAwayRun = lastWasHome[x] == false && runLength[x] >= RunLimit;
            bool yAwayRun = lastWasHome[y] == false && runLength[y] >= RunLimit;
            if (xAwayRun != yAwayRun) return xAwayRun ? x : y;

            if (homeCount[x] != homeCount[y])
                return homeCount[x] < homeCount[y] ? x : y;

            bool xStreak = lastWasHome[x] == true;
            bool yStreak = lastWasHome[y] == true;
            if (xStreak != yStreak) return xStreak ? y : x;

            return x < y ? x : y;
        }

        static FixtureData Make(ref int nextId, int home, int away, GameDate first, int matchday)
            => new FixtureData
            {
                id = nextId++,
                homeClubId = home,
                awayClubId = away,
                date = first.AddDays((matchday - 1) * 7),
                matchday = matchday,
                isPlayed = false,
            };

        /// <summary>시즌 시작일 — 해당 연도 8월 8일을 기준으로 삼는다.</summary>
        public static GameDate SeasonStart(int year) => new GameDate(year, 8, 8);

        /// <summary>그 라운드 주차의 날짜. 컵·조별리그가 리그 일정 사이에 끼어들 때 쓴다.</summary>
        public static GameDate MatchdayDate(int year, int matchday)
            => SeasonStart(year).AddDays((matchday - 1) * 7);

        public static GameDate SeasonEnd(int year, int matchdays)
            => SeasonStart(year).AddDays(matchdays * 7 + 7);
    }
}
