using System;
using System.Collections.Generic;
using System.Linq;
using FootballTactician.Data;
using FootballTactician.World;
using Xunit;

namespace FT.Core.Tests
{
    /// <summary>국내 컵. 대진에서 팀이 사라지거나 우승자가 안 나오면 안 된다.</summary>
    public class CupTests
    {
        static GameWorld Make(int seed = 20261017)
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

        [Fact]
        public void 나라마다_컵이_하나씩_있고_대륙_대회가_하나_있다()
        {
            var w = Make();
            Assert.Equal(w.Nations.Count + 1, w.Cups.Count);

            foreach (var nation in w.Nations)
            {
                var cup = w.Cups.Values.Single(c => !c.continental && c.country == nation.Country);
                int clubs = nation.LeagueIds.Sum(id => w.League(id)!.clubIds.Count);
                Assert.Equal(clubs, cup.entrants.Count);
            }

            var cc = Assert.Single(w.Cups.Values, c => c.continental);
            Assert.Equal(w.Nations.Count * CupCompetition.ContinentalSpotsPerNation, cc.entrants.Count);
        }

        [Fact]
        public void 대륙_대회는_각국_1부_상위_팀만_나간다()
        {
            var w = Make();
            var cc = CupCompetition.ContinentalCup(w)!;

            foreach (int id in cc.entrants)
            {
                Assert.Equal(LeagueTier.First, w.TierOf(id));
                Assert.Equal(id, cc.entrants.Single(x => x == id));   // 중복 없음
            }

            // 나라마다 정해진 수만큼
            foreach (var nation in w.Nations)
            {
                var top = w.League(nation.LeagueIds[0])!;
                int from = cc.entrants.Count(id => top.clubIds.Contains(id));
                Assert.Equal(CupCompetition.ContinentalSpotsPerNation, from);
            }
        }

        [Fact]
        public void 대륙_대회는_국내_컵과_다른_주에_열린다()
        {
            var w = Make();
            var domestic = w.Cups.Values.First(c => !c.continental);
            var continental = CupCompetition.ContinentalCup(w)!;

            Assert.Empty(domestic.roundMatchdays.Intersect(continental.roundMatchdays));
        }

        [Fact]
        public void 대륙_대회도_시즌_안에_우승자가_나온다()
        {
            var w = Make(2244);
            PlaySeason(w);

            var cc = CupCompetition.ContinentalCup(w)!;
            Assert.True(cc.Finished, "대륙 대회가 끝나지 않았다");
            Assert.Contains(cc.winnerClubId, cc.entrants);
        }

        [Fact]
        public void 대륙_대회_목표가_결과대로_판정된다()
        {
            var w = WorldGenerator.Create(3366);
            var cc = CupCompetition.ContinentalCup(w)!;
            var entrant = w.Club(cc.entrants[0])!;
            ManagerCareer.TakeCharge(w, entrant.id, "테스트");

            // 참가 구단이면 대륙 목표를 받는다
            Assert.Contains(w.Objectives, o =>
                o.type is BoardObjectiveType.WinContinentalCup
                       or BoardObjectiveType.ReachContinentalKnockout);

            PlaySeason(w);

            foreach (var o in w.Objectives)
            {
                if (o.type == BoardObjectiveType.WinContinentalCup)
                    Assert.Equal(cc.winnerClubId == entrant.id
                        ? BoardObjectiveResult.Achieved
                        : BoardObjectiveResult.Failed, o.result);

                if (o.type == BoardObjectiveType.ReachContinentalKnockout)
                    Assert.Equal(CupCompetition.ContinentalRoundsWon(cc, entrant.id) >= 1
                        ? BoardObjectiveResult.Achieved
                        : BoardObjectiveResult.Failed, o.result);
            }
        }

        [Fact]
        public void 예선을_치르면_참가_팀이_2의_거듭제곱이_된다()
        {
            var w = Make();
            foreach (var cup in w.Cups.Values)
            {
                // 조별리그로 시작하는 대회는 예선 자체가 없다.
                if (cup.HasGroups) continue;

                int afterPrelim = cup.rounds[0].ties.Count
                                + (cup.rounds[0].ByeClubId > 0 ? 1 : 0)
                                + cup.PendingByes.Count;

                Assert.True((afterPrelim & (afterPrelim - 1)) == 0,
                    $"{cup.name}: 예선 뒤 {afterPrelim}팀 — 2의 거듭제곱이 아니다");
            }
        }

