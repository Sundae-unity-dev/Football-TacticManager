using System.Collections.Generic;
using System.Linq;
using FootballTactician.Data;
using FootballTactician.World;
using Xunit;

namespace FT.Core.Tests
{
    /// <summary>
    /// 챔피언 결정 플레이오프 — 정규리그 1위가 곧 우승팀이 아닌 나라들
    /// (docs/리그-제도.md ⑥). 미국·호주·멕시코·인도·인도네시아.
    /// </summary>
    public class ChampionTests
    {
        static GameWorld Start(int seed, params string[] codes)
        {
            var w = WorldGenerator.Create(seed,
                nations: codes.Select(NameForge.ByCode).ToList());
            var club = w.Clubs.Values.First(c => c.reputation == 3);
            ManagerCareer.TakeCharge(w, club.id, "테스트");
            return w;
        }

        /// <summary>한 시즌을 끝까지 치른다. 플레이오프는 아직 치르지 않은 상태다.</summary>
        static void PlaySeason(GameWorld w)
        {
            int guard = 0;
            while (!w.SeasonFinished && guard++ < 90) GameEngine.PlayNextRound(w);
            SeasonRollover.FinishRemainingFixtures(w);
            Assert.True(w.SeasonFinished, "시즌이 끝나지 않았다");
        }

        static LeagueData League(GameWorld w, string code, int tier = 0)
            => w.League(w.Nations.First(n => n.Code == code).LeagueIds[tier])!;

        static int TopOfTable(LeagueData l)
            => l.currentSeason!.standings.OrderBy(e => e.position).First().clubId;

        [Fact]
        public void 플레이오프가_없는_나라는_정규리그_1위가_우승이다()
        {
            var w = Start(31337, "ENG");
            var league = League(w, "ENG");

            PlaySeason(w);
            int top = TopOfTable(league);

            SeasonRollover.StartNextSeason(w);

            Assert.Equal(top, league.lastChampionClubId);
        }

        [Fact]
        public void 우승팀은_언제나_정규리그_상위권_안에서_나온다()
        {
            // 뒤집히는 일이 실제로 있어야 규칙이 일하는지 알 수 있다.
            bool sawUpset = false;

            var w = Start(31337, "USA", "MEX", "AUS", "IND");

            for (int s = 0; s < 6; s++)
            {
                PlaySeason(w);

                // 플레이오프는 StartNextSeason 안에서 치러진다. 그 전에 적어 둔다.
                var before = w.Leagues.Values.ToDictionary(
                    l => l.id,
                    l => l.currentSeason!.standings.OrderBy(e => e.position)
                          .Select(e => e.clubId).ToList());

                SeasonRollover.StartNextSeason(w);

                foreach (var l in w.Leagues.Values)
                {
                    int entrants = ChampionPlayoff.EntrantsFor(w, l);
                    if (entrants < 2) continue;

                    int champ = l.lastChampionClubId;
                    Assert.True(champ > 0, $"{l.name} 의 우승팀이 없다");

                    // 우승은 반드시 정규리그 상위 entrants 팀 중 하나여야 한다.
                    Assert.Contains(champ, before[l.id].Take(entrants));

                    if (champ != before[l.id][0]) sawUpset = true;
                }
            }

            Assert.True(sawUpset,
                "여섯 시즌 동안 정규리그 1위가 한 번도 안 놓쳤다 — 규칙이 일하는지 알 수 없다");
        }

        [Fact]
        public void 챔피언_결정전은_순위표를_건드리지_않는다()
        {
            var w = Start(4242, "USA", "ENG");
            PlaySeason(w);

            var before = w.Leagues.Values.ToDictionary(
                l => l.id,
                l => l.currentSeason!.standings
                      .OrderBy(e => e.position)
                      .Select(e => (e.clubId, e.position, e.points)).ToList());

            var log = new List<string>();
            foreach (var l in w.Leagues.Values) ChampionPlayoff.Resolve(w, l, log);

            foreach (var l in w.Leagues.Values)
            {
                var after = l.currentSeason!.standings
                    .OrderBy(e => e.position)
                    .Select(e => (e.clubId, e.position, e.points)).ToList();

                Assert.Equal(before[l.id], after);
            }
        }

        [Fact]
        public void 닫힌_리그는_플레이오프를_치러도_팀이_오가지_않는다()
        {
            var w = Start(909, "USA");
            var sizes = w.Leagues.Values.ToDictionary(l => l.id, l => l.clubIds.Count);
            var members = w.Leagues.Values.ToDictionary(
                l => l.id, l => l.clubIds.OrderBy(x => x).ToList());

            PlaySeason(w);
            SeasonRollover.StartNextSeason(w);

            foreach (var l in w.Leagues.Values)
            {
                Assert.Equal(sizes[l.id], l.clubIds.Count);
                Assert.Equal(members[l.id], l.clubIds.OrderBy(x => x).ToList());
            }
        }

