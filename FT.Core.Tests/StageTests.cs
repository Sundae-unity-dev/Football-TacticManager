using System.Collections.Generic;
using System.Linq;
using FootballTactician.Data;
using FootballTactician.World;
using Xunit;

namespace FT.Core.Tests
{
    /// <summary>
    /// 아페르투라 · 클라우수라 — 한 해를 둘로 갈라 각각 우승팀을 뽑는 나라
    /// (docs/리그-제도.md ⑦). 멕시코·우루과이·파라과이·코스타리카·온두라스·과테말라.
    /// </summary>
    public class StageTests
    {
        static GameWorld Start(int seed, params string[] codes)
        {
            var w = WorldGenerator.Create(seed,
                nations: codes.Select(NameForge.ByCode).ToList());
            var club = w.Clubs.Values.First(c => c.reputation == 3);
            ManagerCareer.TakeCharge(w, club.id, "테스트");
            return w;
        }

        static void PlaySeason(GameWorld w)
        {
            int guard = 0;
            while (!w.SeasonFinished && guard++ < 90) GameEngine.PlayNextRound(w);
            SeasonRollover.FinishRemainingFixtures(w);
            Assert.True(w.SeasonFinished, "시즌이 끝나지 않았다");
        }

        static LeagueData League(GameWorld w, string code, int tier = 0)
            => w.League(w.Nations.First(n => n.Code == code).LeagueIds[tier])!;

        [Fact]
        public void 일정의_절반에서_갈린다()
        {
            var w = Start(31337, "MEX");
            var league = League(w, "MEX");
            var season = league.currentSeason!;

            // 멕시코 1부는 열여덟 팀이다 — 34라운드, 경계는 17.
            Assert.Equal(18, league.numberOfTeams);
            Assert.Equal(34, season.totalMatchdays);
            Assert.Equal(17, season.stageMatchday);
        }

        [Fact]
        public void 두_단계가_각각_완전한_한_바퀴다()
        {
            // ⑦ 이 일정을 새로 짜지 않아도 되는 근거다. 전반기에 모든 팀이 서로
            // 한 번씩 만나고, 후반기에 홈·원정을 뒤집어 또 한 번씩 만난다.
            var w = Start(31337, "MEX");
            var league = League(w, "MEX");
            var season = league.currentSeason!;

            foreach (int stage in new[] { TwoStage.Apertura, TwoStage.Clausura })
            {
                var pairs = season.fixtures
                    .Where(f => TwoStage.InStage(season, f, stage))
                    .Select(f => (a: System.Math.Min(f.homeClubId, f.awayClubId),
                                  b: System.Math.Max(f.homeClubId, f.awayClubId)))
                    .ToList();

                int teams = league.numberOfTeams;
                Assert.Equal(teams * (teams - 1) / 2, pairs.Count);
                Assert.Equal(pairs.Count, pairs.Distinct().Count());
            }
        }

        [Fact]
        public void 한_해에_우승팀이_둘_나온다()
        {
            var w = Start(31337, "MEX");
            var league = League(w, "MEX");

            PlaySeason(w);
            var season = league.currentSeason!;

            Assert.True(season.aperturaChampionClubId > 0, "아페르투라 우승팀이 없다");
            Assert.True(ChampionPlayoff.ChampionOf(season) > 0, "클라우수라 우승팀이 없다");
            Assert.Equal(2, TwoStage.ChampionsOf(season).Count);
        }

        [Fact]
        public void 클라우수라는_순위표를_처음부터_다시_센다()
        {
            var w = Start(4242, "MEX");
            var league = League(w, "MEX");
            var season = league.currentSeason!;

            // 경계를 막 넘긴 순간 — 클라우수라 첫 라운드를 치른 직후다.
            int guard = 0;
            while (season.currentMatchday <= season.stageMatchday && guard++ < 90)
                GameEngine.PlayNextRound(w);

            Assert.Equal(TwoStage.Clausura, TwoStage.CurrentStage(season));
            Assert.Equal(season.stageMatchday + 1, season.currentMatchday);

            // 한 라운드만 치렀으니 모든 팀이 한 경기다 — 전반기 17경기가 섞이면 안 된다.
            Assert.All(season.standings, e => Assert.Equal(1, e.played));

            // 합산은 그대로 전부 들고 있다.
            Assert.All(TwoStage.SeasonTable(league), e => Assert.Equal(18, e.played));
        }

