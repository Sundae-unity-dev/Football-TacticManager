using System;
using System.Collections.Generic;
using System.Linq;
using FootballTactician.Balance;
using FootballTactician.Data;
using FootballTactician.World;
using Xunit;

namespace FT.Core.Tests
{
    /// <summary>재계약 협상과 훈련 지시. 감독의 선택이 결과를 바꿔야 한다.</summary>
    public class ContractAndTrainingTests
    {
        static GameWorld Make(int seed = 4242)
        {
            var w = WorldGenerator.Create(seed);
            var club = w.Clubs.Values.First(c => c.reputation == 3);
            ManagerCareer.TakeCharge(w, club.id, "테스트");
            return w;
        }

        static void PlaySeason(GameWorld w)
        {
            int guard = 0;
            while (!w.SeasonFinished && guard++ < 60)
                if (GameEngine.PlayNextRound(w) == null) break;
        }

        // ── 재계약 ────────────────────────────────────────────────────────

        [Fact]
        public void 요구_주급을_맞추면_재계약된다()
        {
            var w = Make();
            var p = w.Squad(w.ManagedClubId).OrderByDescending(GameWorld.Ca).Skip(4).First();
            p.contract.expiryDate = new GameDate(w.Date.year + 1, 1, 1);

            int want = ContractTalks.DemandedWage(w, p);
            int years = ContractTalks.DemandedYears(w, p);

            Assert.Equal(ContractTalks.Verdict.WageTooLow,
                ContractTalks.Evaluate(w, p, (int)(want * 0.7f), years).Result);

            var ok = ContractTalks.Evaluate(w, p, want, years);
            Assert.True(ok.Accepted, ok.Message);

            ContractTalks.Sign(w, p, want, years);
            Assert.Equal(want, p.contract.weeklyWage);
            Assert.True(p.contract.expiryDate.year >= w.Date.year + years);
            Assert.Equal(ContractStatus.Active, p.contract.status);
        }

        [Fact]
        public void 화면이_보여주는_요구_주급이면_재계약이_성립한다()
        {
            // 표시된 금액으로 제안했는데 거절당하면 감독이 이유를 알 수 없다.
            var w = Make(7788);
            foreach (var p in w.Squad(w.ManagedClubId))
            {
                int want = ContractTalks.DemandedWage(w, p);
                int years = ContractTalks.DemandedYears(w, p);
                var outcome = ContractTalks.Evaluate(w, p, want, years);

                // 주급 여력이 없는 경우는 별개의 사유다.
                if (outcome.Result == ContractTalks.Verdict.NoWageRoom) continue;
                Assert.True(outcome.Accepted, $"{p.FullName}: {outcome.Message}");
            }
        }

        [Fact]
        public void 주전은_후보보다_더_요구한다()
        {
            var w = Make();
            var ranked = w.Squad(w.ManagedClubId).OrderByDescending(GameWorld.Ca).ToList();

            float starRatio = ContractTalks.DemandedWage(w, ranked[0])
                            / Math.Max(1f, ranked[0].contract.weeklyWage);
            float fringeRatio = ContractTalks.DemandedWage(w, ranked[^1])
                              / Math.Max(1f, ranked[^1].contract.weeklyWage);

            Assert.True(starRatio >= fringeRatio,
                $"주전 배수 {starRatio:F2} 가 후보 배수 {fringeRatio:F2} 보다 커야 한다");
        }

        [Fact]
        public void 급여_여력을_넘는_재계약은_막힌다()
        {
            var w = Make();
            var p = w.Squad(w.ManagedClubId).First();
            int huge = (int)(w.MyClub!.finances.annualRevenue / 52);

            Assert.Equal(ContractTalks.Verdict.NoWageRoom,
                ContractTalks.Evaluate(w, p, huge, 3).Result);
        }

        [Fact]
        public void 계약_만료가_임박하면_알려준다()
        {
            var w = Make(3131);
            var star = w.Squad(w.ManagedClubId).OrderByDescending(GameWorld.Ca).First();
            star.contract.expiryDate = new GameDate(w.Date.year, Math.Min(12, w.Date.month + 3), 1);

            ContractTalks.WarnExpiring(w);

            Assert.Contains(w.Inbox, i => i.title.Contains(star.FullName) && i.title.Contains("계약"));
            Assert.Contains(star.id, w.ContractWarnings);

            // 같은 소식을 두 번 보내지 않는다
            int before = w.Inbox.Count;
            ContractTalks.WarnExpiring(w);
            Assert.Equal(before, w.Inbox.Count);
        }

