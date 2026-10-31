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
    /// 경기장과 스폰서.
    ///
    /// 둘 다 모델만 있고 아무도 읽지 않았다. 수입은 평판에서만 나왔고,
    /// 경기장을 키워도 성적을 내도 한 푼 늘지 않았다.
    /// </summary>
    public class BusinessTests
    {
        static GameWorld Make(int seed = 31415, int stars = 3)
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

        // ── 기준선 ────────────────────────────────────────────────────────

        [Fact]
        public void 세계의_수입_기준선이_중립이다()
        {
            // 아무것도 하지 않은 구단이 손해를 보면 안 된다.
            var w = WorldGenerator.Create(7);
            float avg = w.Clubs.Values.Average(ClubBusiness.RevenueMultiplier);

            Assert.True(avg > 0.96f && avg < 1.04f, $"수입 배수 평균 {avg:F3} — 기준선이 밀렸다");
        }

        [Fact]
        public void 시즌이_지나도_기준선이_버틴다()
        {
            // 평판은 승격으로 오르는데 경기장이 생성값에 묶여 있으면
            // 기준에서 해마다 멀어져 세계 수입이 통째로 밀린다.
            var w = Make(606);
            Seasons(w, 8);

            float avg = w.Clubs.Values.Average(ClubBusiness.RevenueMultiplier);
            Assert.True(avg > 0.95f && avg < 1.08f, $"8시즌 뒤 수입 배수 평균 {avg:F3}");
        }

        [Fact]
        public void 스폰서가_없어도_손해가_아니다()
        {
            var w = WorldGenerator.Create(7);
            var club = w.Clubs.Values.First();
            club.finances.mainSponsor = null;

            Assert.Equal(1f, ClubBusiness.SponsorFactor(club), 4);
        }

        [Fact]
        public void 보통_계약이면_수입이_그대로다()
        {
            // "스폰서가 있는가" 를 재면 결국 모두가 갖게 되어 세계 수입이 밀린다.
            var w = Make();
            var club = w.MyClub!;
            club.history = new ClubHistory
            {
                recentSeasons = new List<SeasonRecord> { new() { finalPosition = 10 } },
            };

            club.finances.mainSponsor = ClubBusiness.Negotiate(w, club, new Random(1));

            float f = ClubBusiness.SponsorFactor(club);
            Assert.True(Math.Abs(f - 1f) < 0.03f, $"보통 계약인데 배수가 {f:F3}");
        }

        // ── 성적이 돈이 된다 ──────────────────────────────────────────────

        [Fact]
        public void 성적이_좋으면_스폰서가_더_준다()
        {
            var w = Make();
            var club = w.MyClub!;

            long Fee(params int[] places)
            {
                club.history = new ClubHistory
                {
                    recentSeasons = places.Select(p => new SeasonRecord { finalPosition = p }).ToList(),
                };
                return ClubBusiness.Negotiate(w, club, new Random(5)).annualFee;
            }

            long top = Fee(1, 2, 1);
            long bottom = Fee(17, 19, 18);

            Assert.True(top > bottom * 1.4, $"상위권 {top:N0} vs 하위권 {bottom:N0}");
        }

        [Fact]
        public void 성적이_수입_배수로_이어진다()
        {
            var w = Make();
            var club = w.MyClub!;

            float Factor(params int[] places)
            {
                club.history = new ClubHistory
                {
                    recentSeasons = places.Select(p => new SeasonRecord { finalPosition = p }).ToList(),
                };
                club.finances.mainSponsor = ClubBusiness.Negotiate(w, club, new Random(5));
                return ClubBusiness.SponsorFactor(club);
            }

            Assert.True(Factor(1, 1, 2) > Factor(18, 19, 17));
        }

        // ── 경기장 ────────────────────────────────────────────────────────

        [Fact]
        public void 증축하면_돈이_먼저_나가고_수입은_나중에_온다()
        {
            // 그래야 "지금 선수를 살 것인가, 몇 해 뒤를 살 것인가" 가 판단이 된다.
            var w = Make();
            var club = w.MyClub!;
            club.finances.balance = ClubBusiness.ExpansionCost(club) * 3;

            long cashBefore = club.finances.balance;
            long revenueBefore = club.finances.annualRevenue;
            int seatsBefore = club.stadium.capacity;

            Assert.True(ClubBusiness.Expand(w, club));

            Assert.True(club.stadium.capacity > seatsBefore);
            Assert.True(club.finances.balance < cashBefore);
            Assert.Equal(revenueBefore, club.finances.annualRevenue);   // 아직 그대로

            SeasonRollover.RebudgetClub(w, club);
            Assert.True(club.finances.annualRevenue > revenueBefore);   // 이제 늘었다
        }

        [Fact]
        public void 돈이_없으면_증축할_수_없다()
        {
            var w = Make();
            var club = w.MyClub!;
            club.finances.balance = 0;

            Assert.Equal(ClubBusiness.BuildVerdict.NoFunds, ClubBusiness.CanExpand(club));
            Assert.False(ClubBusiness.Expand(w, club));
        }

        [Fact]
        public void 경기장에_상한이_있다()
        {
            var w = Make();
            var club = w.MyClub!;
            club.stadium.capacity = BalanceConfig.StadiumMaxCapacity;
            club.finances.balance = long.MaxValue / 4;

            Assert.Equal(ClubBusiness.BuildVerdict.AtMax, ClubBusiness.CanExpand(club));
        }

        [Fact]
        public void 구단이_커지면_경기장도_따라_자란다()
        {
            var w = Make();
            var club = w.MyClub!;
            int before = club.stadium.capacity;

            club.reputation = 5;                       // 승격으로 평판이 뛴 상황
            ClubBusiness.DriftTowardExpected(club);

            Assert.True(club.stadium.capacity > before);

            // 한 번에 따라잡으면 감독이 직접 증축할 이유가 없다.
            Assert.True(club.stadium.capacity < ClubBusiness.ExpectedCapacity(5));
        }

        [Fact]
        public void 저절로_줄어들지는_않는다()
        {
            // 강등돼도 경기장이 사라지지는 않는다.
            var w = Make(31415, stars: 5);
            var club = w.MyClub!;
            int before = club.stadium.capacity;

            club.reputation = 1;
            ClubBusiness.DriftTowardExpected(club);

            Assert.Equal(before, club.stadium.capacity);
        }

        // ── 이사회를 통한 증축 ────────────────────────────────────────────

        [Fact]
        public void 이사회에_경기장을_요청할_수_있다()
        {
            var w = Make();
            var club = w.MyClub!;
            w.Manager!.boardConfidence = 100;
            club.finances.balance = (long)club.finances.annualRevenue * 5;

            int before = club.stadium.capacity;

            for (int i = 0; i < 40 && club.stadium.capacity == before; i++)
            {
                w.LastBoardAskDay = 0;
                BoardTalks.Request(w, BoardTalks.Ask.Stadium, new Random(i));
                w.Manager.boardConfidence = 100;
            }

            Assert.True(club.stadium.capacity > before);
        }

        [Fact]
        public void 경기장과_스폰서가_세이브를_왕복한다()
        {
            var w = Make();
            var club = w.MyClub!;
            club.finances.mainSponsor = ClubBusiness.Negotiate(w, club, new Random(2));
            club.finances.balance = ClubBusiness.ExpansionCost(club) * 2;
            ClubBusiness.Expand(w, club);

            var back = SaveGame.Deserialize(SaveGame.Serialize(w, "구단사업")).world!;
            var c2 = back.Club(club.id)!;

            Assert.Equal(club.stadium.capacity, c2.stadium.capacity);
            Assert.Equal(club.stadium.name, c2.stadium.name);
            Assert.Equal(club.finances.mainSponsor.sponsorName, c2.finances.mainSponsor.sponsorName);
            Assert.Equal(club.finances.mainSponsor.annualFee, c2.finances.mainSponsor.annualFee);
            Assert.Equal(ClubBusiness.RevenueMultiplier(club), ClubBusiness.RevenueMultiplier(c2), 4);
        }

        [Fact]
        public void 스폰서_이름이_실존_기업과_겹치지_않는다()
        {
            // 라이선스가 없으므로 모든 이름은 지어낸 것이어야 한다.
            var w = Make(606);
            Seasons(w, 2);

            var names = w.Clubs.Values
                .Select(c => c.finances.mainSponsor?.sponsorName)
                .Where(n => !string.IsNullOrEmpty(n))
                .ToList();

            Assert.NotEmpty(names);
            Assert.All(names, n => Assert.Matches(@"^[가-힣]+ [가-힣]+$", n));
        }
    }
}
