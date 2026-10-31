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
    /// AI 영입 안목. 구단이 자기 시스템에 맞는 선수를 봐야 전술 색깔이 의미를 갖는다.
    /// </summary>
    public class RecruitingTests
    {
        static PlayerData Flat(int value)
        {
            var p = new PlayerData
            {
                positionFlags = Position.CM,
                technical = new TechnicalAttributes(),
                physical = new PhysicalAttributes(),
                mental = new MentalAttributes(),
                goalkeeper = new GoalkeeperAttributes(),
            };
            foreach (var f in typeof(TechnicalAttributes).GetFields()) f.SetValue(p.technical, value);
            foreach (var f in typeof(PhysicalAttributes).GetFields()) f.SetValue(p.physical, value);
            foreach (var f in typeof(MentalAttributes).GetFields()) f.SetValue(p.mental, value);
            return p;
        }

        // ── 색깔 되읽기 ───────────────────────────────────────────────────

        [Fact]
        public void 지시에서_전술_색깔을_되읽는다()
        {
            // 색깔은 저장하지 않는다. 지시가 곧 색깔이라 둘이 어긋날 수 없어야 한다.
            foreach (var style in Enum.GetValues<AiTactics.Style>())
            {
                var ins = new TeamInstructions();
                AiTactics.Apply(ins, style);
                Assert.Equal(style, AiRecruiting.StyleOf(ins));
            }
        }

        [Fact]
        public void 세계의_구단들이_여러_색깔을_갖는다()
        {
            var w = WorldGenerator.Create(31415);

            var styles = w.Clubs.Values
                .Select(c => AiRecruiting.StyleOf(
                    c.savedTactics!.First(t => t.id == c.activeTacticsId).instructions))
                .ToList();

            Assert.True(styles.Distinct().Count() >= 4);
        }

        // ── 적합도는 치우침 ───────────────────────────────────────────────

        [Fact]
        public void 색깔_적합도는_총합이_아니라_치우침을_본다()
        {
            // 절대 수준을 재면 잘하는 선수가 모든 색깔에 맞게 나와 영입 결정을 못 바꾼다.
            // 역할 적합도에서 이미 한 번 겪은 실수다.
            var weak = Flat(8);
            var strong = Flat(17);

            // 전체를 똑같이 올린 선수는 적합도가 같아야 한다.
            foreach (var style in Enum.GetValues<AiTactics.Style>())
                Assert.Equal(AiRecruiting.StyleFit(weak, style),
                             AiRecruiting.StyleFit(strong, style), 3);
        }

        [Fact]
        public void 색깔이_요구하는_쪽으로_치우치면_적합도가_오른다()
        {
            var flat = Flat(12);

            var runner = Flat(12);
            runner.mental.workRate = 19;
            runner.physical.stamina = 19;
            Assert.True(AiRecruiting.StyleFit(runner, AiTactics.Style.HighPress)
                      > AiRecruiting.StyleFit(flat, AiTactics.Style.HighPress));

            var passer = Flat(12);
            passer.technical.passing = 19;
            passer.mental.vision = 19;
            Assert.True(AiRecruiting.StyleFit(passer, AiTactics.Style.Possession)
                      > AiRecruiting.StyleFit(flat, AiTactics.Style.Possession));

            // 다른 색깔에는 오히려 처진다 — 치우침은 공짜가 아니다.
            Assert.True(AiRecruiting.StyleFit(passer, AiTactics.Style.HighPress)
                      < AiRecruiting.StyleFit(flat, AiTactics.Style.HighPress));
        }

        [Fact]
        public void 적합도는_정해진_폭_안에_있다()
        {
            // 전술이 실력을 덮으면 선수를 모을 이유가 사라진다.
            var w = WorldGenerator.Create(31415);

            foreach (var p in w.Players.Values.Where(x => x.clubId > 0).Take(600))
            foreach (var style in Enum.GetValues<AiTactics.Style>())
                Assert.InRange(AiRecruiting.StyleFit(p, style),
                    1f - BalanceConfig.StyleFitCap - 0.001f,
                    1f + BalanceConfig.StyleFitCap + 0.001f);
        }

        [Fact]
        public void 적합도가_실력과_같이_움직이지_않는다()
        {
            // 상관이 높으면 적합도는 CA 를 한 번 더 세는 것일 뿐이라 선택을 바꾸지 못한다.
            var w = WorldGenerator.Create(31415);
            var pool = w.Players.Values
                .Where(p => p.clubId > 0 && (p.positionFlags & Position.CM) != 0)
                .Take(400)
                .ToList();

            var ca = pool.Select(p => (double)GameWorld.Ca(p)).ToList();
            var fit = pool.Select(p => (double)AiRecruiting.StyleFit(p, AiTactics.Style.Possession)).ToList();

            double mc = ca.Average(), mf = fit.Average();
            double cov = ca.Zip(fit, (a, b) => (a - mc) * (b - mf)).Sum();
            double sa = Math.Sqrt(ca.Sum(a => (a - mc) * (a - mc)));
            double sb = Math.Sqrt(fit.Sum(b => (b - mf) * (b - mf)));
            double r = sa * sb == 0 ? 0 : cov / (sa * sb);

            Assert.True(Math.Abs(r) < 0.35, $"적합도와 CA 의 상관이 {r:F2} — 사실상 같은 값이다");
        }

        // ── 안목 ──────────────────────────────────────────────────────────

        [Fact]
        public void 안목이_0이면_실력만_본다()
        {
            var w = WorldGenerator.Create(31415);
            var club = w.Clubs.Values.First();
            var p = w.Players.Values.First(x => x.clubId > 0 && (x.positionFlags & Position.CM) != 0);

            Assert.Equal(GameWorld.Ca(p), AiRecruiting.Score(w, club, p, Position.CM, 0f), 3);
        }

        [Fact]
        public void 큰_구단일수록_안목이_좋다()
        {
            var w = WorldGenerator.Create(31415);

            float top = w.Clubs.Values.Where(c => c.reputation == 5).Average(AiRecruiting.SkillOf);
            float bottom = w.Clubs.Values.Where(c => c.reputation == 1).Average(AiRecruiting.SkillOf);

            Assert.True(top > bottom);
            Assert.InRange(top, 0f, 1f);
            Assert.InRange(bottom, 0f, 1f);
        }

        [Fact]
        public void 안목이_실제로_선택을_바꾼다()
        {
            // 바꾸지 못하면 기능이 아니라 장식이다.
            var w = WorldGenerator.Create(31415);
            int changed = 0, total = 0;

            // 표본이 작으면 비율이 크게 흔들린다. 하네스와 같은 폭으로 본다.
            foreach (var club in w.Clubs.Values.Where(c => c.reputation >= 3).Take(80))
            foreach (var pos in new[] { Position.CB, Position.CM, Position.ST, Position.LW })
            {
                var pool = w.Players.Values
                    .Where(p => p.clubId > 0 && p.clubId != club.id && (p.positionFlags & pos) != 0)
                    .OrderByDescending(GameWorld.Ca)
                    .Take(10)
                    .ToList();
                if (pool.Count < 2) continue;

                total++;
                var byCa = pool[0];
                var byScore = pool
                    .OrderByDescending(p => AiRecruiting.Score(w, club, p, pos, AiRecruiting.SkillOf(club)))
                    .First();

                if (byCa != byScore) changed++;
            }

            Assert.True(total > 100);

            // 실력이 주도하고 적합도는 비슷한 후보를 가른다.
            // 0 이면 장식이고, 절반을 넘으면 전술이 실력을 덮은 것이다.
            Assert.InRange(changed / (double)total, 0.05, 0.45);
        }

        [Fact]
        public void 실력_차가_크면_적합도가_뒤집지_못한다()
        {
            // 전술이 실력을 덮으면 약한 선수를 모으는 것이 정답이 된다.
            var w = WorldGenerator.Create(31415);
            var club = w.Clubs.Values.First(c => c.reputation == 5);

            var star = Flat(18);
            var misfit = Flat(10);
            misfit.technical.passing = 20;
            misfit.mental.vision = 20;
            misfit.technical.firstTouch = 20;

            Assert.True(AiRecruiting.Score(w, club, star, Position.CM, 1f)
                      > AiRecruiting.Score(w, club, misfit, Position.CM, 1f));
        }

        [Fact]
        public void 안목이_좋을수록_색깔에_맞는_선수를_고른다()
        {
            // 스쿼드 평균으로 재면 안 된다. 25명을 평균 내면 색깔 적합도의 폭(±15%)이
            // 1% 로 눌리고, 은퇴와 인원 보충이 물갈이하는 폭에 통째로 묻힌다.
            // 실제로 그렇게 쟀을 때 5성 1.0072 대 1성 1.0096 으로 뒤집혔다.
            //
            // 재야 할 것은 **고른 선수**다. 같은 후보군을 놓고 안목만 바꿔 본다.
            var w = WorldGenerator.Create(31415);

            float sumSkilled = 0f, sumBlind = 0f;
            int cases = 0;

            foreach (var club in w.Clubs.Values.Where(c => c.reputation >= 3).Take(60))
            {
                var tac = club.savedTactics!.First(t => t.id == club.activeTacticsId);
                var style = AiRecruiting.StyleOf(tac.instructions);
                if (style == AiTactics.Style.Balanced) continue;   // 색깔이 없으면 고를 이유도 없다

                // 실제 영입처럼 비슷한 수준의 후보를 모은다. 아무나 열 명 뽑으면
                // CA 격차가 적합도 폭을 압도해 안목이 끼어들 틈이 없다.
                float level = w.Squad(club.id).Average(GameWorld.Ca);
                var pool = w.Players.Values
                    .Where(p => p.clubId > 0 && p.clubId != club.id)
                    .Where(p => (p.positionFlags & Position.CM) != 0)
                    .Where(p => Math.Abs(GameWorld.Ca(p) - level) < 8f)
                    .OrderBy(p => (p.id * 7919) % 1000)
                    .Take(10)
                    .ToList();
                if (pool.Count < 10) continue;

                PlayerData Pick(float skill) => pool
                    .OrderByDescending(p => AiRecruiting.Score(w, club, p, Position.CM, skill))
                    .First();

                sumSkilled += AiRecruiting.StyleFit(Pick(1f), style);
                sumBlind += AiRecruiting.StyleFit(Pick(0f), style);
                cases++;
            }

            Assert.True(cases >= 20, $"견줄 사례가 {cases}개뿐이다");
            Assert.True(sumSkilled / cases > sumBlind / cases,
                $"안목 있는 선택({sumSkilled / cases:F4})이 눈먼 선택({sumBlind / cases:F4})보다 색깔에 맞지 않는다");
        }
    }
}
