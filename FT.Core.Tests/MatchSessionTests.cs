using System;
using System.Collections.Generic;
using System.Linq;
using FootballTactician.Data;
using FootballTactician.Simulation;
using FootballTactician.World;
using Xunit;

namespace FT.Core.Tests
{
    /// <summary>
    /// 재개 가능한 경기. 화면에서 한 블록씩 진행하는 경로와 한 번에 돌리는 경로가
    /// 같은 결과를 내야 한다.
    /// </summary>
    public class MatchSessionTests
    {
        static (GameWorld w, MatchSimContext ctx) Fixture(int seed = 5150)
        {
            var w = WorldGenerator.Create(seed);
            var league = w.League(w.Nations[0].LeagueId)!;
            var f = league.currentSeason.fixtures.First();

            var ctx = new MatchSimContext
            {
                fixtureId = f.id,
                home = MatchDay.BuildLineup(w, f.homeClubId)!,
                away = MatchDay.BuildLineup(w, f.awayClubId)!,
                seed = 12345,
            };
            return (w, ctx);
        }

        [Fact]
        public void 블록으로_나눠_진행해도_한번에_돌린_것과_같다()
        {
            var (w1, ctx1) = Fixture();
            var whole = MatchEngine.Simulate(ctx1, w1.Lookup);

            var (w2, ctx2) = Fixture();
            var session = MatchSession.Begin(ctx2, w2.Lookup);
            while (!session.Finished) session.AdvanceBlock();
            var stepped = session.Finish();

            Assert.Equal(whole.homeGoals, stepped.homeGoals);
            Assert.Equal(whole.awayGoals, stepped.awayGoals);
            Assert.Equal(whole.homeStats.shots, stepped.homeStats.shots);
            Assert.Equal(whole.awayStats.shots, stepped.awayStats.shots);
            Assert.Equal(whole.homeStats.possession, stepped.homeStats.possession);
            Assert.Equal(whole.events.Count, stepped.events.Count);
        }

        [Fact]
        public void 직접_치른_경기와_자동_진행이_같은_결과를_낸다()
        {
            // 개입하지 않고 직접 치르면 자동 진행과 결과가 같아야 한다.
            // 다르면 화면에서 치른 경기만 밸런스가 어긋난다.
            var a = WorldGenerator.Create(60606);
            var clubA = a.Clubs.Values.First(c => c.reputation == 3);
            ManagerCareer.TakeCharge(a, clubA.id, "자동");
            var autoResult = GameEngine.PlayNextRound(a);

            var b = WorldGenerator.Create(60606);
            var clubB = b.Clubs.Values.First(c => c.reputation == 3);
            ManagerCareer.TakeCharge(b, clubB.id, "직접");
            var fixture = GameEngine.NextUserFixture(b)!;
            var session = GameEngine.BeginUserMatch(b, fixture)!;
            session.AutoSubstitutions = (true, true);     // 자동 진행과 같은 조건
            while (!session.Finished) session.AdvanceBlock();
            var liveResult = GameEngine.CompleteUserMatch(b, fixture, session);

            Assert.Equal(autoResult!.homeGoals, liveResult.homeGoals);
            Assert.Equal(autoResult.awayGoals, liveResult.awayGoals);
            Assert.Equal(autoResult.homeStats.shots, liveResult.homeStats.shots);

            // 같은 라운드의 다른 경기와 순위표도 똑같이 처리되어야 한다
            Assert.Equal(
                a.MyLeague!.currentSeason.fixtures.Count(f => f.isPlayed),
                b.MyLeague!.currentSeason.fixtures.Count(f => f.isPlayed));
            Assert.Equal(
                a.MyLeague.currentSeason.standings.Select(s => s.points),
                b.MyLeague.currentSeason.standings.Select(s => s.points));
            Assert.Equal(a.Date, b.Date);
        }

        [Fact]
        public void 직접_치른_경기도_선수_기록에_반영된다()
        {
            var w = WorldGenerator.Create(70707);
            var club = w.Clubs.Values.First(c => c.reputation == 4);
            ManagerCareer.TakeCharge(w, club.id, "테스트");

            var fixture = GameEngine.NextUserFixture(w)!;
            var session = GameEngine.BeginUserMatch(w, fixture)!;
            GameEngine.CompleteUserMatch(w, fixture, session);

            var played = w.Squad(club.id).Where(p => p.seasonStats.appearances > 0).ToList();
            Assert.Equal(11, played.Count);
            Assert.All(played, p => Assert.True(p.seasonStats.averageRating > 0));
            Assert.True(fixture.isPlayed);
        }

        [Fact]
        public void 하프타임에서_멈춘다()
        {
            var (w, ctx) = Fixture();
            var s = MatchSession.Begin(ctx, w.Lookup);

            Assert.True(s.AtKickOff);
            Assert.Equal(0, s.Minute);

            s.AdvanceToHalfTime();
            Assert.True(s.AtHalfTime);
            Assert.Equal(45, s.Minute);
            Assert.False(s.Finished);

            s.AdvanceToEnd();
            Assert.True(s.Finished);
            Assert.Equal(100, s.Minute);      // 90분 + 추가 시간 2블록
        }