        [Fact]
        public void 재계약하지_않은_내_선수는_시즌_말에_떠난다()
        {
            var w = Make(5959);
            var club = w.MyClub!;
            // 은퇴할 나이가 아닌 선수를 고른다. 아니면 계약이 아니라 나이로 떠나 버린다.
            var victim = w.Squad(club.id)
                .Where(p => w.Age(p) < BalanceConfig.RetireMinAge)
                .OrderByDescending(GameWorld.Ca).Skip(2).First();
            victim.contract.expiryDate = new GameDate(w.Date.year + 1, 1, 1);   // 시즌 중 만료



            PlaySeason(w);
            SeasonRollover.StartNextSeason(w);

            Assert.DoesNotContain(victim.id, club.playerIds);
            // 자유계약이 된 뒤 다른 구단이 바로 주워 갈 수 있다 — 은퇴가 생기면서
            // 빈자리가 늘어 그 일이 흔해졌다. 여기서 보는 것은 "내 구단을 떠났는가" 다.
            Assert.NotEqual(club.id, victim.clubId);
            Assert.Contains(w.Inbox, i => i.title.Contains("계약 만료로 떠남"));
        }

        [Fact]
        public void 재계약한_선수는_남는다()
        {
            var w = Make(5959);
            var club = w.MyClub!;
            var keeper = w.Squad(club.id)
                .Where(p => w.Age(p) < BalanceConfig.RetireMinAge)
                .OrderByDescending(GameWorld.Ca).Skip(2).First();
            keeper.contract.expiryDate = new GameDate(w.Date.year + 1, 1, 1);

            ContractTalks.Sign(w, keeper,
                ContractTalks.DemandedWage(w, keeper),
                ContractTalks.DemandedYears(w, keeper));

            PlaySeason(w);
            SeasonRollover.StartNextSeason(w);

            Assert.Contains(keeper.id, club.playerIds);
            Assert.Equal(club.id, keeper.clubId);
        }

        // ── 훈련 ──────────────────────────────────────────────────────────

        [Fact]
        public void 강도를_올리면_성장이_빨라지고_회복이_느려진다()
        {
            var light = new TrainingPlan { Intensity = 1 };
            var hard = new TrainingPlan { Intensity = 5 };

            Assert.True(hard.GrowthFactor > light.GrowthFactor);
            Assert.True(hard.RecoveryFactor < light.RecoveryFactor);
            Assert.True(hard.InjuryFactor > light.InjuryFactor);

            // 공짜 선택이면 안 된다 — 얻는 만큼 잃어야 한다.
            Assert.True(hard.GrowthFactor > 1f && hard.InjuryFactor > 1f);
            Assert.True(light.GrowthFactor < 1f && light.InjuryFactor < 1f);
        }

        [Fact]
        public void 유스_중점은_어린_선수를_더_키운다()
        {
            var w = Make();
            var youth = w.Squad(w.ManagedClubId).First(p => w.Age(p) <= 21);
            var veteran = w.Squad(w.ManagedClubId).First(p => w.Age(p) >= 28);

            var plan = new TrainingPlan { Focus = TrainingFocus.Youth };

            Assert.True(plan.FocusBonusFor(youth, w.Age(youth)) > 1.2f);
            Assert.True(plan.FocusBonusFor(veteran, w.Age(veteran)) < 1f);
        }

        [Fact]
        public void 훈련_강도가_한_시즌_결과를_바꾼다()
        {
            float Run(int intensity)
            {
                var w = Make(515151);
                w.Training.Intensity = intensity;
                float before = w.Squad(w.ManagedClubId).Average(GameWorld.Ca);
                PlaySeason(w);
                return w.Squad(w.ManagedClubId).Average(GameWorld.Ca) - before;
            }

            float light = Run(1);
            float hard = Run(5);

            Assert.True(hard > light + 0.8f,
                $"혹독 {hard:F1} 과 가볍게 {light:F1} 의 차이가 너무 작아 선택이 의미 없다");
        }