        [Fact]
        public void 대진에서_팀이_사라지지_않는다()
        {
            var w = Make(5150);
            var cup = w.Cups.Values.First();
            var rng = new Random(3);

            int guard = 0;
            while (!cup.Finished && guard++ < 12)
            {
                var round = cup.Current;
                if (round == null) break;

                // 이 라운드에 들어온 팀 수 = 경기 × 2 + 부전승
                int inRound = round.ties.Count * 2 + (round.ByeClubId > 0 ? 1 : 0);
                CupCompetition.PlayRound(w, cup, rng);

                int survivors = round.ties.Count(t => t.WinnerClubId > 0)
                              + (round.ByeClubId > 0 ? 1 : 0);
                Assert.Equal(round.ties.Count + (round.ByeClubId > 0 ? 1 : 0), survivors);
                Assert.True(inRound >= survivors);
            }

            Assert.True(cup.Finished, "컵이 끝나지 않았다");
            Assert.True(cup.winnerClubId > 0);
            Assert.Contains(cup.winnerClubId, cup.entrants);
        }

        [Fact]
        public void 비기면_승부차기로_가른다()
        {
            var w = Make();
            var cup = w.Cups.Values.First();
            var tie = cup.rounds[0].ties[0];

            CupCompetition.ApplyTie(cup, tie, 2, 2, new Random(7));

            Assert.True(tie.played);
            Assert.True(tie.decidedOnPenalties);
            Assert.NotEqual(tie.homePens, tie.awayPens);
            Assert.True(tie.WinnerClubId == tie.homeClubId || tie.WinnerClubId == tie.awayClubId);
            Assert.Equal(tie.homePens > tie.awayPens ? tie.homeClubId : tie.awayClubId, tie.WinnerClubId);
        }

        [Fact]
        public void 시즌을_치르면_모든_컵에_우승자가_나온다()
        {
            var w = Make(777);
            PlaySeason(w);

            foreach (var cup in w.Cups.Values)
            {
                Assert.True(cup.Finished, $"{cup.name} 이 끝나지 않았다");
                Assert.True(cup.winnerClubId > 0);
                Assert.NotNull(w.Club(cup.winnerClubId));
            }
        }

        [Fact]
        public void 시즌을_넘기면_컵이_새로_짜인다()
        {
            var w = Make(888);
            PlaySeason(w);
            int oldYear = w.Cups.Values.First().year;

            SeasonRollover.StartNextSeason(w);

            foreach (var cup in w.Cups.Values)
            {
                Assert.Equal(oldYear + 1, cup.year);
                Assert.False(cup.Finished);
                Assert.Equal(0, cup.currentRound);

                if (cup.HasGroups)
                {
                    // 조별리그 대회는 토너먼트 대진이 아직 없고 조 경기가 모두 비어 있어야 한다.
                    Assert.Empty(cup.rounds);
                    Assert.All(cup.groups.SelectMany(g => g.fixtures), f => Assert.False(f.played));
                    continue;
                }

                Assert.All(cup.rounds[0].ties, t => Assert.False(t.played));
            }
        }

        [Fact]
        public void 컵_우승은_이사회_목표로_인정된다()
        {
            // 컵이 구현됐으므로 목표에서 더 이상 막혀 있으면 안 된다.
            var w = Make(999);
            var top = w.Clubs.Values.First(c => c.reputation >= 4);
            ManagerCareer.TakeCharge(w, top.id, "테스트");

            Assert.Contains(w.Objectives, o => o.type == BoardObjectiveType.WinDomesticCup);
        }

        [Fact]
        public void 컵_경기가_리그_일정을_건드리지_않는다()
        {
            var w = Make(1234);
            var league = w.MyLeague!;
            int fixturesBefore = league.currentSeason.fixtures.Count;

            PlaySeason(w);

            Assert.Equal(fixturesBefore, league.currentSeason.fixtures.Count);
            Assert.All(league.currentSeason.standings,
                e => Assert.Equal((league.clubIds.Count - 1) * 2, e.played));
        }
    }
}