        [Fact]
        public void 교체는_선발과_벤치를_맞바꾼다()
        {
            var (w, ctx) = Fixture();
            var s = MatchSession.Begin(ctx, w.Lookup);
            s.AdvanceToHalfTime();

            var onPitch = s.OnPitch(true).First(x => (x.slot.position & Position.GK) == 0);
            var bench = s.AvailableBench(true).First(p => (p.positionFlags & Position.GK) == 0);

            int before = s.SubsLeft(true);
            var result = s.Substitute(true, onPitch.player.id, bench.id);

            Assert.Equal(MatchSession.SubResult.Ok, result);
            Assert.Equal(before - 1, s.SubsLeft(true));
            Assert.Contains(s.OnPitch(true), x => x.player.id == bench.id);
            Assert.DoesNotContain(s.OnPitch(true), x => x.player.id == onPitch.player.id);

            // 교체되어 나간 선수는 다시 들어올 수 없다
            Assert.DoesNotContain(s.AvailableBench(true), p => p.id == onPitch.player.id);

            Assert.Contains(s.Events, e => e.eventType == MatchEventType.Substitution
                                        && e.playerId == onPitch.player.id
                                        && e.assistPlayerId == bench.id);
        }

        [Fact]
        public void 교체_한도를_넘기지_못한다()
        {
            var (w, ctx) = Fixture();
            var s = MatchSession.Begin(ctx, w.Lookup);
            s.AdvanceToHalfTime();

            int limit = ctx.rules.maxSubstitutions;
            int made = 0;

            for (int i = 0; i < limit + 3; i++)
            {
                var onPitch = s.OnPitch(true)
                    .FirstOrDefault(x => (x.slot.position & Position.GK) == 0);
                var bench = s.AvailableBench(true)
                    .FirstOrDefault(p => (p.positionFlags & Position.GK) == 0);
                if (onPitch.player == null || bench == null) break;

                var r = s.Substitute(true, onPitch.player.id, bench.id);
                if (r == MatchSession.SubResult.Ok) made++;
                else { Assert.Equal(MatchSession.SubResult.NoSubsLeft, r); break; }
            }

            Assert.Equal(limit, made);
            Assert.Equal(0, s.SubsLeft(true));
        }

        [Fact]
        public void 벤치에_없는_선수는_넣을_수_없다()
        {
            var (w, ctx) = Fixture();
            var s = MatchSession.Begin(ctx, w.Lookup);

            var onPitch = s.OnPitch(true).First();
            var otherClubPlayer = w.Squad(ctx.away.clubId).First();

            Assert.Equal(MatchSession.SubResult.NotOnBench,
                s.Substitute(true, onPitch.player.id, otherClubPlayer.id));
            Assert.Equal(MatchSession.SubResult.NotOnPitch,
                s.Substitute(true, otherClubPlayer.id, s.AvailableBench(true).First().id));
        }

        [Fact]
        public void 멘탈리티를_바꾸면_공격_전력이_달라진다()
        {
            var (w, ctx) = Fixture();
            var s = MatchSession.Begin(ctx, w.Lookup);

            var before = MatchEngine.TeamPower(ctx.home, w.Lookup);
            s.SetMentality(true, TeamMentality.VeryAttacking);
            var after = MatchEngine.TeamPower(ctx.home, w.Lookup);

            Assert.True(after.attack > before.attack,
                $"공격적으로 바꿨는데 공격 전력이 오르지 않았다 {before.attack:F2} → {after.attack:F2}");
            Assert.True(after.defense < before.defense);
        }

        [Fact]
        public void 자동_교체가_지친_선수를_바꾼다()
        {
            var (w, ctx) = Fixture();

            // 선발 전원을 지치게 만들고 벤치는 쌩쌩하게 둔다.
            foreach (var slot in ctx.home.tactics.slots)
            {
                var p = w.Player(slot.assignedPlayerId);
                if (p != null) p.condition = 45f;
            }
            foreach (int id in ctx.home.benchIds)
            {
                var p = w.Player(id);
                if (p != null) p.condition = 100f;
            }

            var s = MatchSession.Begin(ctx, w.Lookup);
            s.AdvanceToEnd();

            Assert.True(s.SubsUsed(true) > 0, "지친 선발이 한 명도 교체되지 않았다");
            Assert.Contains(s.Events, e => e.eventType == MatchEventType.Substitution);
        }

        [Fact]
        public void 자동_교체를_끄면_교체가_일어나지_않는다()
        {
            var (w, ctx) = Fixture();
            foreach (var slot in ctx.home.tactics.slots)
            {
                var p = w.Player(slot.assignedPlayerId);
                if (p != null) p.condition = 45f;
            }

            var s = MatchSession.Begin(ctx, w.Lookup);
            s.AutoSubstitutions = (false, false);
            s.AdvanceToEnd();

            Assert.Equal(0, s.SubsUsed(true));
        }
    }
}
