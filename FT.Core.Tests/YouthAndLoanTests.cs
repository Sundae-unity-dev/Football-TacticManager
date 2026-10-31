using System;
using System.Collections.Generic;
using System.Linq;
using FootballTactician.Balance;
using FootballTactician.Data;
using FootballTactician.World;
using Xunit;

namespace FT.Core.Tests
{
    /// <summary>유스 승격과 임대. 임대가 성장으로 이어지지 않으면 보낼 이유가 없다.</summary>
    public class YouthAndLoanTests
    {
        static GameWorld Make(int seed = 70707, int stars = 3)
        {
            var w = WorldGenerator.Create(seed);
            var club = w.Clubs.Values.First(c => c.reputation == stars);
            ManagerCareer.TakeCharge(w, club.id, "테스트");
            return w;
        }

        static void PlaySeason(GameWorld w)
        {
            int guard = 0;
            while (!w.SeasonFinished && guard++ < 60)
                if (GameEngine.PlayNextRound(w) == null) break;
        }

        // ── 유스 ──────────────────────────────────────────────────────────

        [Fact]
        public void 시즌마다_유스가_올라온다()
        {
            var w = Make();
            var club = w.MyClub!;
            club.playerIds.RemoveRange(20, club.playerIds.Count - 20);   // 자리를 비워 둔다

            var before = club.playerIds.ToHashSet();
            var produced = YouthAcademy.RunIntake(w, club, new Random(1));

            Assert.NotEmpty(produced);
            foreach (var p in produced)
            {
                Assert.Contains(p.id, club.playerIds);
                Assert.DoesNotContain(p.id, before);
                Assert.InRange(w.Age(p), 16, 20);
                Assert.NotNull(p.contract);
                Assert.Equal(club.id, p.clubId);
            }
        }

        [Fact]
        public void 아카데미가_좋을수록_많이_올라온다()
        {
            var w = Make();
            var rng = new Random(2);

            int Count(int rating)
            {
                var club = w.MyClub!;
                club.academyRating = rating;
                int total = 0;
                for (int i = 0; i < 40; i++) total += YouthAcademy.IntakeSize(club, rng);
                return total;
            }

            Assert.True(Count(5) > Count(1));
        }

        [Fact]
        public void 스쿼드가_가득_차면_유스를_올리지_않는다()
        {
            var w = Make();
            var club = w.MyClub!;
            var rng = new Random(3);

            while (club.playerIds.Count < BalanceConfig.SquadMax)
                club.playerIds.Add(w.Players.Values.First(p => !club.playerIds.Contains(p.id)).id);

            Assert.Empty(YouthAcademy.RunIntake(w, club, rng));
        }

        [Fact]
        public void 유스_승격이_스쿼드를_터뜨리지_않는다()
        {
            var w = Make(1357);

            for (int season = 0; season < 6; season++)
            {
                PlaySeason(w);
                SeasonRollover.StartNextSeason(w);

                foreach (var club in w.Clubs.Values)
                    Assert.InRange(club.playerIds.Count,
                        BalanceConfig.SquadMin, BalanceConfig.SquadMax + 4);
            }
        }

        // ── 임대 ──────────────────────────────────────────────────────────

        [Fact]
        public void 임대_대상은_어리고_자리가_없는_선수다()
        {
            var w = Make(2468, stars: 5);
            var tac = w.MyClub!.savedTactics.First(t => t.id == w.MyClub.activeTacticsId);
            var starters = tac.slots.Select(s => s.assignedPlayerId).ToHashSet();

            foreach (var p in LoanMarket.Loanable(w, w.ManagedClubId))
            {
                Assert.True(w.Age(p) <= 23);
                Assert.DoesNotContain(p.id, starters);
                Assert.True(PlayerDevelopment.Pa(p) - GameWorld.Ca(p) >= 10f);
                Assert.False(p.IsInjured);
            }
        }

