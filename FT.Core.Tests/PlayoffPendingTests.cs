using System.Collections.Generic;
using System.Linq;
using FootballTactician.Data;
using FootballTactician.World;
using Xunit;

namespace FT.Core.Tests
{
    /// <summary>
    /// 승격·챔피언 플레이오프의 내 경기를 감독이 직접 뛴다.
    ///
    /// 이 경기들은 시즌 넘김 한가운데서 치러지므로, 넘김을 <b>끊었다 이어 붙여야</b> 한다.
    /// 지켜야 할 것은 셋이다.
    /// 1. 멈췄다 이어도 <b>한 번에 넘긴 세계와 같아야</b> 한다 — 내 경기를 같은 씨드로 돌리면
    ///    지문까지 같다. 어긋나면 무언가가 두 번 일어났거나 빠진 것이다.
    /// 2. <b>내 점수가 대진을 정한다</b> — 이기면 오르고, 지면 못 오른다.
    /// 3. 멈춘 채로 저장했다 불러와도 이어진다.
    ///
    /// 플레이오프 모양마다(잉글랜드식 승격 · 독일식 승강 · 미국식 우승) 시드 여럿으로 본다.
    /// </summary>
    public class PlayoffPendingTests
    {
        public enum Kind { None, Promotion, Survival, Champion }

        static readonly int[] Seeds = { 777, 31415, 2024 };
        static readonly string[] Codes = { "ENG", "GER", "USA" };

        static readonly Dictionary<int, byte[]> Finished = new();

        /// <summary>한 시즌을 끝까지 돌린 세계. 시드마다 한 번만 돌리고 저장본으로 복제한다.</summary>
        static GameWorld SeasonDone(int seed)
        {
            lock (Finished)
            {
                if (!Finished.TryGetValue(seed, out var bytes))
                {
                    var w = WorldGenerator.Create(seed, nations: Codes.Select(NameForge.ByCode).ToList());
                    ManagerCareer.TakeCharge(w, w.Clubs.Values.First(c => c.reputation == 3).id, "테스트");

                    for (int guard = 0; guard < 90 && !w.SeasonFinished; guard++)
                        GameEngine.PlayNextRound(w);
                    SeasonRollover.FinishRemainingFixtures(w);

                    bytes = SaveGame.Serialize(w, "시즌 끝");
                    Finished[seed] = bytes;
                }
                return SaveGame.Deserialize(bytes).world;
            }
        }

        /// <summary>
        /// 그 모양의 플레이오프에 나가는 구단 하나. 가장 낮은 씨드를 고른다 — 부전승 없이
        /// 첫 라운드부터 뛴다. 없으면 0.
        /// </summary>
        static int Pick(GameWorld w, Kind kind)
        {
            foreach (var nation in w.Nations)
            {
                var spec = NameForge.ByCode(nation.Code);

                if (kind == Kind.Champion)
                {
                    foreach (int lid in nation.LeagueIds)
                    {
                        var league = w.League(lid)!;
                        int n = ChampionPlayoff.EntrantsFor(w, league);
                        if (n < 2) continue;
                        return league.currentSeason!.standings.OrderBy(e => e.position).ElementAt(n - 1).clubId;
                    }
                    continue;
                }

                if (kind == Kind.None)
                {
                    var top = w.League(nation.LeagueIds[0])!;
                    if (ChampionPlayoff.EntrantsFor(w, top) > 0) continue;
                    return top.currentSeason!.standings.OrderBy(e => e.position).ElementAt(7).clubId;
                }

                for (int t = 0; t + 1 < nation.LeagueIds.Count; t++)
                {
                    var top = w.League(nation.LeagueIds[t])!;
                    var below = w.League(nation.LeagueIds[t + 1])!;
                    int spots = System.Math.Min(top.relegationSpots, below.promotionSpots);
                    if (spots <= 0) continue;

                    var up = TwoStage.SeasonTable(below).OrderBy(e => e.position).Select(e => e.clubId).ToList();
                    var down = TwoStage.SeasonTable(top).OrderByDescending(e => e.position).Select(e => e.clubId).ToList();
                    int entrants = spec.PlayoffTeams(t + 1);

                    if (kind == Kind.Promotion && entrants >= 2)
                        return up[spots - 1 + entrants - 1];

                    if (kind == Kind.Survival && entrants < 2 && spec.HasSurvivalPlayoff(t + 1))
                        return down[spots];
                }
            }
            return 0;
        }

        static GameWorld Ready(int seed, Kind kind)
        {
            var w = SeasonDone(seed);
            int id = Pick(w, kind);
            Assert.True(id > 0, $"시드 {seed}: {kind} 에 나갈 구단을 못 찾았다");
            w.ManagedClubId = id;
            return w;
        }

