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
    /// 감독 커리어. 경질이 게임 종료가 아니라 한 장면이어야 한다.
    /// </summary>
    public class CareerTests
    {
        static GameWorld Make(int seed, int stars = 3)
        {
            var w = WorldGenerator.Create(seed);
            var club = w.Clubs.Values.First(c => c.reputation == stars);
            ManagerCareer.TakeCharge(w, club.id, "테스트 감독");
            return w;
        }

        static void PlaySeason(GameWorld w)
        {
            int guard = 0;
            while (!w.SeasonFinished && guard++ < 60)
                if (GameEngine.PlayNextRound(w) == null) break;
        }

        [Fact]
        public void 작은_구단에서_시작하면_평판도_낮다()
        {
            var w = WorldGenerator.Create(111);
            var small = w.Clubs.Values.First(c =>
                c.reputation == 1 && w.TierOf(c.id) == LeagueTier.Second);
            ManagerCareer.TakeCharge(w, small.id, "테스트");

            Assert.Equal(BalanceConfig.ManagerStartingReputation, w.ManagerReputation);

            var bigClub = w.Clubs.Values.First(c => c.reputation == 5);
            Assert.True(ManagerCareer.RequiredReputation(w, bigClub) > w.ManagerReputation,
                "하위 구단에서 시작했는데 바로 최상위 구단을 맡을 수 있으면 커리어가 성립하지 않는다");
        }

        [Fact]
        public void 맡은_구단은_내_평판으로_다시_맡을_수_있어야_한다()
        {
            // 5성 구단에서 시작했는데 평판이 모자라면, 그 자리를 다시는 얻을 수 없다는 뜻이 된다.
            foreach (int stars in new[] { 5, 4, 3, 2, 1 })
            {
                var w = WorldGenerator.Create(222 + stars);
                var club = w.Clubs.Values.First(c =>
                    c.reputation == stars && w.TierOf(c.id) == LeagueTier.First);
                ManagerCareer.TakeCharge(w, club.id, "테스트");

                Assert.True(w.ManagerReputation >= ManagerCareer.RequiredReputation(w, club),
                    $"별 {stars} 구단에서 시작했는데 평판 {w.ManagerReputation} 로는 " +
                    $"{ManagerCareer.RequiredReputation(w, club)} 가 필요한 그 자리를 못 맡는다");
            }
        }

        [Fact]
        public void 기대를_넘으면_평판이_오르고_못_미치면_내린다()
        {
            var w = Make(222, stars: 3);
            int size = w.MyLeague!.clubIds.Count;
            int start = w.ManagerReputation;

            ManagerCareer.UpdateReputation(w, finalPosition: 1, size, wonCup: false);
            int afterGood = w.ManagerReputation;
            Assert.True(afterGood > start, "우승했는데 평판이 오르지 않았다");

            ManagerCareer.UpdateReputation(w, finalPosition: size, size, wonCup: false);
            Assert.True(w.ManagerReputation < afterGood, "최하위인데 평판이 내리지 않았다");
        }

        [Fact]
        public void 컵_우승은_평판을_더_올린다()
        {
            var a = Make(333);
            var b = Make(333);
            int size = a.MyLeague!.clubIds.Count;

            ManagerCareer.UpdateReputation(a, 5, size, wonCup: false);
            ManagerCareer.UpdateReputation(b, 5, size, wonCup: true);

            Assert.True(b.ManagerReputation > a.ManagerReputation);
        }

        [Fact]
        public void 평판이_오르는_폭에_상한이_있다()
        {
            var w = Make(444, stars: 1);
            int size = w.MyLeague!.clubIds.Count;
            int before = w.ManagerReputation;

            ManagerCareer.UpdateReputation(w, 1, size, wonCup: true);

            Assert.True(w.ManagerReputation - before <= 20,
                "한 시즌에 평판이 폭증하면 바로 최상위 구단으로 갈 수 있다");
        }

        [Fact]
        public void 시즌이_끝나면_감독을_구하는_구단이_생긴다()
        {
            var w = Make(555);
            PlaySeason(w);

            Assert.NotEmpty(w.VacantClubs);
            Assert.DoesNotContain(w.ManagedClubId, w.VacantClubs);
        }

        [Fact]
        public void 평판이_닿는_자리만_제안으로_보인다()
        {
            var w = Make(666);
            PlaySeason(w);

            var jobs = ManagerCareer.OpenJobs(w, new Random(1), take: 20);
            foreach (var job in jobs)
            {
                Assert.True(w.ManagerReputation >= job.requiredReputation);
                Assert.Contains(job.clubId, w.VacantClubs);
                Assert.NotEqual(w.ManagedClubId, job.clubId);
            }
        }

        [Fact]
        public void 이직하면_평판과_커리어_기록이_남는다()
        {
            var w = Make(777);
            PlaySeason(w);

            int firstClub = w.ManagedClubId;
            string firstName = w.MyClub!.name;
            int rep = w.ManagerReputation;
            int wins = w.Manager.totalWins;

            var jobs = ManagerCareer.OpenJobs(w, new Random(2), take: 20);
            if (jobs.Count == 0) return;                 // 이번 시드에 자리가 없으면 넘어간다

            ManagerCareer.TakeJob(w, jobs[0].clubId);

            Assert.NotEqual(firstClub, w.ManagedClubId);
            Assert.Equal(rep, w.ManagerReputation);      // 평판은 따라간다
            Assert.False(w.Sacked);

            var spell = Assert.Single(w.CareerHistory);
            Assert.Equal(firstClub, spell.clubId);
            Assert.Equal(firstName, spell.clubName);
            Assert.Equal(wins, spell.wins);

            // 새 구단의 목표와 라인업이 제대로 잡혀야 한다
            Assert.NotEmpty(w.Objectives);
            var lineup = MatchDay.BuildLineup(w, w.ManagedClubId);
            Assert.NotNull(lineup);
            Assert.All(lineup!.tactics.slots,
                s => Assert.Equal(w.ManagedClubId, w.Player(s.assignedPlayerId)?.clubId));
        }

        [Fact]
        public void 경질되어도_맡을_자리가_생긴다()
        {
            var w = Make(888);
            w.Manager.boardConfidence = 5;               // 경질 직전

            PlaySeason(w);

            if (!w.Sacked) return;                       // 이번 시드에 살아남았으면 넘어간다
            Assert.NotEmpty(w.VacantClubs);

            var jobs = ManagerCareer.OpenJobs(w, new Random(3), take: 20);
            Assert.NotEmpty(jobs);

            ManagerCareer.TakeJob(w, jobs[0].clubId);
            Assert.False(w.Sacked, "새 구단을 맡았는데 경질 상태가 남아 있다");
            Assert.Single(w.CareerHistory);
            Assert.True(w.CareerHistory[0].endedSacked);
        }

        [Fact]
        public void 이직_후에도_세계가_정상이다()
        {
            var w = Make(999);
            PlaySeason(w);

            var jobs = ManagerCareer.OpenJobs(w, new Random(4), take: 20);
            if (jobs.Count == 0) return;
            ManagerCareer.TakeJob(w, jobs[0].clubId);

            SeasonRollover.StartNextSeason(w);
            PlaySeason(w);

            Assert.True(w.SeasonFinished);
            foreach (var league in w.Leagues.Values)
                Assert.Equal(league.clubIds.Count, league.currentSeason.standings.Count);
        }

        [Fact]
        public void 커리어가_세이브에_남는다()
        {
            var w = Make(1212);
            PlaySeason(w);

            var jobs = ManagerCareer.OpenJobs(w, new Random(5), take: 20);
            if (jobs.Count > 0) ManagerCareer.TakeJob(w, jobs[0].clubId);

            var back = SaveGame.Deserialize(SaveGame.Serialize(w, "커리어")).world;

            Assert.Equal(w.ManagerReputation, back.ManagerReputation);
            Assert.Equal(w.ManagedClubId, back.ManagedClubId);
            Assert.Equal(w.CareerHistory.Count, back.CareerHistory.Count);
            Assert.Equal(w.VacantClubs.Count, back.VacantClubs.Count);
            Assert.Equal(w.Cups.Count, back.Cups.Count);

            var cupA = w.Cups.Values.First();
            var cupB = back.Cups[cupA.id];
            Assert.Equal(cupA.winnerClubId, cupB.winnerClubId);
            Assert.Equal(cupA.rounds.Count, cupB.rounds.Count);
        }
    }
}