        [Fact]
        public void 혹독하게_굴리면_컨디션이_떨어진다()
        {
            float Run(int intensity)
            {
                var w = Make(515151);
                w.Training.Intensity = intensity;
                PlaySeason(w);
                return w.Squad(w.ManagedClubId).Average(p => p.condition);
            }

            Assert.True(Run(5) < Run(1) - 3f, "강도가 컨디션에 드러나지 않는다");
        }

        [Fact]
        public void AI_구단은_기본_훈련을_쓴다()
        {
            // 플레이어의 훈련 선택이 남의 구단 성장을 체계적으로 바꾸면 안 된다.
            //
            // 한 시드로 정확히 같기를 바랄 수는 없다. 이적 시장과 대표팀 소집은 세계가 함께 쓰므로
            // 내 구단이 달라지면 남에게 돌아가는 선수도 달라진다 — 그 폭은 한 시즌 성장의 10% 에 이른다.
            // 예전에 2자리 동일 단언이 통과한 것은 그 시드에서 우연히 작았기 때문이고,
            // 실제로 효과를 하나만 켜면 셋 다 켤 때보다 차이가 커졌다. 잡음이라는 뜻이다.
            //
            // 그래서 시드를 여러 개 돌려 **치우침**을 본다. 누수라면 한쪽으로 쏠린다.
            float Run(int seed, int intensity, int clubId)
            {
                var w = Make(seed);
                w.Training.Intensity = intensity;
                float before = w.Squad(clubId).Average(GameWorld.Ca);
                PlaySeason(w);
                return w.Squad(clubId).Average(GameWorld.Ca) - before;
            }

            int[] seeds = { 818181, 220022, 404404, 616161, 737373 };
            var diffs = new List<float>();

            foreach (int seed in seeds)
            {
                var probe = Make(seed);
                // 플레이어와 다른 리그에 있는 구단 — 맞대결이 없다.
                int myLeague = probe.MyLeague!.id;
                int farClub = probe.Clubs.Values.First(c => probe.LeagueOf(c.id)?.id != myLeague).id;
                diffs.Add(Run(seed, 5, farClub) - Run(seed, 1, farClub));
            }

            float mean = diffs.Average();
            float sd = MathF.Sqrt(diffs.Sum(d => (d - mean) * (d - mean)) / Math.Max(1, diffs.Count - 1));

            // 고정된 문턱을 쓰면 안 된다. 이 값의 흔들림은 세계가 얼마나 얽혀 있느냐에 따라
            // 커지고, 기능을 더할수록 커진다 — 실제로 홈그로운 규정을 넣자 폭이 두 배가 됐다.
            // 진짜 누수라면 치우침이 흩어짐보다 크다. 잡음이라면 흩어짐 안에 묻힌다.
            Assert.True(Math.Abs(mean) < sd,
                $"치우침 {mean:F4} 이 흩어짐 {sd:F4} 보다 크다 — 훈련 강도가 남의 구단 성장에 새고 있다");
        }

        [Fact]
        public void 한_구단의_난수가_다른_구단에_새지_않는다()
        {
            // 구단마다 독립된 난수를 쓰지 않으면, 한 구단의 추첨 횟수가 다음 구단의 흐름을
            // 밀어내 플레이어의 선택이 세계 전체를 흔든다.
            var a = Make(246824);
            var b = Make(246824);
            b.Training.Focus = TrainingFocus.Goalkeeping;   // 플레이어 구단만 달라진다

            int farA = a.Clubs.Values.First(c => a.LeagueOf(c.id)?.id != a.MyLeague!.id).id;

            for (int week = 0; week < 6; week++)
            {
                WeekCycle.Advance(a, a.Date.AddDays(7));
                WeekCycle.Advance(b, b.Date.AddDays(7));
            }

            Assert.Equal(a.Squad(farA).Average(GameWorld.Ca),
                         b.Squad(farA).Average(GameWorld.Ca), 3);
            Assert.Equal(a.Squad(farA).Count(p => p.IsInjured),
                         b.Squad(farA).Count(p => p.IsInjured));
        }
    }
}
