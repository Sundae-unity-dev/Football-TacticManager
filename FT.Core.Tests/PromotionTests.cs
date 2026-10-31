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
    /// 승강, 계약 만료, 스쿼드 유지. 여러 시즌을 돌렸을 때 세계가 무너지지 않아야 한다.
    /// </summary>
    public class PromotionTests
    {
        static GameWorld Make(int seed = 31415)
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
            for (int week = 0; week < 14; week++)
                WeekCycle.Advance(w, w.Date.AddDays(7));
        }

        // ── 등급 ──────────────────────────────────────────────────────────

        [Fact]
        public void 이부_리그가_일부보다_약하다()
        {
            var w = Make();
            foreach (var nation in w.Nations)
            {
                var top = w.League(nation.LeagueIds[0])!;
                var below = w.League(nation.LeagueIds[1])!;

                float topAvg = top.clubIds.Average(id => w.Strength(id));
                float belowAvg = below.clubIds.Average(id => w.Strength(id));
                Assert.True(topAvg > belowAvg + 15f,
                    $"{nation.Country}: 1부 {topAvg:F0} vs 2부 {belowAvg:F0} — 차이가 너무 작다");

                // 다만 경계는 겹쳐야 한다. 완전히 갈라지면 승격 팀이 바로 전멸한다.
                float belowBest = below.clubIds.Max(id => w.Strength(id));
                float topWorst = top.clubIds.Min(id => w.Strength(id));
                Assert.True(belowBest > topWorst - 25f,
                    $"{nation.Country}: 2부 최강 {belowBest:F0} 이 1부 최약 {topWorst:F0} 에 한참 못 미친다");
            }
        }

        [Fact]
        public void 이부_구단의_수입이_일부보다_적다()
        {
            var w = Make();
            foreach (var nation in w.Nations)
            {
                long topAvg = (long)w.League(nation.LeagueIds[0])!.clubIds
                    .Average(id => w.Club(id)!.finances.annualRevenue);
                long belowAvg = (long)w.League(nation.LeagueIds[1])!.clubIds
                    .Average(id => w.Club(id)!.finances.annualRevenue);
                Assert.True(topAvg > belowAvg * 2, $"1부 {topAvg} vs 2부 {belowAvg}");
            }
        }

        // ── 승강 ──────────────────────────────────────────────────────────

        [Fact]
        public void 시즌이_끝나면_하위_팀과_상위_팀이_자리를_바꾼다()
        {
            var w = Make(70707);
            var nation = w.Nations[0];
            var top = w.League(nation.LeagueIds[0])!;
            var below = w.League(nation.LeagueIds[1])!;

            // 1부만 돌려도 2부 순위가 필요하므로 전체를 진행한다.
            PlaySeason(w);

            int spots = top.relegationSpots;
            var doomed = top.currentSeason.standings
                .OrderByDescending(e => e.position).Take(spots).Select(e => e.clubId).ToList();
            var rising = below.currentSeason.standings
                .OrderBy(e => e.position).Take(spots).Select(e => e.clubId).ToList();

            SeasonRollover.StartNextSeason(w);

            foreach (int id in doomed)
            {
                Assert.Contains(id, below.clubIds);
                Assert.DoesNotContain(id, top.clubIds);
                Assert.Equal(below.id.ToString(), w.Club(id)!.leagueId);
            }
            foreach (int id in rising)
            {
                Assert.Contains(id, top.clubIds);
                Assert.DoesNotContain(id, below.clubIds);
                Assert.Equal(top.id.ToString(), w.Club(id)!.leagueId);
            }

            // 인원은 그대로여야 한다
            Assert.Equal(WorldGenerator.ClubsPerLeague, top.clubIds.Count);
            Assert.Equal(WorldGenerator.ClubsPerLeague, below.clubIds.Count);
        }

        [Fact]
        public void 강등되면_수입과_주급이_함께_줄어든다()
        {
            var w = Make(80808);
            var nation = w.Nations[0];
            var top = w.League(nation.LeagueIds[0])!;

            PlaySeason(w);

            int doomedId = top.currentSeason.standings.OrderByDescending(e => e.position).First().clubId;
            var club = w.Club(doomedId)!;
            long revenueBefore = club.finances.annualRevenue;
            int wagesBefore = club.finances.currentWageSpend;

            SeasonRollover.StartNextSeason(w);

            Assert.True(club.finances.annualRevenue < revenueBefore,
                "강등됐는데 수입이 그대로다");
            Assert.True(club.finances.currentWageSpend < wagesBefore,
                "강등됐는데 주급이 그대로면 급여 비중이 치솟아 파산한다");

            float ratio = club.finances.currentWageSpend * 52f / club.finances.annualRevenue;
            Assert.True(ratio < 1.0f, $"강등 후 급여 비중이 {ratio:P0} 다");
        }

        // ── 계약과 스쿼드 ─────────────────────────────────────────────────

        [Fact]
        public void 계약이_만료되면_재계약하거나_풀린다()
        {
            var w = Make(90909);
            var club = w.Clubs.Values.First(c => c.id != w.ManagedClubId);
            var player = w.Squad(club.id).OrderByDescending(GameWorld.Ca).Skip(3).First();

            player.contract.expiryDate = new GameDate(w.Date.year - 1, 6, 30);   // 이미 만료
            int before = player.contract.weeklyWage;

            SeasonRollover.ResolveExpiringContracts(w, new Random(1));

            if (player.clubId == club.id)
            {
                Assert.True(player.contract.expiryDate > w.Date, "재계약했는데 만료일이 그대로다");
                Assert.NotEqual(ContractStatus.Expired, player.contract.status);
            }
            else
            {
                Assert.Equal(0, player.clubId);
                Assert.DoesNotContain(player.id, club.playerIds);
            }
        }

        [Fact]
        public void 여러_시즌을_돌려도_스쿼드가_무너지지_않는다()
        {
            var w = Make(11223);

            for (int season = 0; season < 8; season++)
            {
                PlaySeason(w);
                SeasonRollover.StartNextSeason(w);

                foreach (var club in w.Clubs.Values)
                    Assert.InRange(club.playerIds.Count,
                        BalanceConfig.SquadMin, BalanceConfig.SquadMax + 4);
            }
        }

        [Fact]
        public void 여러_시즌을_돌려도_급여_비중이_치솟지_않는다()
        {
            var w = Make(44556);

            for (int season = 0; season < 8; season++)
            {
                PlaySeason(w);
                SeasonRollover.StartNextSeason(w);
            }

            float avg = w.Clubs.Values.Average(c =>
                c.finances.currentWageSpend * 52f / Math.Max(1, c.finances.annualRevenue));

            // 계약 만료로 주급이 내려오는 길이 없으면 매 시즌 비중이 올라 결국 시장이 멈춘다.
            Assert.InRange(avg, 0.30f, BalanceConfig.WageSanctionRatio);
        }

        [Fact]
        public void 여러_시즌을_돌려도_이적_시장이_멈추지_않는다()
        {
            var w = Make(66778);
            int lastThree = 0;

            for (int season = 0; season < 8; season++)
            {
                int before = w.TotalTransfers;
                PlaySeason(w);
                SeasonRollover.StartNextSeason(w);
                if (season >= 5) lastThree += w.TotalTransfers - before;
            }

            Assert.True(lastThree > 20,
                $"마지막 세 시즌 이적이 {lastThree}건 — 시장이 말라붙었다");
        }

        [Theory]
        [InlineData(99887)]
        [InlineData(1000)]
        [InlineData(31415)]
        [InlineData(5150)]
        [InlineData(777)]
        public void 모든_선수는_소속이_있거나_자유계약이다(int seed)
        {
            // 한 시드로만 보면 안 된다. 명단과 소속이 어긋나는 것은 시드를 바꾸면
            // 숨었다가 다시 나타나는 종류의 결함이다.
            var w = Make(seed);

            for (int season = 0; season < 4; season++)
            {
                PlaySeason(w);
                SeasonRollover.StartNextSeason(w);
            }

            foreach (var p in w.Players.Values)
            {
                if (p.clubId == 0) continue;                  // 자유계약
                var club = w.Club(p.clubId);
                Assert.NotNull(club);
                Assert.Contains(p.id, club!.playerIds);
            }

            // 구단 명단에 없는 선수가 끼어 있으면 안 된다
            foreach (var club in w.Clubs.Values)
                foreach (int id in club.playerIds)
                    Assert.Equal(club.id, w.Player(id)?.clubId);
        }
    }
}
