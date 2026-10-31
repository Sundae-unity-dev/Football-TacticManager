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
    /// 은퇴와 이사회 협상.
    ///
    /// 은퇴가 없으면 세계가 썩는다 — 12시즌 만에 46세가 뛰고 40세 이상이 330명이었다.
    /// 전력과 득점만 재는 감사로는 잡히지 않았다.
    /// </summary>
    public class RetirementTests
    {
        static GameWorld Make(int seed = 31415, int stars = 4)
        {
            var w = WorldGenerator.Create(seed);
            var club = w.Clubs.Values.First(c => c.reputation == stars);
            ManagerCareer.TakeCharge(w, club.id, "테스트");
            return w;
        }

        static void Seasons(GameWorld w, int n)
        {
            for (int s = 0; s < n; s++)
            {
                int guard = 0;
                while (!w.SeasonFinished && guard++ < 60)
                    if (GameEngine.PlayNextRound(w) == null) break;
                SeasonRollover.StartNextSeason(w);
            }
        }

        // ── 은퇴 ──────────────────────────────────────────────────────────

        [Fact]
        public void 젊은_선수는_그만두지_않는다()
        {
            var w = Make();
            foreach (var p in w.Players.Values.Where(x => w.Age(x) < BalanceConfig.RetireMinAge))
                Assert.Equal(0f, Retirement.Chance(w, p), 5);
        }

        [Fact]
        public void 나이가_많을수록_그만둘_확률이_높다()
        {
            var w = Make();
            var club = w.MyClub!;
            // 또래보다 한참 잘하는 선수를 고르면 확률이 0 으로 눌려 비교가 안 된다.
            var sample = w.Squad(club.id)
                .Where(p => (p.positionFlags & Position.GK) == 0)
                .OrderBy(GameWorld.Ca).First();

            float At(int age)
            {
                sample.dateOfBirth = new GameDate(w.Date.year - age, 1, 1);
                sample.seasonStats = new PlayerStats();   // 출전 없음으로 고정
                return Retirement.Chance(w, sample);
            }

            Assert.True(At(36) > At(33));
            Assert.True(At(33) > At(31));
        }

        [Fact]
        public void 뛴_선수는_더_오래_한다()
        {
            // 나이만으로 자르면 서른여섯 주전이 사라진다.
            var w = Make();
            var p = w.Squad(w.ManagedClubId).First(x => (x.positionFlags & Position.GK) == 0);
            p.dateOfBirth = new GameDate(w.Date.year - 34, 1, 1);

            p.seasonStats = new PlayerStats { appearances = 0 };
            float idle = Retirement.Chance(w, p);

            p.seasonStats = new PlayerStats { appearances = BalanceConfig.RetireGamesCap };
            float busy = Retirement.Chance(w, p);

            Assert.True(busy < idle, $"주전({busy:F3})이 벤치({idle:F3})보다 빨리 접는다");
        }

        [Fact]
        public void 또래보다_잘하면_더_오래_한다()
        {
            // 절대 수준으로 재면 하부 리그 선수가 서른둘에 전부 사라진다 —
            // 적합도에서 거듭 당한 것과 같은 자리다.
            var w = Make();
            var lower = w.Clubs.Values.First(c => c.reputation == 1);
            var upper = w.Clubs.Values.First(c => c.reputation == 5);

            float ChanceIn(ClubData club)
            {
                var p = w.Squad(club.id).OrderByDescending(GameWorld.Ca)
                    .First(x => (x.positionFlags & Position.GK) == 0);
                p.dateOfBirth = new GameDate(w.Date.year - 33, 1, 1);
                p.seasonStats = new PlayerStats();
                return Retirement.Chance(w, p);
            }

            // 두 리그의 에이스는 실력 차가 크지만, 각자 또래 기준으로는 둘 다 앞선다.
            Assert.True(ChanceIn(lower) < BalanceConfig.RetireChanceCap);
            Assert.True(Math.Abs(ChanceIn(lower) - ChanceIn(upper)) < 0.35f,
                "하부 리그 에이스가 상부 리그 에이스보다 훨씬 빨리 접는다");
        }

        [Fact]
        public void 골키퍼가_더_오래_한다()
        {
            // 같은 선수를 놓고 포지션만 바꾼다. 다른 선수끼리 견주면 실력 차가 섞인다.
            var w = Make();
            var p = w.Squad(w.ManagedClubId).OrderBy(GameWorld.Ca).First();
            p.dateOfBirth = new GameDate(w.Date.year - 35, 1, 1);
            p.seasonStats = new PlayerStats();

            var original = p.positionFlags;

            p.positionFlags = Position.CB;
            float outfield = Retirement.Chance(w, p);

            p.positionFlags = Position.GK;
            float keeper = Retirement.Chance(w, p);

            p.positionFlags = original;

            Assert.True(outfield > 0f, "확률이 0 으로 눌려 견줄 수 없다");
            Assert.True(keeper < outfield, $"골키퍼({keeper:F3})가 필드 선수({outfield:F3})보다 빨리 접는다");
        }

        [Fact]
        public void 세계가_늙지_않는다()
        {
            var w = Make(606);
            Seasons(w, 10);

            var squads = w.Players.Values.Where(p => p.clubId > 0).ToList();
            Assert.True(squads.Count > 0);

            double avg = squads.Average(p => w.Age(p));
            Assert.True(avg > 23.0 && avg < 28.0, $"평균 나이 {avg:F1} — 세계가 기울었다");

            Assert.Equal(0, squads.Count(p => w.Age(p) >= BalanceConfig.RetireHardAge + 2));
        }

        [Fact]
        public void 세계의_선수_수가_유지된다()
        {
            // 은퇴가 유입보다 빠르면 구단이 비고, 느리면 명단이 불어난다.
            var w = Make(606);
            int before = w.Players.Values.Count(p => p.clubId > 0);

            Seasons(w, 10);

            int after = w.Players.Values.Count(p => p.clubId > 0);
            Assert.True(after > before * 0.9 && after < before * 1.15,
                $"{before} → {after} 로 움직였다");
        }

        [Fact]
        public void 은퇴한_선수는_흔적을_남기지_않는다()
        {
            // 하나라도 남으면 없는 선수를 가리키는 참조가 된다.
            var w = Make(606);
            Seasons(w, 6);

            foreach (var club in w.Clubs.Values)
            {
                Assert.All(club.playerIds, id => Assert.True(w.Players.ContainsKey(id)));

                foreach (var t in club.savedTactics)
                    foreach (var s in t.slots)
                        Assert.True(s.assignedPlayerId == 0 || w.Players.ContainsKey(s.assignedPlayerId),
                            $"{club.name} 의 전술에 없는 선수 {s.assignedPlayerId} 가 남아 있다");
            }

            Assert.All(w.Loans.Keys, id => Assert.True(w.Players.ContainsKey(id)));
            Assert.All(w.ScoutReports.Keys, id => Assert.True(w.Players.ContainsKey(id)));
        }

        [Fact]
        public void 자유계약_선수도_그만둔다()
        {
            // 빼놓으면 마흔둘짜리 자유계약이 남아 있다가 계약 정리 때 다시 영입된다.
            var w = Make(606);
            Seasons(w, 10);

            Assert.All(w.Players.Values.Where(p => p.clubId <= 0),
                p => Assert.True(w.Age(p) < BalanceConfig.RetireHardAge + 2));
        }

        [Fact]
        public void 통산_기록이_쌓인다()
        {
            var w = Make(606);
            Seasons(w, 3);

            var veterans = w.Squad(w.ManagedClubId)
                .Where(p => (p.careerStats?.appearances ?? 0) > 0)
                .ToList();

            Assert.NotEmpty(veterans);
            Assert.All(veterans, p => Assert.True(p.careerStats.appearances >= p.seasonStats.appearances));
        }

    }
}