        [Fact]
        public void 두_단계를_합치면_한_해다()
        {
            var w = Start(909, "MEX", "URU", "PAR", "CRC");
            PlaySeason(w);

            foreach (var l in w.Leagues.Values)
            {
                if (!TwoStage.Has(l.currentSeason)) continue;

                int a = TwoStage.StageTable(l, TwoStage.Apertura).Sum(e => e.played);
                int c = TwoStage.StageTable(l, TwoStage.Clausura).Sum(e => e.played);
                int all = TwoStage.SeasonTable(l).Sum(e => e.played);

                Assert.Equal(all, a + c);
                Assert.True(a > 0 && c > 0, $"{l.name} 한쪽이 비었다 ({a}, {c})");
            }
        }

        [Fact]
        public void 강등은_한_해_합산으로_정한다()
        {
            // 파라과이는 한 팀이 오르내린다. 내려간 팀은 **합산** 꼴찌여야 한다 —
            // 클라우수라만 못한 팀이 내려가면 안 된다.
            bool sawDifference = false;

            var w = Start(515, "PAR");
            var top = League(w, "PAR", 0);

            for (int s = 0; s < 4; s++)
            {
                PlaySeason(w);

                int aggBottom = TwoStage.SeasonTable(top)
                    .OrderByDescending(e => e.position).First().clubId;
                int stageBottom = top.currentSeason!.standings
                    .OrderByDescending(e => e.position).First().clubId;
                if (aggBottom != stageBottom) sawDifference = true;

                SeasonRollover.StartNextSeason(w);

                Assert.DoesNotContain(aggBottom, top.clubIds);
            }

            Assert.True(sawDifference,
                "합산 꼴찌와 클라우수라 꼴찌가 한 번도 다르지 않았다 — 규칙이 일하는지 알 수 없다");
        }

        [Fact]
        public void 가르지_않는_나라는_그대로다()
        {
            var w = Start(31337, "ENG");
            var league = League(w, "ENG");

            Assert.Equal(0, league.currentSeason!.stageMatchday);
            Assert.False(TwoStage.Has(league.currentSeason));

            PlaySeason(w);

            Assert.Equal(0, league.currentSeason.aperturaChampionClubId);
            Assert.Single(TwoStage.ChampionsOf(league.currentSeason));

            // 합산과 지금 순위표가 같아야 한다.
            Assert.Equal(league.currentSeason.standings.Select(e => e.clubId),
                         TwoStage.SeasonTable(league).Select(e => e.clubId));
        }

        [Fact]
        public void 우승_둘이_모두_구단_기록에_새겨진다()
        {
            var w = Start(2468, "MEX");
            var league = League(w, "MEX");

            PlaySeason(w);
            var season = league.currentSeason!;
            var champs = TwoStage.ChampionsOf(season);
            Assert.Equal(2, champs.Count);

            var before = w.Clubs.Values.ToDictionary(c => c.id, c => c.history.leagueTitles);

            SeasonRollover.StartNextSeason(w);

            foreach (var g in champs.GroupBy(id => id))
                Assert.Equal(before[g.Key] + g.Count(), w.Club(g.Key)!.history.leagueTitles);
        }

        [Fact]
        public void 단계_설정은_세이브를_왕복한다()
        {
            var w = Start(13579, "MEX");
            var league = League(w, "MEX");
            PlaySeason(w);

            var season = league.currentSeason!;
            int boundary = season.stageMatchday, ap = season.aperturaChampionClubId;
            Assert.True(boundary > 0 && ap > 0);

            var back = SaveGame.Deserialize(SaveGame.Serialize(w, "단계")).world;
            var se = back.League(league.id)!.currentSeason!;

            Assert.Equal(boundary, se.stageMatchday);
            Assert.Equal(ap, se.aperturaChampionClubId);
        }

        /// <summary>
        /// 대진 씨드에 해와 단계가 섞여야 한다.
        ///
        /// 섞지 않으면 씨드가 세계·구단·회차만으로 정해져서, 같은 팀들이 다시 만나면
        /// 몇 해가 지나도 똑같은 점수가 나온다. ⑦ 은 한 해에 같은 리그가 대진을 두 번
        /// 치르므로 바로 드러난다.
        /// </summary>
        [Fact]
        public void 같은_대진도_해와_단계가_다르면_결과가_다르다()
        {
            var w = Start(31337, "MEX");
            var league = League(w, "MEX");
            PlaySeason(w);

            var table = TwoStage.SeasonTable(league);

            List<string> Run(int year, int stage)
            {
                league.currentSeason!.year = year;
                var log = new List<string>();
                ChampionPlayoff.ResolveFor(w, league, table, stage, log);
                return log;
            }

            var y1 = Run(2026, TwoStage.Apertura);
            var y2 = Run(2027, TwoStage.Apertura);
            var s2 = Run(2026, TwoStage.Clausura);

            Assert.NotEqual(y1, y2);   // 해가 다르면
            Assert.NotEqual(y1, s2);   // 단계가 달라도
        }
    }
}
