using System;
using System.Collections.Generic;
using System.Linq;
using FootballTactician.Balance;
using FootballTactician.Data;
using FootballTactician.World;
using Xunit;

namespace FT.Core.Tests
{
    /// <summary>
    /// 리그 피라미드와 대륙 대회 조별리그.
    /// 3부가 갇히거나 같은 나라끼리 한 조에 묶이면 안 된다.
    /// </summary>
    public class PyramidTests
    {
        static GameWorld Make(int seed = 31415)
        {
            var w = WorldGenerator.Create(seed);
            var club = w.Clubs.Values.First(c => c.reputation == 4);
            ManagerCareer.TakeCharge(w, club.id, "테스트");
            return w;
        }

        static void PlaySeason(GameWorld w)
        {
            int guard = 0;
            while (!w.SeasonFinished && guard++ < 60)
                if (GameEngine.PlayNextRound(w) == null) break;
        }

        // ── 피라미드 ──────────────────────────────────────────────────────

        [Theory]
        [InlineData(31415)]
        [InlineData(606)]
        [InlineData(90210)]
        public void 모든_단계가_위아래로_이어진다(int seed)
        {
            var w = WorldGenerator.Create(seed);

            foreach (var nation in w.Nations)
            {
                Assert.Equal(WorldGenerator.Tiers, nation.LeagueIds.Count);

                for (int t = 0; t + 1 < nation.LeagueIds.Count; t++)
                {
                    var upper = w.League(nation.LeagueIds[t])!;
                    var lower = w.League(nation.LeagueIds[t + 1])!;

                    Assert.Equal(lower.id, upper.relegationLeagueId);
                    Assert.Equal(upper.id, lower.promotionLeagueId);
                }

                // 맨 아래는 내려갈 곳이 없다.
                Assert.Equal(0, w.League(nation.LeagueIds[^1])!.relegationSpots);
                Assert.Equal(0, w.League(nation.LeagueIds[0])!.promotionSpots);
            }
        }

        [Fact]
        public void 아래_단계일수록_약하다()
        {
            var w = WorldGenerator.Create(31415);

            foreach (var nation in w.Nations)
            {
                var strengths = nation.LeagueIds
                    .Select(id => w.League(id)!.clubIds.Average(c => w.Strength(c)))
                    .ToList();

                for (int t = 0; t + 1 < strengths.Count; t++)
                    Assert.True(strengths[t] > strengths[t + 1],
                        $"{nation.Country} {t + 1}부({strengths[t]:F0}) ≤ {t + 2}부({strengths[t + 1]:F0})");
            }
        }

        [Fact]
        public void 맨_아래_단계도_승격할_수_있다()
        {
            // 1·2부만 처리하면 3부는 영영 갇힌다.
            var w = Make(606);
            var nation = w.Nations[0];
            var third = w.League(nation.LeagueIds[2])!;
            var before = third.clubIds.ToHashSet();

            PlaySeason(w);
            SeasonRollover.StartNextSeason(w);

            var after = third.clubIds.ToHashSet();
            Assert.NotEqual(before, after);

            // 올라간 팀은 2부에 있어야 한다.
            var second = w.League(nation.LeagueIds[1])!;
            var promoted = before.Except(after).ToList();
            Assert.NotEmpty(promoted);
            Assert.All(promoted, id => Assert.Contains(id, second.clubIds));
        }

        [Fact]
        public void 한_번에_두_단계를_건너뛰지_않는다()
        {
            var w = Make(606);
            var nation = w.Nations[0];
            var tierOf = w.Clubs.Values.ToDictionary(c => c.id, c => (int)w.TierOf(c.id));

            PlaySeason(w);
            SeasonRollover.StartNextSeason(w);

            foreach (var club in w.Clubs.Values)
            {
                if (!tierOf.TryGetValue(club.id, out int was)) continue;
                int now = (int)w.TierOf(club.id);
                Assert.True(Math.Abs(now - was) <= 1,
                    $"{club.name} 이 {was}부에서 {now}부로 옮겨졌다");
            }
        }

        [Fact]
        public void 모든_리그의_팀_수가_유지된다()
        {
            // 승강이 어긋나면 한 리그가 19팀, 다른 리그가 21팀이 된다.
            var w = Make(606);

            for (int s = 0; s < 3; s++)
            {
                PlaySeason(w);
                SeasonRollover.StartNextSeason(w);

                foreach (var league in w.Leagues.Values)
                    Assert.Equal(WorldGenerator.ClubsPerLeague, league.clubIds.Count);
            }
        }

        [Fact]
        public void 국내_컵에_모든_단계가_참가한다()
        {
            var w = Make();
            var cup = CupCompetition.UserCup(w)!;
            var nation = w.NationOf(w.ManagedClubId)!;

            int expected = nation.LeagueIds.Sum(id => w.League(id)!.clubIds.Count);
            Assert.Equal(expected, cup.entrants.Count);

            // 3부 팀도 들어 있어야 한다.
            var third = w.League(nation.LeagueIds[^1])!;
            Assert.Contains(third.clubIds[0], cup.entrants);
        }

        // ── 대륙 대회 조별리그 ─────────────────────────────────────────────

        [Fact]
        public void 대륙_대회는_조별리그로_시작한다()
        {
            var w = Make();
            var cc = CupCompetition.ContinentalCup(w)!;

            Assert.True(cc.HasGroups);
            Assert.Equal(CupCompetition.GroupCount, cc.groups.Count);
            Assert.All(cc.groups, g => Assert.Equal(CupCompetition.GroupSize, g.clubIds.Count));

            // 토너먼트 대진은 조별리그가 끝나야 생긴다.
            Assert.Empty(cc.rounds);
        }