        [Fact]
        public void 임대를_보내면_소속과_명단이_옮겨간다()
        {
            var w = Make(2468, stars: 5);
            var parent = w.MyClub!;
            var p = LoanMarket.Loanable(w, parent.id).First();
            var host = LoanMarket.Suitors(w, p).First();

            Assert.True(LoanMarket.Evaluate(w, p, host).Accepted);
            LoanMarket.Send(w, p, host);

            Assert.Equal(host.id, p.clubId);
            Assert.Contains(p.id, host.playerIds);
            Assert.DoesNotContain(p.id, parent.playerIds);
            Assert.True(p.isOnLoan);
            Assert.Equal(parent.id, p.loanedFromClubId);
            Assert.NotNull(w.OnLoan(p.id));

            // 원소속 선발에 남아 있으면 안 된다
            var tac = parent.savedTactics.First(t => t.id == parent.activeTacticsId);
            Assert.DoesNotContain(p.id, tac.slots.Select(s => s.assignedPlayerId));
        }

        [Fact]
        public void 임대_선수는_시즌_말에_돌아온다()
        {
            var w = Make(2468, stars: 5);
            var parent = w.MyClub!;
            var p = LoanMarket.Loanable(w, parent.id).First();
            var host = LoanMarket.Suitors(w, p).First();
            LoanMarket.Send(w, p, host);

            PlaySeason(w);
            SeasonRollover.StartNextSeason(w);

            Assert.Equal(parent.id, p.clubId);
            Assert.Contains(p.id, parent.playerIds);
            Assert.DoesNotContain(p.id, host.playerIds);
            Assert.False(p.isOnLoan);
            Assert.Null(w.OnLoan(p.id));
            Assert.Empty(w.Loans);
        }

        [Fact]
        public void 임대처는_실제로_선발에_드는_구단만_고른다()
        {
            // 능력치 비교만으로 "주전으로 뛸 수 있다"고 하면 벤치에 앉힌다.
            // 라인업은 슬롯 경쟁과 포지션 적합도로 정해지므로 돌려서 확인해야 한다.
            var w = Make(515253, stars: 5);
            int checkedCount = 0;

            foreach (var club in w.Clubs.Values.Take(40))
            foreach (var p in LoanMarket.Loanable(w, club.id).Take(3))
            foreach (var host in LoanMarket.Suitors(w, p, 3))
            {
                Assert.True(LoanMarket.WouldStart(w, p, host),
                    $"{p.FullName} 이 {host.name} 에서 선발에 들지 못한다");
                checkedCount++;
            }

            Assert.True(checkedCount > 0, "검사할 임대처가 하나도 없었다");
        }

        [Fact]
        public void 임대_간_선수는_실제로_뛴다()
        {
            var w = Make(515253, stars: 5);
            var parent = w.MyClub!;
            var ranked = w.Squad(parent.id).OrderByDescending(GameWorld.Ca).ToList();

            var p = LoanMarket.Loanable(w, parent.id)
                .Where(x => ranked.IndexOf(x) >= 14)
                // 갈 곳이 있는 선수만 고른다. 임대처가 없는 선수를 보내 놓고
                // 출전을 재면 임대가 아니라 선택을 측정하는 셈이 된다.
                .Where(x => LoanMarket.Suitors(w, x, 1).Count > 0)
                .OrderByDescending(x => PlayerDevelopment.Pa(x) - GameWorld.Ca(x))
                .FirstOrDefault();
            Assert.NotNull(p);

            var host = LoanMarket.Suitors(w, p!).First();

            LoanMarket.Send(w, p, host);
            PlaySeason(w);

            Assert.True(p.seasonStats.appearances >= 20,
                $"임대 보냈는데 {p.seasonStats.appearances}경기밖에 못 뛰었다");
        }

        [Fact]
        public void 뛰지_못할_구단으로는_임대가_성립하지_않는다()
        {
            var w = Make(2468, stars: 5);
            var p = LoanMarket.Loanable(w, w.ManagedClubId).First();
            var pos = p.preferredPosition != Position.None ? p.preferredPosition : p.positionFlags;

            // 그 자리에 훨씬 나은 자원이 있는 구단
            var stacked = w.Clubs.Values
                .Where(c => c.id != p.clubId)
                .FirstOrDefault(c => w.Squad(c.id)
                    .Where(x => (x.positionFlags & pos) != 0)
                    .Any(x => GameWorld.Ca(x) > GameWorld.Ca(p) + 20f));

            if (stacked == null) return;

            // 자리나 주급이 먼저 걸릴 수도 있다. 어느 쪽이든 성립하면 안 된다는 것이 요점이다.
            var verdict = LoanMarket.Evaluate(w, p, stacked).Result;
            Assert.NotEqual(LoanMarket.Verdict.Accepted, verdict);
            Assert.DoesNotContain(stacked, LoanMarket.Suitors(w, p));
        }

