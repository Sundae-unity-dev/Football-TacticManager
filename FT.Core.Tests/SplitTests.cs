using System;
using System.Collections.Generic;
using System.Linq;
using FootballTactician.Data;
using FootballTactician.World;
using Xunit;

namespace FT.Core.Tests
{
    /// <summary>
    /// 스플릿 — 정규리그 뒤에 상·하위로 갈라 자기들끼리 더 치른다
    /// (docs/리그-제도.md ⑤).
    /// </summary>
    public class SplitTests
    {
        /// <summary>그 나라들로 세계를 만들고 한 시즌을 끝까지 치른다.</summary>
        static GameWorld Season(int seed, params string[] codes)
        {
            var w = WorldGenerator.Create(seed,
                nations: codes.Select(NameForge.ByCode).ToList());

            var club = w.Clubs.Values.First(c => c.reputation == 3);
            ManagerCareer.TakeCharge(w, club.id, "테스트");

            int guard = 0;
            while (!w.SeasonFinished && guard++ < 90) GameEngine.PlayNextRound(w);

            // 리그마다 라운드 수가 다르다. 남은 경기를 마쳐야 모든 순위표가 완성된다.
            SeasonRollover.FinishRemainingFixtures(w);
            return w;
        }

        static LeagueData Top(GameWorld w, string code)
            => w.League(w.Nations.First(n => n.Code == code).LeagueIds[0])!;

        [Fact]
        public void 정규리그가_끝나면_상하위로_갈린다()
        {
            // 스코틀랜드 1부는 열두 팀이다 — 22라운드를 치른 뒤 여섯씩 갈려
            // 다섯 경기를 더 치르므로 27라운드가 된다.
            var w = Season(31337, "SCO");
            var league = Top(w, "SCO");
            var season = league.currentSeason!;

            Assert.Equal(12, league.numberOfTeams);
            Assert.Equal(22, season.splitMatchday);
            Assert.Equal(27, season.totalMatchdays);

            Assert.Equal(6, season.splitGroups.Count(g => g.Value == 0));
            Assert.Equal(6, season.splitGroups.Count(g => g.Value == 1));
        }

        [Fact]
        public void 갈린_뒤에는_같은_조끼리만_치른다()
        {
            var w = Season(31337, "SCO", "KOR", "AUT");

            foreach (var league in w.Leagues.Values)
            {
                var season = league.currentSeason!;
                if (season.splitMatchday == 0) continue;

                var crossed = season.fixtures
                    .Where(f => f.matchday > season.splitMatchday)
                    .Where(f => LeagueSplit.GroupOf(season, f.homeClubId)
                                != LeagueSplit.GroupOf(season, f.awayClubId))
                    .ToList();

                Assert.Empty(crossed);
            }
        }

        [Fact]
        public void 하위_조는_상위_조를_넘어서지_못한다()
        {
            // 스플릿의 핵심이다. 하위 조 1위가 상위 조 꼴찌보다 승점이 많아도
            // 순위는 아래여야 한다 — 그래서 정규리그 막판이 팽팽해진다.
            var w = Season(31337, "SCO", "KOR", "AUT", "BEL");

            bool sawInversion = false;

            foreach (var league in w.Leagues.Values)
            {
                var season = league.currentSeason!;
                if (season.splitMatchday == 0) continue;

                var order = season.standings.OrderBy(e => e.position).ToList();

                // 상위 조가 먼저, 하위 조가 나중 — 섞이면 안 된다.
                int firstLower = order.FindIndex(e => LeagueSplit.GroupOf(season, e.clubId) == 1);
                Assert.True(firstLower > 0, "하위 조가 하나도 없다");
                Assert.All(order.Take(firstLower),
                           e => Assert.Equal(0, LeagueSplit.GroupOf(season, e.clubId)));
                Assert.All(order.Skip(firstLower),
                           e => Assert.Equal(1, LeagueSplit.GroupOf(season, e.clubId)));

                int worstUpper = order.Take(firstLower).Min(e => e.points);
                int bestLower = order.Skip(firstLower).Max(e => e.points);
                if (bestLower > worstUpper) sawInversion = true;
            }

            Assert.True(sawInversion,
                "승점이 뒤집힌 경우가 한 번도 없었다 — 경계 규칙이 일하는지 알 수 없다");
        }

        [Fact]
        public void 승점_절반은_갈릴_때_깎인다()
        {
            // 오스트리아는 갈라질 때 승점을 절반(올림)으로 줄인다.
            var w = WorldGenerator.Create(4242,
                nations: new[] { NameForge.ByCode("AUT") });
            var club = w.Clubs.Values.First(c => c.reputation == 3);
            ManagerCareer.TakeCharge(w, club.id, "테스트");

            var league = Top(w, "AUT");
            int regular = WorldGenerator.Matchdays(league.numberOfTeams);

            // 갈리기 직전까지만 돌린다.
            int guard = 0;
            while (league.currentSeason!.currentMatchday < regular && guard++ < 90)
                GameEngine.PlayNextRound(w);

            var season = league.currentSeason!;
            Assert.True(season.splitMatchday > 0, "정규리그를 다 치렀는데 갈리지 않았다");
            Assert.NotNull(season.pointsCarry);

            // 보정은 "절반(올림) - 원래" 이므로 0 이하이고, 되돌리면 원래 승점이 나온다.
            foreach (var e in season.standings)
            {
                int carry = season.pointsCarry[e.clubId];
                int before = e.points - carry;

                Assert.True(carry <= 0, $"승점이 늘었다 ({carry})");
                Assert.Equal((before + 1) / 2, e.points);
            }
        }

        [Fact]
        public void 스플릿이_없는_리그는_그대로다()
        {
            var w = Season(31337, "ENG");
            var season = Top(w, "ENG").currentSeason!;

            Assert.Equal(0, season.splitMatchday);
            Assert.Null(season.splitGroups);
            Assert.Equal(38, season.totalMatchdays);
        }

        [Fact]
        public void 다음_시즌에는_스플릿이_지워진다()
        {
            var w = Season(515, "SCO");
            var league = Top(w, "SCO");

            Assert.True(league.currentSeason!.splitMatchday > 0);

            SeasonRollover.StartNextSeason(w);

            var season = league.currentSeason!;
            Assert.Equal(0, season.splitMatchday);
            Assert.Null(season.splitGroups);
            Assert.Null(season.pointsCarry);
            Assert.Equal(22, season.totalMatchdays);   // 다시 정규리그부터
        }

        [Fact]
        public void 스플릿_리그도_여러_시즌을_넘긴다()
        {
            // 라운드 수가 시즌마다 늘었다 줄었다 하므로, 승강과 일정이 어긋나기 쉽다.
            var w = Season(909, "SCO", "AUT", "BEL");
            var sizes = w.Leagues.Values.ToDictionary(l => l.id, l => l.clubIds.Count);

            for (int s = 0; s < 3; s++)
            {
                SeasonRollover.StartNextSeason(w);

                int guard = 0;
                while (!w.SeasonFinished && guard++ < 90) GameEngine.PlayNextRound(w);
                SeasonRollover.FinishRemainingFixtures(w);

                foreach (var league in w.Leagues.Values)
                {
                    var season = league.currentSeason!;
                    Assert.True(season.Finished, $"{league.name} 의 시즌이 끝나지 않았다");
                    Assert.True(season.splitMatchday > 0, $"{league.name} 이 갈리지 않았다");
                }
            }

            foreach (var (id, n) in sizes)
                Assert.Equal(n, w.League(id)!.clubIds.Count);
        }
    }
}
