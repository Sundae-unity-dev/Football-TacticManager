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

            // 2부는 1부보다 라운드가 많다(24팀 46라운드). 내 시즌이 끝난 시점의
            // 순위표는 아직 덜 치른 표이므로, 남은 경기를 마친 뒤에 봐야
            // "누가 올라가는가"를 맞게 집을 수 있다.
            SeasonRollover.FinishRemainingFixtures(w);

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
            // 팀 수는 리그마다 다르다(잉글랜드 1부 20 · 2부 24). 승강으로 자리가
            // 바뀌어도 **각 리그의 팀 수는 그대로**여야 한다 — 올라간 만큼 내려간다.
            Assert.Equal(top.numberOfTeams, top.clubIds.Count);
            Assert.Equal(below.numberOfTeams, below.clubIds.Count);
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

        [Fact]
        public void 승강_자리는_나라마다_다르다()
        {
            // 리그의 모양은 그 나라의 성격이다 — 브라질은 네 팀이 내려가고,
            // 벨기에는 한 팀이 내려간다(docs/리그-제도.md).
            var w = WorldGenerator.Create(4242, nations: new[]
            {
                NameForge.ByCode("BRA"), NameForge.ByCode("BEL"), NameForge.ByCode("USA"),
            });

            Assert.Equal(4, TopLeague(w, "BRA").relegationSpots);
            Assert.Equal(1, TopLeague(w, "BEL").relegationSpots);
            Assert.Equal(0, TopLeague(w, "USA").relegationSpots);
        }

        [Fact]
        public void 닫힌_리그에서는_아무도_오가지_않는다()
        {
            // 미국·호주·인도·멕시코는 프랜차이즈 구조라 승강이 없다.
            // 시즌을 넘겨도 구단의 소속이 그대로여야 한다.
            var w = WorldGenerator.Create(515, nations: new[] { NameForge.ByCode("USA") });
            var club = w.Clubs.Values.First(c => c.reputation >= 3);
            ManagerCareer.TakeCharge(w, club.id, "테스트");

            var before = w.Clubs.Values.ToDictionary(c => c.id, c => c.leagueId);

            // 가드 없이 돌리면 시즌이 안 끝나는 순간 테스트가 멈춘다.
            // 미국 1부는 29팀(홀수)이라 58라운드까지 간다 — 넉넉히 잡는다.
            int guard = 0;
            while (!w.SeasonFinished && guard++ < 80) GameEngine.PlayNextRound(w);
            Assert.True(w.SeasonFinished, $"{guard}라운드를 돌려도 시즌이 끝나지 않았다");

            SeasonRollover.StartNextSeason(w);

            foreach (var c in w.Clubs.Values)
                Assert.Equal(before[c.id], c.leagueId);
        }

        [Fact]
        public void 닫힌_리그에서는_잔류가_목표로_나오지_않는다()
        {
            // 내려갈 곳이 없는데 잔류를 목표로 받으면 뜻이 없다.
            // 그렇다고 목표를 아예 안 주면 이사회가 리그에 무관심해진다 —
            // 느슨한 순위 목표로 바뀌어야 한다.
            var w = WorldGenerator.Create(909, nations: new[] { NameForge.ByCode("USA") });
            var club = w.Clubs.Values.First(c => c.reputation == 1);

            var goals = BoardObjectives.GenerateFor(w, club.id);
            Assert.DoesNotContain(goals, o => o.type == BoardObjectiveType.AvoidRelegation);
            Assert.Contains(goals, o => o.type == BoardObjectiveType.LeagueFinish);
        }

        /// <summary>그 나라의 1부.</summary>
        static LeagueData TopLeague(GameWorld w, string code)
        {
            var nation = w.Nations.First(n => n.Code == code);
            return w.League(nation.LeagueIds[0])!;
        }

        [Fact]
        public void 승격_플레이오프가_마지막_한_자리를_가린다()
        {
            // 잉글랜드 2부는 1·2위가 자동 승격하고 3~6위가 한 자리를 다툰다.
            // 그러니 세 번째로 올라간 팀은 3위가 아닐 수 있다.
            var w = Season(4242, "ENG");

            var top = TopLeague(w, "ENG");
            var below = w.League(w.Nations.First(n => n.Code == "ENG").LeagueIds[1])!;

            var table = below.currentSeason.standings
                .OrderBy(e => e.position).Select(e => e.clubId).ToList();

            SeasonRollover.StartNextSeason(w);

            var rose = table.Select((id, i) => (id, pos: i + 1))
                            .Where(x => top.clubIds.Contains(x.id))
                            .Select(x => x.pos).OrderBy(x => x).ToList();

            Assert.Equal(3, rose.Count);                 // 자리 셋
            Assert.Equal(new[] { 1, 2 }, rose.Take(2));  // 두 자리는 순위대로
            Assert.InRange(rose[2], 3, 6);               // 나머지 한 자리는 3~6위 중에서
        }

        [Fact]
        public void 승강_플레이오프는_자리를_한_쌍_더_만들_수_있다()
        {
            // 독일은 1부 16위와 2부 3위가 직접 맞붙는다. 2부 팀이 이기면
            // 그 해에만 오가는 팀이 하나 늘어난다 - 올라간 수와 내려간 수는 그래도 같다.
            var w = Season(515, "GER");
            var nation = w.Nations.First(n => n.Code == "GER");
            var top = w.League(nation.LeagueIds[0])!;
            var below = w.League(nation.LeagueIds[1])!;

            int topSize = top.clubIds.Count;
            int belowSize = below.clubIds.Count;

            var table = below.currentSeason.standings
                .OrderBy(e => e.position).Select(e => e.clubId).ToList();

            SeasonRollover.StartNextSeason(w);

            var rose = table.Select((id, i) => (id, pos: i + 1))
                            .Where(x => top.clubIds.Contains(x.id))
                            .Select(x => x.pos).OrderBy(x => x).ToList();

            // 자동 두 자리는 순위대로, 그리고 3위가 더 올라왔을 수도 있다.
            Assert.Equal(new[] { 1, 2 }, rose.Take(2));
            Assert.InRange(rose.Count, 2, 3);
            if (rose.Count == 3) Assert.Equal(3, rose[2]);

            // 어느 쪽이든 리그 크기는 그대로다.
            Assert.Equal(topSize, top.clubIds.Count);
            Assert.Equal(belowSize, below.clubIds.Count);
        }

        [Fact]
        public void 플레이오프가_있어도_리그_크기는_유지된다()
        {
            // 승격 플레이오프(잉글랜드·이탈리아)와 승강 플레이오프(독일·한국)를
            // 한 세계에 섞어 놓고 여러 시즌을 넘겨 본다.
            var w = Season(909, "ENG", "ITA", "GER", "KOR");

            var sizes = w.Leagues.Values.ToDictionary(l => l.id, l => l.clubIds.Count);

            for (int s = 0; s < 3; s++)
            {
                SeasonRollover.StartNextSeason(w);

                int guard = 0;
                while (!w.SeasonFinished && guard++ < 80) GameEngine.PlayNextRound(w);
                SeasonRollover.FinishRemainingFixtures(w);
            }

            foreach (var (id, n) in sizes)
                Assert.Equal(n, w.League(id)!.clubIds.Count);
        }

        /// <summary>그 나라들로 세계를 만들고 한 시즌을 끝까지 치른다.</summary>
        static GameWorld Season(int seed, params string[] codes)
        {
            var w = WorldGenerator.Create(seed,
                nations: codes.Select(NameForge.ByCode).ToList());

            var club = w.Clubs.Values.First(c => c.reputation == 3);
            ManagerCareer.TakeCharge(w, club.id, "테스트");

            int guard = 0;
            while (!w.SeasonFinished && guard++ < 80) GameEngine.PlayNextRound(w);
            Assert.True(w.SeasonFinished, $"{guard}라운드를 돌려도 시즌이 끝나지 않았다");

            // 아래 리그는 라운드가 더 많다. 남은 경기를 마쳐야 순위표가 완성된다.
            SeasonRollover.FinishRemainingFixtures(w);
            return w;
        }
    }
}
