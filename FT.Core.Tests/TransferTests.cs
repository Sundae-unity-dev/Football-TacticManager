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
    /// 이적 시장. 가장 중요한 것은 돈과 전력이 한쪽으로 쏠리지 않는 것이다.
    /// </summary>
    public class TransferTests
    {
        static GameWorld Make(int seed = 31415)
        {
            var w = WorldGenerator.Create(seed);
            var club = w.Clubs.Values.First(c => c.reputation == 3);
            ManagerCareer.TakeCharge(w, club.id, "테스트");
            return w;
        }

        // ── 가격 ──────────────────────────────────────────────────────────

        [Fact]
        public void 호가는_시장_가치_주변에_머문다()
        {
            var w = Make();
            foreach (var p in w.Players.Values.Take(300))
            {
                long ask = TransferMarket.AskingPrice(w, p);
                Assert.InRange(ask, (long)(p.marketValue * 0.4), (long)(p.marketValue * 2.3) + 50_000);
            }
        }

        [Fact]
        public void 주전은_후보보다_비싸다()
        {
            var w = Make();
            var club = w.Clubs.Values.First(c => c.reputation == 4);
            var ranked = w.Squad(club.id).OrderByDescending(GameWorld.Ca).ToList();

            var star = ranked.First();
            var fringe = ranked.Last();

            // 같은 기량이라면 서열이 높은 쪽이 더 비싸야 한다.
            float starRatio = TransferMarket.AskingPrice(w, star) / (float)star.marketValue;
            float fringeRatio = TransferMarket.AskingPrice(w, fringe) / (float)fringe.marketValue;
            Assert.True(starRatio > fringeRatio,
                $"주전 배수 {starRatio:F2} 가 후보 배수 {fringeRatio:F2} 보다 커야 한다");
        }

        [Fact]
        public void 계약이_얼마_안_남으면_싸진다()
        {
            var w = Make();
            var p = w.Players.Values.First(x => x.clubId != w.ManagedClubId);

            p.contract.expiryDate = new GameDate(w.Date.year + 5, 6, 30);
            long longDeal = TransferMarket.AskingPrice(w, p);

            p.contract.expiryDate = new GameDate(w.Date.year, w.Date.month + 3 > 12 ? 12 : w.Date.month + 3, 1);
            long shortDeal = TransferMarket.AskingPrice(w, p);

            Assert.True(shortDeal < longDeal,
                $"계약 3개월 남은 선수({shortDeal}) 가 5년 남은 선수({longDeal}) 보다 싸야 한다");
        }

        // ── 판단 ──────────────────────────────────────────────────────────

        [Fact]
        public void 호가를_채우면_구단이_수락한다()
        {
            var w = Make();
            var seller = w.Clubs.Values.First(c => c.id != w.ManagedClubId && c.reputation == 2);
            var target = w.Squad(seller.id).OrderByDescending(GameWorld.Ca).Skip(5).First();

            long ask = TransferMarket.AskingPrice(w, target);
            Assert.Equal(TransferMarket.Verdict.Accepted, TransferMarket.SellerVerdict(w, target, ask));
            Assert.Equal(TransferMarket.Verdict.FeeTooLow,
                TransferMarket.SellerVerdict(w, target, ask / 2));
        }

        [Fact]
        public void 예산을_넘는_제안은_막힌다()
        {
            var w = Make();
            var buyer = w.MyClub!;
            long tooMuch = buyer.finances.transferBudget + 10_000_000;

            Assert.Equal(TransferMarket.Verdict.NoBudget,
                TransferMarket.BuyerVerdict(w, buyer, tooMuch, 10_000));
        }

        [Fact]
        public void 급여_한도를_넘는_주급은_막힌다()
        {
            var w = Make();
            var buyer = w.MyClub!;
            int huge = (int)(buyer.finances.annualRevenue / 52);   // 주당 연수입 전부

            Assert.Equal(TransferMarket.Verdict.NoWageRoom,
                TransferMarket.BuyerVerdict(w, buyer, 0, huge));
        }

        [Fact]
        public void 선수는_주급이_모자라면_거절한다()
        {
            var w = Make();
            var buyer = w.MyClub!;
            var target = w.Players.Values.First(p => p.clubId != buyer.id);

            int expected = TransferMarket.ExpectedWage(w, target, buyer);
            Assert.Equal(TransferMarket.Verdict.WageTooLow,
                TransferMarket.PlayerVerdict(w, target, buyer, (int)(expected * 0.5f)));
        }

        [Fact]
        public void 작은_구단으로_갈_때는_주급을_더_요구한다()
        {
            var w = Make();
            var small = w.Clubs.Values.First(c => c.reputation == 1);
            var bigClub = w.Clubs.Values.First(c => c.reputation == 5);
            var star = w.Squad(bigClub.id).OrderByDescending(GameWorld.Ca).First();

            int expected = TransferMarket.ExpectedWage(w, star, small);
            int required = TransferMarket.RequiredWage(w, star, small);

            Assert.True(required > expected,
                "평판이 낮은 구단으로 가려면 기대 주급보다 더 필요해야 한다");
            Assert.Equal(TransferMarket.Verdict.ClubNotAppealing,
                TransferMarket.PlayerVerdict(w, star, small, expected));
            Assert.Equal(TransferMarket.Verdict.Accepted,
                TransferMarket.PlayerVerdict(w, star, small, required));
        }

        [Fact]
        public void 화면이_보여주는_요구_주급이면_선수가_수락한다()
        {
            // 표시된 금액으로 제안했는데 거절당하면 감독이 이유를 알 수 없다.
            var w = Make(5566);
            var buyer = w.MyClub!;

            foreach (var p in w.Players.Values.Where(x => x.clubId != buyer.id && x.clubId != 0).Take(200))
            {
                int shown = TransferMarket.RequiredWage(w, p, buyer);
                Assert.Equal(TransferMarket.Verdict.Accepted,
                    TransferMarket.PlayerVerdict(w, p, buyer, shown));
            }
        }

        [Fact]
        public void 대체_자원이_없으면_팔지_않는다()
        {
            var w = Make();
            var club = w.Clubs.Values.First(c => c.id != w.ManagedClubId);
            var keeper = w.Squad(club.id).First(p => (p.positionFlags & Position.GK) != 0);

            // 골키퍼를 한 명만 남긴다
            foreach (var other in w.Squad(club.id)
                         .Where(p => (p.positionFlags & Position.GK) != 0 && p.id != keeper.id)
                         .ToList())
            {
                club.playerIds.Remove(other.id);
                w.Players.Remove(other.id);
            }

            Assert.Equal(TransferMarket.Verdict.SquadTooThin,
                TransferMarket.SellerVerdict(w, keeper, long.MaxValue / 2));
        }

        // ── 체결 ──────────────────────────────────────────────────────────

        [Fact]
        public void 이적이_체결되면_소속과_돈이_옮겨간다()
        {
            var w = Make();
            var buyer = w.MyClub!;
            var seller = w.Clubs.Values.First(c =>
                c.id != buyer.id && c.reputation <= buyer.reputation);
            var target = w.Squad(seller.id).OrderByDescending(GameWorld.Ca).Skip(8).First();

            long fee = TransferMarket.AskingPrice(w, target);
            int wage = TransferMarket.ExpectedWage(w, target, buyer);

            long buyerBefore = buyer.finances.balance;
            long sellerBefore = seller.finances.balance;
            int buyerSquad = buyer.playerIds.Count;
            int sellerSquad = seller.playerIds.Count;

            TransferMarket.Execute(w, TransferMarket.BuildOffer(w, target, fee, wage, 3));

            Assert.Equal(buyer.id, target.clubId);
            Assert.Contains(target.id, buyer.playerIds);
            Assert.DoesNotContain(target.id, seller.playerIds);
            Assert.Equal(buyerSquad + 1, buyer.playerIds.Count);
            Assert.Equal(sellerSquad - 1, seller.playerIds.Count);

            Assert.Equal(buyerBefore - fee, buyer.finances.balance);
            Assert.Equal(sellerBefore + fee, seller.finances.balance);

            Assert.Equal(wage, target.contract.weeklyWage);
            Assert.Equal(buyer.id, target.contract.clubId);

            // 급여 합계가 다시 계산되어야 한다 — 선수 + 스태프다.
            // 예전에는 여기서 선수만 세었고, 그래서 이적 한 번에 스태프 몫이 사라졌다.
            Assert.Equal(ClubWages.Of(w, buyer), buyer.finances.currentWageSpend);

            Assert.Single(w.Transfers, t => t.playerId == target.id);
        }

        [Fact]
        public void 이적_후_양쪽_선발이_다시_뽑힌다()
        {
            var w = Make();
            var buyer = w.MyClub!;
            var seller = w.Clubs.Values.First(c => c.id != buyer.id && c.reputation == 5);
            var star = w.Squad(seller.id).OrderByDescending(GameWorld.Ca).First();

            TransferMarket.Execute(w, TransferMarket.BuildOffer(
                w, star, TransferMarket.AskingPrice(w, star),
                TransferMarket.ExpectedWage(w, star, buyer), 4));

            // 팔린 선수가 원소속 선발에 남아 있으면 안 된다
            var sellerTac = seller.savedTactics.First(t => t.id == seller.activeTacticsId);
            Assert.DoesNotContain(star.id, sellerTac.slots.Select(s => s.assignedPlayerId));

            // 선발 11자리가 모두 자기 구단 선수로 채워져야 한다
            foreach (var club in new[] { buyer, seller })
            {
                var tac = club.savedTactics.First(t => t.id == club.activeTacticsId);
                foreach (var slot in tac.slots)
                {
                    var p = w.Player(slot.assignedPlayerId);
                    Assert.NotNull(p);
                    Assert.Equal(club.id, p!.clubId);
                }
            }
        }

        // ── 창구와 AI ─────────────────────────────────────────────────────

        [Fact]
        public void 창구가_열린_동안에만_AI_이적이_일어난다()
        {
            var w = Make();
            Assert.True(TransferMarket.IsWindowOpen(w), "7월 1일에는 여름 창구가 열려 있어야 한다");

            var league = w.MyLeague!;
            w.Date = new GameDate(w.SeasonYear, 11, 1);
            Assert.False(TransferMarket.IsWindowOpen(w), "11월에는 창구가 닫혀 있어야 한다");
        }

        [Fact]
        public void AI_이적이_실제로_일어난다()
        {
            var w = Make();
            var rng = new Random(7);
            TransferMarket.UpdateTransferLists(w, rng);

            int moves = TransferMarket.RunAiWindow(w, rng, 40);
            Assert.True(moves > 0, "창구가 열렸는데 AI 이적이 한 건도 없다");
            Assert.Equal(moves, w.Transfers.Count);

            // 플레이어 구단은 AI 가 마음대로 건드리지 않는다
            Assert.DoesNotContain(w.Transfers, t =>
                t.fromClubId == w.ManagedClubId || t.toClubId == w.ManagedClubId);
        }

        [Fact]
        public void 여러_시즌을_돌려도_전력이_한쪽으로_쏠리지_않는다()
        {
            var w = Make(80808);
            var league = w.MyLeague!;

            float Share()
            {
                var ranked = league.clubIds.OrderByDescending(id => w.Strength(id)).ToList();
                return ranked.Take(5).Sum(id => w.Strength(id)) / ranked.Sum(id => w.Strength(id));
            }

            float before = Share();

            for (int season = 0; season < 4; season++)
            {
                int guard = 0;
                while (!w.SeasonFinished && guard++ < 60)
                    if (GameEngine.PlayNextRound(w) == null) break;

                for (int week = 0; week < 14; week++)
                    WeekCycle.Advance(w, w.Date.AddDays(7));

                SeasonRollover.StartNextSeason(w);
            }

            float after = Share();
            Assert.InRange(after, before - 0.05f, before + 0.05f);
        }

        [Fact]
        public void 제재선까지_급여를_써도_구조적_적자가_되지_않는다()
        {
            // 급여 상한과 운영비의 합이 1.0 을 넘으면, 규정을 다 지킨 구단도
            // 매년 적자를 보고 영영 회복하지 못한다.
            float worstCase = BalanceConfig.WageSanctionRatio + BalanceConfig.OverheadRatio;
            Assert.True(worstCase < 1.0f,
                $"급여 제재선 {BalanceConfig.WageSanctionRatio:P0} + 운영비 {BalanceConfig.OverheadRatio:P0} " +
                $"= {worstCase:P0} 가 수입을 넘는다");

            // 경고선 수준이면 흑자가 남아야 한다.
            Assert.True(BalanceConfig.WageWarnRatio + BalanceConfig.OverheadRatio < 0.95f);
        }

        [Fact]
        public void 적자_구단도_몇_시즌이면_회복한다()
        {
            var w = Make(24680);
            var club = w.MyClub!;
            club.finances.balance = -5_000_000;      // 과소비로 적자에 빠진 상태

            for (int season = 0; season < 3; season++)
            {
                int guard = 0;
                while (!w.SeasonFinished && guard++ < 60)
                    if (GameEngine.PlayNextRound(w) == null) break;
                for (int week = 0; week < 14; week++)
                    WeekCycle.Advance(w, w.Date.AddDays(7));
                SeasonRollover.StartNextSeason(w);
            }

            Assert.True(club.finances.balance > 0,
                $"급여 비중 {club.finances.currentWageSpend * 52f / club.finances.annualRevenue:P0} 인데 " +
                $"3시즌 뒤에도 자금이 {club.finances.balance / 1_000_000d:F1}M€ 다");
        }

        [Fact]
        public void 구단_자금이_무한정_쌓이지_않는다()
        {
            var w = Make(555111);

            double CashRatio() => w.Clubs.Values
                .Average(c => c.finances.balance / (double)Math.Max(1, c.finances.annualRevenue));

            double early = 0;

            for (int season = 0; season < 8; season++)
            {
                int guard = 0;
                while (!w.SeasonFinished && guard++ < 60)
                    if (GameEngine.PlayNextRound(w) == null) break;

                for (int week = 0; week < 14; week++)
                    WeekCycle.Advance(w, w.Date.AddDays(7));

                SeasonRollover.StartNextSeason(w);
                if (season == 3) early = CashRatio();
            }

            double late = CashRatio();

            // 급여 외 운영비가 없으면 수입의 절반씩 영원히 쌓인다.
            // 강등 직후처럼 한때 비율이 튀는 구단은 있을 수 있으므로 리그 평균 추세로 본다.
            Assert.True(late < early + 1.0,
                $"자금 비율이 4시즌 {early:F2}배 → 8시즌 {late:F2}배 로 계속 쌓인다");
            Assert.True(late < 4.0, $"리그 평균 자금이 연 수입의 {late:F2}배다");
        }

        // ── 검색 ──────────────────────────────────────────────────────────

        [Fact]
        public void 검색은_내_구단_선수를_빼고_조건을_지킨다()
        {
            var w = Make();
            var found = TransferMarket.Scout(w, new TransferMarket.ScoutFilter
            {
                Position = Position.ST,
                MaxAge = 26,
                MinCa = 100,
            });

            Assert.NotEmpty(found);
            foreach (var p in found)
            {
                Assert.NotEqual(w.ManagedClubId, p.clubId);
                Assert.True((p.positionFlags & Position.ST) != 0);
                Assert.True(w.Age(p) <= 26);
                Assert.True(GameWorld.Ca(p) >= 100);
            }
        }

        // ── 시즌 전환 ─────────────────────────────────────────────────────

        [Fact]
        public void 시즌을_넘기면_일정과_기록이_새로_시작된다()
        {
            var w = Make(246810);
            var league = w.MyLeague!;
            int year = w.SeasonYear;

            int guard = 0;
            while (!w.SeasonFinished && guard++ < 60)
                if (GameEngine.PlayNextRound(w) == null) break;

            var scorer = w.Players.Values.OrderByDescending(p => p.seasonStats.goals).First();
            Assert.True(scorer.seasonStats.goals > 0);

            SeasonRollover.StartNextSeason(w);

            Assert.Equal(year + 1, w.SeasonYear);
            Assert.Equal(year + 1, league.currentSeason.year);
            Assert.All(league.currentSeason.fixtures, f => Assert.False(f.isPlayed));
            Assert.All(league.currentSeason.standings, e => Assert.Equal(0, e.played));
            Assert.Equal(0, scorer.seasonStats.goals);

            // 지난 시즌 성적이 구단 역사에 남는다
            Assert.Contains(w.MyClub!.history.recentSeasons, s => s.season == year);

            // 새 일정의 id 가 지난 시즌과 겹치면 경기 시드가 충돌한다
            Assert.Equal(league.currentSeason.fixtures.Count,
                         league.currentSeason.fixtures.Select(f => f.id).Distinct().Count());
        }

        [Fact]
        public void 시즌을_넘기면_이적_예산이_다시_책정된다()
        {
            var w = Make(13131);
            var club = w.MyClub!;
            club.finances.transferBudget = 0;

            int guard = 0;
            while (!w.SeasonFinished && guard++ < 60)
                if (GameEngine.PlayNextRound(w) == null) break;

            SeasonRollover.StartNextSeason(w);

            Assert.True(club.finances.transferBudget > 0,
                "이적 예산이 회복되지 않으면 몇 시즌 만에 시장이 멈춘다");
        }
    }
}