        [Fact]
        public void 임대가_성장에_도움이_된다()
        {
            // 1군에서 자리가 없는 선수는 내보내는 편이 나아야 한다.
            // 그렇지 않으면 임대는 쓸 이유 없는 기능이다.
            (float gain, int apps) Run(bool loanOut)
            {
                var w = Make(515253, stars: 5);
                var parent = w.MyClub!;
                var ranked = w.Squad(parent.id).OrderByDescending(GameWorld.Ca).ToList();

                var p = LoanMarket.Loanable(w, parent.id)
                    .Where(x => ranked.IndexOf(x) >= 14)
                    .OrderByDescending(x => PlayerDevelopment.Pa(x) - GameWorld.Ca(x))
                    .FirstOrDefault();
                if (p == null) return (0f, 0);

                float before = GameWorld.Ca(p);
                if (loanOut)
                {
                    var host = LoanMarket.Suitors(w, p).FirstOrDefault();
                    if (host != null) LoanMarket.Send(w, p, host);
                }

                PlaySeason(w);
                return (GameWorld.Ca(p) - before, p.seasonStats.appearances);
            }

            var kept = Run(false);
            var sent = Run(true);
            if (sent.apps == 0) return;      // 이번 시드에 받을 곳이 없으면 넘어간다

            Assert.True(sent.gain > kept.gain,
                $"임대 {sent.gain:F1} ({sent.apps}경기) 가 잔류 {kept.gain:F1} ({kept.apps}경기) 보다 나아야 한다");
        }

        [Fact]
        public void AI_임대가_실제로_일어난다()
        {
            var w = Make(8642);
            int moves = LoanMarket.RunAiLoans(w, new Random(9), 60);

            Assert.True(moves > 0, "AI 임대가 한 건도 일어나지 않는다");
            Assert.Equal(moves, w.Loans.Count);
            Assert.DoesNotContain(w.Loans.Values, l =>
                l.parentClubId == w.ManagedClubId || l.hostClubId == w.ManagedClubId);
        }

        [Fact]
        public void 임대도_세이브에_남는다()
        {
            var w = Make(2468, stars: 5);
            var p = LoanMarket.Loanable(w, w.ManagedClubId).First();
            var host = LoanMarket.Suitors(w, p).First();
            LoanMarket.Send(w, p, host);

            var back = SaveGame.Deserialize(SaveGame.Serialize(w, "임대")).world;

            Assert.Equal(w.Loans.Count, back.Loans.Count);
            var deal = back.OnLoan(p.id);
            Assert.NotNull(deal);
            Assert.Equal(host.id, deal!.hostClubId);
            Assert.Equal(w.ManagedClubId, deal.parentClubId);
            Assert.Equal(host.id, back.Player(p.id)!.clubId);
        }

        // ── 시설 투자 ─────────────────────────────────────────────────────

        [Fact]
        public void 남는_돈은_시설에_들어간다()
        {
            var w = Make(9753);
            var club = w.MyClub!;
            club.trainingFacilitiesRating = 1;
            club.academyRating = 1;
            club.finances.balance = club.finances.annualRevenue * 3;

            SeasonRollover.InvestSurplus(w);

            Assert.True(club.trainingFacilitiesRating > 1 || club.academyRating > 1,
                "자금이 넘치는데 시설이 그대로다");
            Assert.True(club.finances.balance < club.finances.annualRevenue * 3);
        }

        [Fact]
        public void 자금이_무한정_쌓이지_않는다()
        {
            var w = Make(555111);

            double CashRatio() => w.Clubs.Values
                .Average(c => c.finances.balance / (double)Math.Max(1, c.finances.annualRevenue));

            for (int season = 0; season < 8; season++)
            {
                PlaySeason(w);
                SeasonRollover.StartNextSeason(w);
            }

            Assert.True(CashRatio() < 2.5,
                $"리그 평균 자금이 연 수입의 {CashRatio():F2}배다");
        }
    }
}
