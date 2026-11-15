using System.Collections.Generic;
using System.Linq;
using FootballTactician.Data;
using FootballTactician.World;
using Xunit;
using Zone = FootballTactician.World.LeagueZones.Zone;

namespace FT.Core.Tests
{
    /// <summary>
    /// 순위표의 구간 색이 시즌 끝에 실제로 일어나는 일과 맞는지.
    ///
    /// 구간은 화면을 위한 값이지만, 틀리면 감독이 엉뚱한 목표를 보고 시즌을 치른다.
    /// 예전 순위표는 2부 이하에 강등권을 칠하지 않았다 — 3부가 있는 나라의 2부는 내려가는데도.
    /// 그래서 시즌을 끝까지 돌린 뒤, 넘기기 직전의 구간대로 실제로 오르내렸는지 본다.
    /// </summary>
    public class LeagueZonesTests
    {
        // 승격 플레이오프(잉글랜드·이탈리아) · 승강 플레이오프(독일) · 닫힌 리그와 챔피언 결정전(미국) · 스플릿(스코틀랜드)
        static readonly string[] Codes = { "ENG", "GER", "USA", "ITA", "SCO" };

        [Theory]
        [InlineData(777)]
        [InlineData(31415)]
        [InlineData(2024)]
        public void 구간대로_오르내린다(int seed)
        {
            var w = WorldGenerator.Create(seed, nations: Codes.Select(NameForge.ByCode).ToList());
            ManagerCareer.TakeCharge(w, w.Clubs.Values.First(c => c.reputation == 3).id, "테스트");

            for (int guard = 0; guard < 90 && !w.SeasonFinished; guard++)
                GameEngine.PlayNextRound(w);
            SeasonRollover.FinishRemainingFixtures(w);

            // 넘기기 직전의 구간과 소속 단계.
            var before = new Dictionary<int, (Zone zone, int tier, string nation)>();
            foreach (var nation in w.Nations)
                for (int t = 0; t < nation.LeagueIds.Count; t++)
                {
                    var league = w.League(nation.LeagueIds[t])!;
                    if (TwoStage.Has(league.currentSeason)) continue;   // 강등을 한 해 합산 순위로 본다
                    foreach (var e in league.currentSeason!.standings)
                        before[e.clubId] = (LeagueZones.Of(w, league, e.position), t, nation.Code);
                }

            SeasonRollover.StartNextSeason(w);

            int checkedMoves = 0;
            foreach (var (clubId, b) in before)
            {
                var nation = w.Nations.First(n => n.Code == b.nation);
                int now = nation.LeagueIds.IndexOf(w.LeagueOf(clubId)!.id);
                int moved = b.tier - now;        // +1 올라감, -1 내려감

                string who = $"{w.Club(clubId)!.name}({b.nation} {b.tier + 1}부, {b.zone})";
                switch (b.zone)
                {
                    case Zone.Relegation:
                        Assert.True(moved == -1, $"{who} 가 내려가지 않았다");
                        checkedMoves++;
                        break;
                    case Zone.Promotion:
                        Assert.True(moved == 1, $"{who} 가 올라가지 않았다");
                        checkedMoves++;
                        break;
                    case Zone.PromotionPlayoff:
                        Assert.True(moved is 0 or 1, $"{who} 가 엉뚱하게 움직였다");
                        break;
                    case Zone.SurvivalPlayoff:
                        Assert.True(moved is 0 or -1, $"{who} 가 엉뚱하게 움직였다");
                        break;
                    default:
                        Assert.True(moved == 0, $"{who} 는 구간 밖인데 움직였다");
                        break;
                }
            }

            Assert.True(checkedMoves > 10, $"확인한 승강이 {checkedMoves}건뿐이다");
        }

        [Fact]
        public void 범례에는_있는_구간만_나온다()
        {
            var w = WorldGenerator.Create(777, nations: new[] { "ENG", "USA" }.Select(NameForge.ByCode).ToList());
            var eng2 = w.League(w.Nations.First(n => n.Code == "ENG").LeagueIds[1])!;
            var usa1 = w.League(w.Nations.First(n => n.Code == "USA").LeagueIds[0])!;

            var eng = LeagueZones.Present(w, eng2);
            Assert.Contains(Zone.Promotion, eng);
            Assert.Contains(Zone.PromotionPlayoff, eng);
            Assert.Contains(Zone.Relegation, eng);       // 3부가 있으니 2부도 내려간다

            var usa = LeagueZones.Present(w, usa1);
            Assert.DoesNotContain(Zone.Relegation, usa);  // 닫힌 리그
            Assert.Contains(Zone.ChampionPlayoff, usa);
        }
    }
}