        [Fact]
        public void 한_조에_같은_나라가_둘_들어가지_않는다()
        {
            // 리그에서 매주 만나는 팀을 대륙 대회에서 또 만나면 대회의 의미가 없다.
            foreach (int seed in new[] { 31415, 606, 90210, 777 })
            {
                var w = WorldGenerator.Create(seed);
                var cc = CupCompetition.ContinentalCup(w)!;

                foreach (var g in cc.groups)
                {
                    var countries = g.clubIds.Select(id => w.NationOf(id)?.Country).ToList();
                    Assert.Equal(countries.Count, countries.Distinct().Count());
                }
            }
        }

        [Fact]
        public void 조별리그_일정이_서로_겹치지_않는다()
        {
            var w = Make();
            var cc = CupCompetition.ContinentalCup(w)!;

            foreach (var g in cc.groups)
            {
                // 네 팀이면 여섯 경기, 각 팀이 세 번씩 뛴다.
                Assert.Equal(6, g.fixtures.Count);
                foreach (int id in g.clubIds)
                    Assert.Equal(3, g.fixtures.Count(f => f.homeClubId == id || f.awayClubId == id));

                // 한 주차에 같은 팀이 두 경기를 뛰면 안 된다.
                foreach (var day in g.fixtures.GroupBy(f => f.matchday))
                {
                    var ids = day.SelectMany(f => new[] { f.homeClubId, f.awayClubId }).ToList();
                    Assert.Equal(ids.Count, ids.Distinct().Count());
                }
            }

            // 조별리그와 토너먼트 주차가 겹치면 같은 주에 두 경기를 치른다.
            Assert.Empty(CupCompetition.GroupMatchdays
                .Intersect(CupCompetition.ContinentalKnockoutMatchdays));
        }

        [Fact]
        public void 조별리그를_치르면_토너먼트가_짜인다()
        {
            var w = Make(2244);
            PlaySeason(w);

            var cc = CupCompetition.ContinentalCup(w)!;
            Assert.True(cc.GroupsDone);
            Assert.NotEmpty(cc.rounds);

            // 조마다 두 팀씩 = 8팀이 토너먼트에 오른다.
            int expected = CupCompetition.GroupCount * CupCompetition.QualifyPerGroup;
            var inKnockout = cc.rounds[0].ties
                .SelectMany(t => new[] { t.homeClubId, t.awayClubId })
                .ToList();
            Assert.Equal(expected, inKnockout.Count);
            Assert.Equal(expected, inKnockout.Distinct().Count());

            // 오른 팀은 모두 조 상위권이어야 한다.
            foreach (int id in inKnockout)
            {
                var group = cc.groups.First(g => g.clubIds.Contains(id));
                Assert.True(group.Standing().IndexOf(id) < CupCompetition.QualifyPerGroup,
                    $"{w.Club(id)?.name} 이 조 하위인데 토너먼트에 올랐다");
            }
        }

        [Fact]
        public void 조_순위는_승점_득실_순이다()
        {
            var group = new CupGroup { clubIds = { 1, 2, 3, 4 } };
            void Play(int h, int a, int hg, int ag) =>
                group.fixtures.Add(new CupTie
                {
                    homeClubId = h, awayClubId = a, played = true, homeGoals = hg, awayGoals = ag,
                });

            Play(1, 2, 1, 0);   // 1번 3점
            Play(3, 4, 5, 0);   // 3번 3점 (+5)
            Play(2, 3, 0, 0);   // 둘 다 1점
            Play(4, 1, 0, 1);   // 1번 6점

            var standing = group.Standing();
            Assert.Equal(1, standing[0]);           // 6점
            Assert.Equal(3, standing[1]);           // 4점
            Assert.Equal(4, standing[^1]);          // 0점, -6

            Assert.Equal((6, 2, 0), group.Record(1));
        }

        [Fact]
        public void 대륙_대회도_시즌_안에_우승자가_나온다()
        {
            var w = Make(2244);
            PlaySeason(w);

            var cc = CupCompetition.ContinentalCup(w)!;
            Assert.True(cc.Finished, "조별리그를 거친 대륙 대회가 끝나지 않았다");
            Assert.Contains(cc.winnerClubId, cc.entrants);
        }

        [Fact]
        public void 조별리그_통과도_한_단계로_센다()
        {
            // 세지 않으면 "본선 진출" 목표가 8강을 이겨야만 달성된다.
            var w = Make(2244);
            PlaySeason(w);

            var cc = CupCompetition.ContinentalCup(w)!;
            foreach (var g in cc.groups)
            {
                var standing = g.Standing();
                Assert.True(CupCompetition.ContinentalRoundsWon(cc, standing[0]) >= 1);
                Assert.Equal(0, CupCompetition.ContinentalRoundsWon(cc, standing[^1]));
            }
        }

        [Fact]
        public void 조별리그_경기가_세이브를_왕복한다()
        {
            var w = Make();
            var cc = CupCompetition.ContinentalCup(w)!;
            var first = cc.groups[0].fixtures[0];
            CupCompetition.ApplyGroupResult(first, 3, 1);

            var back = SaveGame.Deserialize(SaveGame.Serialize(w, "조별리그")).world!;
            var loaded = CupCompetition.ContinentalCup(back)!;

            Assert.Equal(cc.groups.Count, loaded.groups.Count);
            Assert.Equal(cc.groups[0].clubIds, loaded.groups[0].clubIds);

            var tie = loaded.groups[0].fixtures[0];
            Assert.True(tie.played);
            Assert.Equal(3, tie.homeGoals);
            Assert.Equal(1, tie.awayGoals);
            Assert.Equal(first.matchday, tie.matchday);
        }
    }
}
