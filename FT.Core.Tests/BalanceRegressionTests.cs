using System;
using System.Collections.Generic;
using System.Linq;
using FootballTactician.Balance;
using FootballTactician.Data;
using FootballTactician.Simulation;
using FootballTactician.World;
using Xunit;

namespace FT.Core.Tests
{
    /// <summary>
    /// 밸런스 회귀 테스트. 수치를 건드렸을 때 범위를 벗어나면 빌드가 실패한다.
    /// 기준값의 출처는 FT.Sim 측정이다.
    /// </summary>
    public class BalanceRegressionTests
    {
        static readonly GameDate Today = new GameDate { year = 2026, month = 7, day = 1 };

        static readonly Position[] Shape =
        {
            Position.GK, Position.LB, Position.CB, Position.CB, Position.RB,
            Position.CDM, Position.CDM, Position.LW, Position.CAM, Position.RW, Position.ST
        };

        /// <summary>
        /// 게임이 실제로 만드는 스쿼드와 같은 구성이어야 한다.
        /// 주전에 18세가 섞이면 실현율이 낮아 전력이 평판과 어긋나고, 측정 기준이 흔들린다.
        /// </summary>
        static List<PlayerData> Squad(int seed, int clubId, float rep, int firstId)
        {
            var rng = new Random(seed);
            var list = new List<PlayerData>();
            for (int i = 0; i < 25; i++)
            {
                bool starter = i < 11;
                int age = starter
                    ? 23 + rng.Next(8)
                    : (rng.Next(2) == 0 ? 17 + rng.Next(6) : 29 + rng.Next(6));
                list.Add(PlayerFactory.Create(rng, firstId + i, clubId, rep,
                                              Shape[i % Shape.Length], age, starter, Today));
            }
            return list;
        }

        static float Ca(PlayerData p) => BalanceConfig.PowerToCa(p.AbilityScore);

        static MatchLineup Lineup(int clubId, List<PlayerData> squad)
        {
            // 게임과 동일한 배치 규칙 — LineupBuilder 한 곳에만 둔다.
            var tac = FormationDatabase.CreateTactics("4-2-3-1", "t");
            LineupBuilder.Assign(tac, squad);
            return new MatchLineup { clubId = clubId, tactics = tac };
        }

        // ── 매치 엔진 ─────────────────────────────────────────────────────

        [Fact]
        public void 동급_두_팀의_합계_득점이_기준_범위에_있다()
        {
            var (hg, ag, shots, homeWin) = EqualTeams(1200);
            double total = hg + ag;
            Assert.InRange(total, (double)BalanceConfig.GoalsTotalMin, BalanceConfig.GoalsTotalMax);
            Assert.InRange(hg, (double)BalanceConfig.GoalsPerTeamMin, BalanceConfig.GoalsPerTeamMax);
            Assert.InRange(ag, (double)BalanceConfig.GoalsPerTeamMin, BalanceConfig.GoalsPerTeamMax);
        }

        [Fact]
        public void 홈_어드밴티지가_실제로_작동한다()
        {
            var (hg, ag, _, homeWin) = EqualTeams(1200);
            Assert.True(hg > ag, $"홈 득점이 원정보다 높아야 한다. 홈 {hg:F2} 원정 {ag:F2}");
            Assert.InRange(homeWin, (double)BalanceConfig.HomeWinRateMin, BalanceConfig.HomeWinRateMax);
        }

        [Fact]
        public void 경기당_슈팅이_기준_범위에_있다()
        {
            var (_, _, shots, _) = EqualTeams(800);
            Assert.InRange(shots, (double)BalanceConfig.ShotsPerMatchMin, BalanceConfig.ShotsPerMatchMax);
        }

        static (double hg, double ag, double shots, double homeWin) EqualTeams(int n)
        {
            var a = Squad(777, 1, 1.0f, 1);
            var b = Squad(777, 2, 1.0f, 10001);
            var map = a.Concat(b).ToDictionary(p => p.id);
            Func<int, PlayerData> get = id => map.TryGetValue(id, out var p) ? p : null;

            double hg = 0, ag = 0, sh = 0; int hw = 0;
            for (int i = 0; i < n; i++)
            {
                var ctx = new MatchSimContext
                {
                    fixtureId = i,
                    home = Lineup(1, a),
                    away = Lineup(2, b),
                    seed = i * 7919 + 13
                };
                var r = MatchEngine.Simulate(ctx, get);
                hg += r.homeGoals; ag += r.awayGoals;
                sh += r.homeStats.shots + r.awayStats.shots;
                if (r.homeGoals > r.awayGoals) hw++;
            }
            return (hg / n, ag / n, sh / n, hw / (double)n);
        }

        // ── 성장 ──────────────────────────────────────────────────────────