        [Fact]
        public void 우승은_구단_기록에_새겨진다()
        {
            var w = Start(515, "USA");
            var league = League(w, "USA");

            PlaySeason(w);
            int top = TopOfTable(league);

            var before = w.Clubs.Values.ToDictionary(c => c.id, c => c.history.leagueTitles);

            SeasonRollover.StartNextSeason(w);

            int champ = league.lastChampionClubId;
            Assert.True(champ > 0);

            // 기록이 오른 쪽은 플레이오프 우승팀이지 정규리그 1위가 아니다.
            Assert.Equal(before[champ] + 1, w.Club(champ)!.history.leagueTitles);
            if (champ != top)
                Assert.Equal(before[top], w.Club(top)!.history.leagueTitles);
        }

        [Fact]
        public void 참가_팀이_모자라면_치르지_않는다()
        {
            var w = Start(2468, "ENG");
            var league = League(w, "ENG");
            PlaySeason(w);

            // 잉글랜드에는 애초에 없다.
            var log = new List<string>();

            Assert.Equal(0, ChampionPlayoff.EntrantsFor(w, league));
            Assert.Equal(0, ChampionPlayoff.Resolve(w, league, log));
            Assert.Empty(log);
            Assert.Equal(0, league.currentSeason!.championClubId);
        }

        [Fact]
        public void 우승을_묻는_길은_한_곳이다()
        {
            var w = Start(13579, "ENG");
            PlaySeason(w);

            var season = League(w, "ENG").currentSeason!;
            int top = season.standings.OrderBy(e => e.position).First().clubId;

            // championClubId 가 0 이면(예전 세이브, 플레이오프 없는 리그) 1위가 우승이다.
            Assert.Equal(0, season.championClubId);
            Assert.Equal(top, ChampionPlayoff.ChampionOf(season));
            Assert.False(ChampionPlayoff.WonFromBehind(season));

            // 플레이오프 우승팀이 적히면 그쪽이 우승이다.
            int other = season.standings.OrderBy(e => e.position).Last().clubId;
            season.championClubId = other;

            Assert.Equal(other, ChampionPlayoff.ChampionOf(season));
            Assert.True(ChampionPlayoff.WonFromBehind(season));
        }

        /// <summary>
        /// 플레이오프 골은 득점왕에 들어가면 안 된다.
        ///
        /// 일정에 없는 경기도 득점과 평점을 선수 기록에 남긴다. 그래서 챔피언 결정전을
        /// 시상보다 먼저 치르면 득점왕이 플레이오프 골로 뒤집힌다 — 실제로는 정규리그만
        /// 센다. 순서 하나에 달린 규칙이라, 나중에 줄을 옮기면 조용히 깨진다.
        /// </summary>
        /// <summary>
        /// 먼저 전제를 확인한다 — <b>플레이오프 골은 실제로 선수 기록에 쌓인다.</b>
        /// 이게 참이라서 시상 순서가 문제가 된다. 거짓이면 아래 테스트는 아무것도 지키지 않는다.
        /// </summary>
        [Fact]
        public void 플레이오프_골은_선수_기록에_쌓인다()
        {
            var w = Start(31337, "USA");
            var league = League(w, "USA");
            PlaySeason(w);

            int Goals() => league.clubIds.SelectMany(id => w.Squad(id))
                                 .Sum(p => p.seasonStats?.goals ?? 0);

            int before = Goals();
            ChampionPlayoff.Resolve(w, league, new List<string>());

            Assert.True(Goals() > before,
                $"플레이오프를 치렀는데 득점이 그대로다 ({before})");
        }

        /// <summary>
        /// 플레이오프 골은 <b>득점왕에는</b> 들어가면 안 된다. 실제로도 정규리그만 센다.
        ///
        /// 챔피언 결정전을 시상보다 먼저 치르면 깨진다. 한 시드만 보면 그 해 득점왕이
        /// 플레이오프에서 안 넣어 통과해 버리므로 여러 시드로 본다.
        /// </summary>
        [Fact]
        public void 플레이오프_골은_득점왕에_들어가지_않는다()
        {
            foreach (int seed in new[] { 31337, 4242, 909, 515, 2468, 13579, 24680 })
            {
                var w = Start(seed, "USA");
                var league = League(w, "USA");

                PlaySeason(w);
                int year = league.currentSeason!.year;

                int regularGoals = league.clubIds.SelectMany(id => w.Squad(id))
                    .Max(p => p.seasonStats?.goals ?? 0);

                SeasonRollover.StartNextSeason(w);

                var set = w.Awards.First(a => a.leagueId == league.id && a.year == year);

                Assert.True(set.topScorerGoals > 0, $"시드 {seed}: 득점왕이 0골이다");
                Assert.Equal(regularGoals, set.topScorerGoals);
            }
        }

        [Fact]
        public void 우승_기록은_세이브를_왕복한다()
        {
            var w = Start(24680, "USA");
            var league = League(w, "USA");

            PlaySeason(w);
            SeasonRollover.StartNextSeason(w);

            int champ = league.lastChampionClubId;
            int year = league.lastChampionYear;
            Assert.True(champ > 0 && year > 0);

            var back = SaveGame.Deserialize(SaveGame.Serialize(w, "우승")).world;

            Assert.Equal(champ, back.League(league.id)!.lastChampionClubId);
            Assert.Equal(year, back.League(league.id)!.lastChampionYear);
        }
    }
}