        /// <summary>멈출 때마다 "바로 결과 보기" 로 넘기며 끝까지 간다. 멈춘 횟수를 준다.</summary>
        static int RunDeferred(GameWorld w, List<string> labels = null)
        {
            int pauses = 0;
            while (!SeasonRollover.StartNextSeason(w, deferMyPlayoff: true))
            {
                Assert.True(++pauses < 20, "스무 번을 멈춰도 넘김이 끝나지 않는다");
                var p = w.pendingMatch;
                Assert.NotNull(p);
                Assert.True(p!.IsPlayoff);
                labels?.Add(p.label);
                Assert.NotNull(GameEngine.SimulatePending(w));
                Assert.Null(w.pendingMatch);
            }
            return pauses;
        }

        // ── 1. 끊어 이어도 같은 세계 ─────────────────────────────────────

        [Theory]
        [InlineData(Kind.Promotion)]
        [InlineData(Kind.Survival)]
        [InlineData(Kind.Champion)]
        [InlineData(Kind.None)]
        public void 멈췄다_이어도_한_번에_넘긴_세계와_같다(Kind kind)
        {
            foreach (int seed in Seeds)
            {
                var once = Ready(seed, kind);
                var split = Ready(seed, kind);
                Assert.Equal(SaveGame.Fingerprint(once), SaveGame.Fingerprint(split));

                SeasonRollover.StartNextSeason(once);
                int pauses = RunDeferred(split);

                if (kind == Kind.None) Assert.Equal(0, pauses);
                else Assert.True(pauses >= 1, $"시드 {seed}: {kind} 인데 한 번도 안 멈췄다");

                Assert.Null(split.rollover);
                Assert.Equal(once.SeasonYear, split.SeasonYear);
                Assert.Equal(SaveGame.Fingerprint(once), SaveGame.Fingerprint(split));
            }
        }

        [Fact]
        public void 경기_이름에_대회와_회차가_붙는다()
        {
            var w = Ready(777, Kind.Promotion);
            var labels = new List<string>();
            RunDeferred(w, labels);

            Assert.NotEmpty(labels);
            Assert.All(labels, l => Assert.Contains("승격 플레이오프", l));
            Assert.Contains(labels, l => l.EndsWith("1차전"));
            Assert.Contains(labels, l => l.EndsWith("2차전"));
        }

        /// <summary>
        /// 승강 플레이오프 이름은 화면에 그대로 뜬다. 리그 약호('GER1·GER2')로 붙였더니
        /// 대시보드와 경기 화면에 암호처럼 보였다.
        /// </summary>
        [Fact]
        public void 승강_플레이오프_이름에_약호가_없다()
        {
            var w = Ready(777, Kind.Survival);
            var labels = new List<string>();
            RunDeferred(w, labels);

            Assert.NotEmpty(labels);
            Assert.All(labels, l =>
            {
                Assert.Contains("1부·2부 승강 플레이오프", l);
                Assert.DoesNotMatch(@"[A-Z]{3}\d", l);
            });
        }

        // ── 2. 내 점수가 대진을 정한다 ───────────────────────────────────

        static void Score(GameWorld w, bool win)
        {
            var f = w.pendingMatch!.fixture;
            bool home = f.homeClubId == w.ManagedClubId;
            int us = win ? 9 : 0, them = win ? 0 : 9;
            GameEngine.ResumeAfterPending(w, home ? us : them, home ? them : us);
        }

        /// <summary>내 경기를 전부 정해 둔 점수로 치르고 끝까지 간다.</summary>
        static void RunWith(GameWorld w, bool win)
        {
            for (int guard = 0; !SeasonRollover.StartNextSeason(w, deferMyPlayoff: true); guard++)
            {
                Assert.True(guard < 20);
                Score(w, win);
            }
        }

        static int TopLeagueOf(GameWorld w, int clubId)
            => w.Nations.First(n => n.LeagueIds.Contains(w.LeagueOf(clubId)!.id)).LeagueIds[0];

        [Fact]
        public void 승격_플레이오프를_이기면_오르고_지면_남는다()
        {
            foreach (int seed in Seeds)
            {
                var win = Ready(seed, Kind.Promotion);
                int me = win.ManagedClubId;
                int from = win.LeagueOf(me)!.id;
                RunWith(win, true);
                Assert.NotEqual(from, win.LeagueOf(me)!.id);

                var lose = Ready(seed, Kind.Promotion);
                RunWith(lose, false);
                Assert.Equal(from, lose.LeagueOf(me)!.id);
            }
        }

        [Fact]
        public void 승강_플레이오프를_이기면_남고_지면_내려간다()
        {
            foreach (int seed in Seeds)
            {
                var win = Ready(seed, Kind.Survival);
                int me = win.ManagedClubId;
                int from = win.LeagueOf(me)!.id;
                RunWith(win, true);
                Assert.Equal(from, win.LeagueOf(me)!.id);

                var lose = Ready(seed, Kind.Survival);
                RunWith(lose, false);
                Assert.NotEqual(from, lose.LeagueOf(me)!.id);
            }
        }

