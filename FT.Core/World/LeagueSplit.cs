using System;
using System.Collections.Generic;
using System.Linq;
using FootballTactician.Data;
using FootballTactician.Simulation;

namespace FootballTactician.World
{
    /// <summary>
    /// 정규리그를 치른 뒤 상·하위로 갈라 자기들끼리 더 치른다 (docs/리그-제도.md ⑤).
    ///
    /// 스코틀랜드·한국·덴마크·오스트리아·스위스·벨기에·루마니아가 이렇게 한다.
    /// 우승은 상위 조에서만 나오고, 강등은 하위 조에서만 나온다.
    ///
    /// <b>조 경계는 승점보다 우선한다.</b> 하위 조 1위가 상위 조 꼴찌보다 승점이 많아도
    /// 위로 올라서지 못한다 — 그게 스플릿의 핵심이고, 그래서 정규리그 막판이 팽팽해진다.
    ///
    /// 오스트리아·스위스·루마니아·벨기에는 갈라질 때 <b>승점을 절반으로</b> 줄인다
    /// (올림). 정규리그에서 벌린 차이를 반으로 접어 막판을 다시 붙여 놓는 장치다.
    /// </summary>
    public static class LeagueSplit
    {
        /// <summary>
        /// 정규리그가 끝났으면 갈라 놓는다. 이미 갈랐거나 스플릿이 없는 리그면 아무 일도 없다.
        ///
        /// 순위가 정해진 뒤에 불러야 한다 — 누가 상위 조인지는 순위표가 정한다.
        /// </summary>
        public static void ApplyIfDue(GameWorld w, LeagueData league)
        {
            var season = league?.currentSeason;
            if (season == null || season.splitMatchday > 0) return;

            var nation = w.Nations.FirstOrDefault(n => n.LeagueIds.Contains(league.id));
            if (nation == null) return;

            var spec = NameForge.ByCode(nation.Code);
            int topSize = spec?.SplitTopSize((int)league.tier) ?? 0;
            if (topSize < 2) return;

            // 정규리그를 다 치렀을 때만. 한 경기라도 남았으면 아직이다.
            int regular = WorldGenerator.Matchdays(league.numberOfTeams);
            if (season.fixtures.Any(f => f.matchday <= regular && !f.isPlayed)) return;

            var order = season.standings.OrderBy(e => e.position).Select(e => e.clubId).ToList();
            if (order.Count != league.numberOfTeams || topSize >= order.Count) return;

            var upper = order.Take(topSize).ToList();
            var lower = order.Skip(topSize).ToList();
            if (upper.Count < 2 || lower.Count < 2) return;

            season.splitGroups = new Dictionary<int, int>();
            foreach (int id in upper) season.splitGroups[id] = 0;
            foreach (int id in lower) season.splitGroups[id] = 1;

            // 승점 절반 — 올림이다. 13점이면 7점이 된다.
            if (spec.HalvesPoints((int)league.tier))
            {
                season.pointsCarry = new Dictionary<int, int>();
                foreach (var e in season.standings)
                    season.pointsCarry[e.clubId] = (e.points + 1) / 2 - e.points;
            }

            // 조마다 한 바퀴씩 더. 여섯 팀이면 다섯 경기다.
            var rng = new Random(SeedMix.Of(w.Seed, league.id * 541 + season.year, 71123));

            // 경기 번호는 세계가 들고 있는 다음 번호에서 이어 받는다.
            int nextFixtureId = w.NextFixtureId;
            var added = new List<FixtureData>();
            added.AddRange(RoundRobin(upper, regular, season, rng, ref nextFixtureId));
            added.AddRange(RoundRobin(lower, regular, season, rng, ref nextFixtureId));
            w.NextFixtureId = nextFixtureId;

            season.fixtures.AddRange(added);
            season.splitMatchday = regular;
            season.totalMatchdays = added.Count > 0 ? added.Max(f => f.matchday) : regular;
            season.endDate = FixtureScheduler.SeasonEnd(season.year, season.totalMatchdays);

            // 승점이 줄었을 수도 있으니 다시 센다.
            Standings.Recalculate(league);

            if (w.ManagedClubId > 0 && league.clubIds.Contains(w.ManagedClubId))
            {
                bool mine = season.splitGroups[w.ManagedClubId] == 0;
                string half = spec.HalvesPoints((int)league.tier) ? " 승점은 절반에서 다시 시작합니다." : "";
                w.Post($"{league.name} 스플릿",
                       $"정규리그가 끝나고 상위 {upper.Count}팀과 하위 {lower.Count}팀으로 갈렸습니다. "
                       + $"{w.MyClub?.name} 은 {(mine ? "상위" : "하위")} 조입니다."
                       + $" 여기서부터는 같은 조끼리만 치릅니다.{half}",
                       InboxKind.News);
            }
        }

        /// <summary>
        /// 이미 치른 경기 뒤에 붙이는 한 바퀴. 서클 방식이고, 홈은 누적 횟수로 고른다.
        ///
        /// 정규리그에서 원정이 많았던 팀이 홈을 먼저 받는다 — 스플릿은 경기 수가 적어
        /// 한쪽으로 몰리면 그대로 억울해진다.
        /// </summary>
        static List<FixtureData> RoundRobin(List<int> clubs, int baseMatchday, LeagueSeason season,
                                            Random rng, ref int nextFixtureId)
        {
            var ids = clubs.OrderBy(_ => rng.Next()).ToList();
            if (ids.Count % 2 == 1) ids.Add(-1);

            int n = ids.Count;
            var fixtures = new List<FixtureData>();

            // 정규리그의 홈 횟수를 이어받는다.
            var homeCount = clubs.ToDictionary(
                id => id,
                id => season.fixtures.Count(f => f.homeClubId == id && f.matchday <= baseMatchday));

            var rot = ids.Skip(1).ToList();

            for (int r = 0; r < n - 1; r++)
            {
                var ring = rot.Skip(r).Concat(rot.Take(r)).ToList();

                var pairs = new List<(int, int)> { (ids[0], ring[n - 2]) };
                for (int k = 0; k < n / 2 - 1; k++)
                    pairs.Add((ring[k], ring[n - 3 - k]));

                int matchday = baseMatchday + r + 1;

                foreach (var (x, y) in pairs)
                {
                    if (x < 0 || y < 0) continue;

                    // 홈이 적었던 쪽이 홈이다. 같으면 번호가 작은 쪽.
                    int home = homeCount[x] < homeCount[y] ? x
                             : homeCount[y] < homeCount[x] ? y
                             : Math.Min(x, y);
                    int away = home == x ? y : x;
                    homeCount[home]++;

                    fixtures.Add(new FixtureData
                    {
                        id = nextFixtureId++,
                        homeClubId = home,
                        awayClubId = away,
                        date = FixtureScheduler.SeasonStart(season.year).AddDays((matchday - 1) * 7),
                        matchday = matchday,
                        isPlayed = false,
                    });
                }
            }

            return fixtures;
        }

        /// <summary>그 구단의 조. 0 이 상위, 1 이 하위, 스플릿 전이면 0.</summary>
        public static int GroupOf(LeagueSeason season, int clubId)
            => season?.splitGroups != null && season.splitGroups.TryGetValue(clubId, out int g) ? g : 0;
    }
}