        [Theory]
        [InlineData(18,  88, 160,  8f, 18f)]   // 유망주는 한 시즌에 크게 큰다
        [InlineData(24, 125, 150,  1f,  6f)]
        [InlineData(27, 140, 150, -1f,  3f)]
        [InlineData(35, 135, 145, -12f, -2f)]  // 노장은 하락한다
        public void 나이별_시즌_성장이_기준_범위에_있다(int age, float ca0, float pa, float min, float max)
        {
            float ca = ca0;
            for (int w = 0; w < BalanceConfig.WeeksPerSeason; w++)
                ca = Math.Clamp(ca + BalanceConfig.WeeklyGrowth(age, ca, pa, 0.9f, 0.9f, 1.1f, 1.0f), 20f, 200f);
            Assert.InRange(ca - ca0, min, max);
        }

        [Fact]
        public void 성장은_잠재력을_넘지_못한다()
        {
            float ca = 100f, pa = 120f;
            for (int season = 0; season < 20; season++)
                for (int w = 0; w < BalanceConfig.WeeksPerSeason; w++)
                    ca += BalanceConfig.WeeklyGrowth(22, ca, pa, 1.7f, 1.3f, 1.4f, 1.2f);
            Assert.True(ca <= pa + 0.5f, $"CA {ca:F1} 가 PA {pa} 를 넘었다");
        }

        [Fact]
        public void 주간_성장_상한이_지켜진다()
        {
            float w = BalanceConfig.WeeklyGrowth(17, 40f, 200f, 1.7f, 1.3f, 1.4f, 1.2f);
            Assert.True(w <= BalanceConfig.GrowthWeeklyCap + 1e-4f, $"주간 {w:F3} 가 상한을 넘었다");
        }

        // ── 가치와 재정 ───────────────────────────────────────────────────

        [Fact]
        public void 가치는_CA에_대해_단조증가한다()
        {
            float prev = 0;
            for (int ca = 50; ca <= 190; ca += 10)
            {
                float v = BalanceConfig.Value(ca, 25, 1.0f);
                Assert.True(v > prev, $"CA {ca} 에서 가치가 증가하지 않았다");
                prev = v;
            }
        }

        [Fact]
        public void 노장은_같은_CA의_전성기보다_싸다()
        {
            Assert.True(BalanceConfig.Value(150, 33, 1.0f) < BalanceConfig.Value(150, 25, 1.0f));
        }

        [Theory]
        [InlineData(1.35f)]
        [InlineData(1.15f)]
        [InlineData(1.00f)]
        [InlineData(0.90f)]
        [InlineData(0.80f)]
        public void 급여_비중이_기준_범위에_있다(float rep)
        {
            var squad = Squad(11, 1, rep, 1);
            float wages = squad.Sum(p => BalanceConfig.WeeklyWage(Ca(p), p.GetAge(Today), rep)) * 52f / 10000f;
            float ratio = wages / BalanceConfig.AnnualRevenue(rep);
            Assert.InRange(ratio, 0.15f, BalanceConfig.WageSanctionRatio);
        }

        [Fact]
        public void 수입은_평판에_대해_지수적으로_오른다()
        {
            float top = BalanceConfig.AnnualRevenue(1.35f);
            float bottom = BalanceConfig.AnnualRevenue(0.80f);
            Assert.InRange(top / bottom, 4.5f, 7.5f);
        }

        // ── 생성 ──────────────────────────────────────────────────────────

        [Fact]
        public void 생성된_선수의_CA는_PA를_넘지_않는다()
        {
            var rng = new Random(5);
            for (int i = 0; i < 400; i++)
            {
                int age = 16 + rng.Next(21);
                var p = PlayerFactory.Create(rng, i, 1, 1.0f, Position.CM, age, i % 2 == 0, Today);
                Assert.InRange(p.CurrentAbility, 1, 20);
            }
        }

        [Fact]
        public void 평판이_높은_구단이_더_강한_스쿼드를_가진다()
        {
            float Strength(float rep)
            {
                float sum = 0; int n = 0;
                for (int s = 0; s < 8; s++)       // 난수 편차를 줄이려고 여러 번 생성
                {
                    var sq = Squad(100 + s, 1, rep, 1);
                    sum += sq.OrderByDescending(Ca).Take(11).Average(Ca); n++;
                }
                return sum / n;
            }
            float top = Strength(1.35f), mid = Strength(1.0f), low = Strength(0.8f);
            Assert.True(top > mid, $"상위({top:F1}) 가 중위({mid:F1}) 보다 강해야 한다");
            Assert.True(mid > low, $"중위({mid:F1}) 가 하위({low:F1}) 보다 강해야 한다");
        }

        // ── 스케일 변환 ───────────────────────────────────────────────────

        [Fact]
        public void CA와_전력_변환은_가역이다()
        {
            for (float ca = 40; ca <= 190; ca += 15)
                Assert.Equal(ca, BalanceConfig.PowerToCa(BalanceConfig.CaToPower(ca)), 2);
        }
    }
}