        [Fact]
        public void 챔피언_결정전을_이기면_우승이다()
        {
            foreach (int seed in Seeds)
            {
                var win = Ready(seed, Kind.Champion);
                int me = win.ManagedClubId;
                var league = win.LeagueOf(me)!;
                RunWith(win, true);
                Assert.Equal(me, league.lastChampionClubId);

                var lose = Ready(seed, Kind.Champion);
                var league2 = lose.LeagueOf(me)!;
                RunWith(lose, false);
                Assert.NotEqual(me, league2.lastChampionClubId);
            }
        }

        // ── 3. 멈춘 채로 저장해도 이어진다 ───────────────────────────────

        [Fact]
        public void 멈춘_채로_저장했다_불러와도_같은_세계로_이어진다()
        {
            foreach (int seed in Seeds)
            {
                var once = Ready(seed, Kind.Promotion);
                SeasonRollover.StartNextSeason(once);

                var w = Ready(seed, Kind.Promotion);
                Assert.False(SeasonRollover.StartNextSeason(w, deferMyPlayoff: true));
                Assert.NotNull(w.rollover);

                // 멈출 때마다 저장했다 불러온다 — 화면에서 새로고침한 것과 같다.
                for (int guard = 0; guard < 20; guard++)
                {
                    w = SaveGame.Deserialize(SaveGame.Serialize(w, "플레이오프")).world;
                    Assert.NotNull(w.pendingMatch);
                    Assert.NotNull(w.rollover);

                    GameEngine.SimulatePending(w);
                    if (SeasonRollover.StartNextSeason(w, deferMyPlayoff: true)) break;
                }

                Assert.Null(w.rollover);

                // 통산 평점 하나만 빼고 본다. 시즌 평점은 최근 다섯 경기(recentRatings)의
                // 평균인데, 그 목록은 저장하지 않는다 — 불러온 뒤 첫 경기가 평균을 새로 연다.
                // 시즌 중에 저장했다 불러와도 똑같이 생기는 일이고, 고치면 세이브가 바뀐다.
                // 이것 말고 한 바이트라도 다르면 이어 붙이기가 무언가를 빠뜨린 것이다.
                Assert.Equal(FingerprintSansRating(once), FingerprintSansRating(w));
            }
        }

        static string FingerprintSansRating(GameWorld w)
        {
            foreach (var p in w.Players.Values)
                if (p.careerStats != null) p.careerStats.averageRating = 0f;
            return SaveGame.Fingerprint(w);
        }

        // ── 직접 뛰는 길 ─────────────────────────────────────────────────

        [Fact]
        public void 직접_뛰어도_새_시즌이_열린다()
        {
            var w = Ready(31415, Kind.Promotion);
            int year = w.SeasonYear;
            var sizes = w.Leagues.Values.ToDictionary(l => l.id, l => l.clubIds.Count);
            int played = 0;

            while (!SeasonRollover.StartNextSeason(w, deferMyPlayoff: true))
            {
                Assert.True(++played < 20);
                var session = GameEngine.BeginPendingMatch(w);
                Assert.NotNull(session);
                while (!session!.Finished) session.AdvanceBlock();

                var result = GameEngine.CompletePendingMatch(w, session);

                // 화면이 그리는 칸 — 비어 있으면 결과 화면이 터진다.
                Assert.NotNull(result!.homeStats);
                Assert.NotNull(result.awayStats);
                Assert.Null(w.pendingMatch);
            }

            Assert.True(played >= 1);
            Assert.Equal(year + 1, w.SeasonYear);
            Assert.Null(w.rollover);
            Assert.All(w.Leagues.Values, l => Assert.Equal(sizes[l.id], l.clubIds.Count));
            Assert.All(w.Leagues.Values, l => Assert.All(l.currentSeason!.fixtures, f => Assert.False(f.isPlayed)));
        }

        [Fact]
        public void 치르지_않은_경기가_있으면_넘어가지_않는다()
        {
            var w = Ready(777, Kind.Champion);
            int year = w.SeasonYear;

            Assert.False(SeasonRollover.StartNextSeason(w, deferMyPlayoff: true));
            var pending = w.pendingMatch;

            // 다시 눌러도, 미루지 않고 눌러도 그 경기를 건너뛰지 않는다.
            Assert.False(SeasonRollover.StartNextSeason(w, deferMyPlayoff: true));
            Assert.False(SeasonRollover.StartNextSeason(w, deferMyPlayoff: false));
            Assert.Same(pending, w.pendingMatch);
            Assert.Equal(year, w.SeasonYear);
        }
    }
}
