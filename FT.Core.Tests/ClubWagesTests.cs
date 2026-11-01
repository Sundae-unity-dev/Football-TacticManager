using System.Linq;
using FootballTactician.Balance;
using FootballTactician.Data;
using FootballTactician.World;
using Xunit;

namespace FT.Core.Tests
{
    /// <summary>
    /// 급여 합계의 정의는 하나여야 한다 — <b>선수 + 스태프</b>.
    ///
    /// 예전에는 둘이었다. 스태프를 고용하는 쪽은 합계에 더했고, 다시 세는 쪽은 선수만
    /// 세어 덮어썼다. 그래서 이적이나 재계약을 한 번 하면 스태프 몫이 장부에서 사라졌고,
    /// 스태프 급여는 어디에서도 나가지 않았다. 여기 있는 것은 그 두 구멍을 막는 테스트다.
    /// </summary>
    public class ClubWagesTests
    {
        static GameWorld Managed(int seed = 9182)
        {
            var w = WorldGenerator.Create(seed);
            var club = w.Clubs.Values.First(c => c.reputation == 3);
            ManagerCareer.TakeCharge(w, club.id, "테스트 감독");
            return w;
        }

        [Fact]
        public void 급여_합계는_선수와_스태프를_함께_센다()
        {
            var w = Managed();
            var club = w.MyClub!;

            Assert.True(ClubWages.Staff(w, club) > 0, "스태프가 있어야 뜻이 있는 테스트다");
            Assert.Equal(ClubWages.Players(w, club) + ClubWages.Staff(w, club),
                         ClubWages.Of(w, club));
            Assert.Equal(ClubWages.Of(w, club), club.finances.currentWageSpend);
        }

        [Fact]
        public void 거래를_해도_스태프_몫이_사라지지_않는다()
        {
            // 이것이 원래 버그다. 이적 한 번에 주당 21K€ 가 장부에서 증발했다.
            var w = Managed();
            var club = w.MyClub!;
            long staffBefore = ClubWages.Staff(w, club);

            var p = w.Squad(club.id).OrderByDescending(GameWorld.Ca).Skip(3).First();
            TransferMarket.RecalcWages(w, club);

            Assert.Equal(staffBefore, ClubWages.Staff(w, club));
            Assert.Equal(ClubWages.Of(w, club), club.finances.currentWageSpend);

            // 재계약 경로도 같다.
            var t = ContractNegotiation.Open(w, p, club.id, renewal: true);
            ContractNegotiation.Sign(w, p, ContractNegotiation.Suggest(t), club.id);

            Assert.Equal(staffBefore, ClubWages.Staff(w, club));
            Assert.Equal(ClubWages.Of(w, club), club.finances.currentWageSpend);
        }

        [Fact]
        public void 주간_진행이_스태프_급여까지_낸다()
        {
            // 잔고로는 볼 수 없다 — 수입이 더 커서 한 주를 돌면 오히려 올라간다.
            // 그래서 같은 세계를 둘 만들어 한쪽의 스태프를 전부 내보내고, 지출의 차이가
            // 정확히 스태프 주급만큼인지 본다. 공짜로 일하고 있으면 차이가 0 이다.
            var a = Managed();
            var b = Managed();

            var ca = a.MyClub!;
            var cb = b.MyClub!;

            long staff = ClubWages.Staff(a, ca);
            Assert.True(staff > 0, "스태프가 있어야 뜻이 있는 테스트다");

            foreach (var s in StaffRoom.Of(b, cb.id).ToList()) StaffRoom.Release(b, cb, s);
            Assert.Equal(0, ClubWages.Staff(b, cb));

            GameEngine.PlayNextRound(a);
            GameEngine.PlayNextRound(b);

            long diff = ca.finances.annualExpenditure - cb.finances.annualExpenditure;
            Assert.Equal(staff * 52, diff);
            Assert.Equal(ClubWages.Of(a, ca), ca.finances.currentWageSpend);
        }

        [Fact]
        public void 스태프를_고용하고_내보내면_합계가_따라온다()
        {
            var w = Managed();
            var club = w.MyClub!;

            var free = w.Staff.Values.FirstOrDefault(s => s.clubId == 0
                && StaffRoom.CanHire(w, club, s) == StaffRoom.HireVerdict.Ok);
            if (free == null) return;       // 자리가 다 찼으면 볼 것이 없다

            long before = club.finances.currentWageSpend;
            Assert.True(StaffRoom.Hire(w, club, free));
            Assert.Equal(ClubWages.Of(w, club), club.finances.currentWageSpend);
            Assert.True(club.finances.currentWageSpend > before, "고용은 급여 합계를 올려야 한다");

            StaffRoom.Release(w, club, free);
            Assert.Equal(ClubWages.Of(w, club), club.finances.currentWageSpend);
            Assert.Equal(before, club.finances.currentWageSpend);
        }

        [Fact]
        public void 세계는_제재선을_넘는_구단을_만들지_않는다()
        {
            // 3부 구단은 평균 급여 비중 81.5% 로 태어났다. 첫날부터 규정 위반이라
            // 계약 화면의 모든 버튼이 죽었다 — 플레이어가 3부에서 시작하는 게임에서는 벽이다.
            var w = WorldGenerator.Create(31415);

            var over = w.Clubs.Values
                .Where(c => c.finances.annualRevenue > 0)
                .Where(c => ClubWages.Of(w, c) * 52f
                          > c.finances.annualRevenue * BalanceConfig.WageSanctionRatio)
                .ToList();

            Assert.True(over.Count == 0,
                $"생성 직후 제재선을 넘는 구단 {over.Count}개 — 예: {over.FirstOrDefault()?.name}");
        }

        [Fact]
        public void 아래_등급도_선수를_더_쓸_여지가_있다()
        {
            // 생성 직후 모든 구단이 경고선(60%) 아래에 있어야 한다.
            // 꽉 찬 채로 시작하면 영입도 재계약도 할 수 없다.
            var w = WorldGenerator.Create(31415);

            foreach (var tier in new[] { LeagueTier.First, LeagueTier.Second, LeagueTier.Third })
            {
                var cs = w.Clubs.Values.Where(c => w.TierOf(c.id) == tier).ToList();
                if (cs.Count == 0) continue;

                double avg = cs.Average(c => ClubWages.Of(w, c) * 52.0 / c.finances.annualRevenue);
                Assert.True(avg <= BalanceConfig.WageWarnRatio + 0.01,
                    $"{tier} 평균 급여 비중 {avg:P1} — 경고선을 넘은 채로 태어났다");
            }
        }
    }
}
