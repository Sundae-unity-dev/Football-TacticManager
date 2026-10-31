using System;
using FootballTactician.Data;
using FootballTactician.Balance;

namespace FootballTactician.Simulation
{
    /// <summary>
    /// 팀 지시를 경기에 들어가는 보정값으로 옮긴다.
    ///
    /// 원칙 하나: <b>모든 지시는 거래여야 한다.</b> 공짜로 좋아지는 설정이 있으면
    /// 그것만 고르면 되고, 나머지 지시는 화면 장식이 된다.
    /// 그래서 값을 올리는 항목에는 반드시 내려가는 항목을 함께 둔다.
    ///
    /// 상대와의 궁합(압박 대 빌드업, 역습 대 수비 라인, 폭 대 블록)은
    /// <see cref="Resolve"/> 에서 두 팀을 같이 보고 정한다.
    /// </summary>
    public struct TacticalProfile
    {
        /// <summary>공격 시도 확률에 더한다. 슈팅 수를 움직인다.</summary>
        public float attackRateBonus;

        /// <summary>찬스의 질(xG)에 곱한다.</summary>
        public float qualityMul;

        /// <summary>빅찬스가 나올 확률에 곱한다.</summary>
        public float bigChanceMul;

        /// <summary>점유율에 더한다.</summary>
        public float possessionBonus;

        /// <summary>5분 블록마다 깎이는 체력. 경기 후반에 효과가 드러난다.</summary>
        public float fatiguePerBlock;

        /// <summary>쌓인 피로(0~1)가 경기력에 곱해지는 값.</summary>
        public static float FatigueFactor(float fatigue)
            => 1f - Math.Clamp(fatigue, 0f, 1f) * BalanceConfig.InMatchFatigueSwing;

        /// <summary>상대의 압박을 얼마나 견디는지. 0 = 그대로 당함, 1 = 영향 없음.</summary>
        public float pressResistance;

        public static TacticalProfile Neutral => new()
        {
            qualityMul = 1f,
            bigChanceMul = 1f,
            pressResistance = 0.5f,
        };

        // ── 단독 효과 ──────────────────────────────────────────────────────

        /// <summary>상대를 보지 않고 정해지는 부분.</summary>
        public static TacticalProfile FromInstructions(TeamInstructions ins)
        {
            var p = Neutral;

            // 수비 라인 — 높이면 중원을 압축해 공을 더 쥐지만 뒷공간을 내준다.
            // 내리면 반대다. 어느 쪽도 공짜가 아니다.
            var (linePoss, lineQuality) = ins.defensiveLine switch
            {
                DefensiveLine.VeryLow => (-BalanceConfig.LinePossessionStep * 2f, 1f - BalanceConfig.LineQualityStep * 2f),
                DefensiveLine.Low => (-BalanceConfig.LinePossessionStep, 1f - BalanceConfig.LineQualityStep),
                DefensiveLine.High => (BalanceConfig.LinePossessionStep, 1f + BalanceConfig.LineQualityStep),
                DefensiveLine.VeryHigh => (BalanceConfig.LinePossessionStep * 2f, 1f + BalanceConfig.LineQualityStep * 2f),
                _ => (0f, 1f),
            };
            p.possessionBonus += linePoss;
            p.qualityMul *= lineQuality;

            // 템포 — 빠르면 많이 때리고 적게 넣는다. 느리면 반대다.
            int tempo = Math.Clamp(ins.tempo, 1, 5) - 3;
            p.attackRateBonus += tempo * BalanceConfig.TempoShotStep;
            p.qualityMul *= 1f - tempo * BalanceConfig.TempoQualityStep;
            p.fatiguePerBlock += Math.Max(0, tempo) * BalanceConfig.TempoFatigueStep;

            // 압박 — 공은 더 쥐지만 다리가 먼저 지친다.
            int press = Math.Clamp(ins.pressingIntensity, 1, 5) - 3;
            p.fatiguePerBlock += Math.Max(0, press) * BalanceConfig.PressFatigueStep;

            if (ins.counterPress)
            {
                p.possessionBonus += BalanceConfig.CounterPressPossession;
                p.fatiguePerBlock += BalanceConfig.CounterPressFatigue;
            }

            // 빌드업 — 짧게 돌리면 공을 쥐지만 압박에 약하고,
            // 길게 차면 압박을 넘기지만 공을 내준다.
            switch (ins.buildUpStyle)
            {
                case BuildUpStyle.ShortPassing:
                    p.possessionBonus += BalanceConfig.ShortPassPossession;
                    p.qualityMul *= 1f + BalanceConfig.ShortPassQuality;
                    p.pressResistance = 0.20f;
                    break;

                case BuildUpStyle.LongBall:
                    p.possessionBonus -= BalanceConfig.LongBallPossession;
                    p.qualityMul *= 1f - BalanceConfig.LongBallQuality;
                    p.bigChanceMul *= 1f + BalanceConfig.LongBallBigChance;
                    p.pressResistance = 0.90f;
                    break;

                case BuildUpStyle.Direct:
                    p.possessionBonus -= BalanceConfig.DirectPossession;
                    p.attackRateBonus += BalanceConfig.DirectShotRate;
                    p.qualityMul *= 1f - BalanceConfig.DirectQuality;
                    p.bigChanceMul *= 1f + BalanceConfig.DirectBigChance;
                    p.pressResistance = 0.65f;
                    break;
            }

            // 오프사이드 트랩은 상대의 빅찬스에 걸리는 효과라 Resolve 에서 다룬다.
            return p;
        }

        /// <summary>
        /// 오프사이드 트랩의 성패. 수비수의 위치 선정과 예측으로 가른다.
        /// 20 만점 평균이 13 쯤이면 본전이고, 그 아래면 손해다.
        /// </summary>
        public static float TrapSkill(PlayerData[] starters)
        {
            float sum = 0f;
            int n = 0;

            foreach (var p in starters)
            {
                if (p?.mental == null) continue;
                if ((p.positionFlags & (Position.CB | Position.LB | Position.RB)) == 0) continue;

                sum += (p.mental.positioning + p.mental.decisions + p.mental.concentration) / 3f;
                n++;
            }

            return n == 0 ? 10f : sum / n;
        }

        // ── 궁합 ──────────────────────────────────────────────────────────

        /// <summary>
        /// 두 팀을 같이 보고 상성을 더한다.
        ///
        /// 여기가 전술의 핵심이다. 단독 효과만 있으면 가장 센 설정 하나로 수렴하지만,
        /// 상대에 따라 유불리가 뒤집히면 매 경기 고를 이유가 생긴다.
        /// </summary>
        /// <param name="foeTrapSkill">상대 수비진의 트랩 숙련. 상대가 트랩을 걸 때만 쓰인다.</param>
        public static void Resolve(
            ref TacticalProfile self, TeamInstructions selfIns,
            TeamInstructions foeIns, float foeTrapSkill)
        {
            // 내려선 상대는 내 슈팅 자체를 줄인다.
            //
            // 이게 없으면 수비적 설정은 내 슈팅만 깎고 상대는 그대로라 순수한 손해가 된다.
            // 막는 쪽이 막는 값을 해야 수비를 고를 이유가 생긴다.
            self.attackRateBonus -= foeIns.mentality switch
            {
                TeamMentality.VeryDefensive => BalanceConfig.DefensiveShotDenial * 2f,
                TeamMentality.Defensive => BalanceConfig.DefensiveShotDenial,
                _ => 0f,
            };
            self.attackRateBonus -= foeIns.defensiveLine switch
            {
                DefensiveLine.VeryLow => BalanceConfig.LowLineShotDenial * 2f,
                DefensiveLine.Low => BalanceConfig.LowLineShotDenial,
                _ => 0f,
            };

            // 상대 압박 대 내 빌드업 — 견디지 못하면 찬스의 질이 깎인다.
            float foePress = (Math.Clamp(foeIns.pressingIntensity, 1, 5) - 3) / 2f;
            if (foeIns.counterPress) foePress += 0.3f;
            if (foePress > 0)
                self.qualityMul *= 1f - foePress * (1f - self.pressResistance) * BalanceConfig.PressDisruption;

            // 내 역습 대 상대의 높은 라인 — 뒷공간이 열려 있을 때만 값어치가 있다.
            if (selfIns.directCounterAttack)
            {
                bool foeHigh = foeIns.defensiveLine is DefensiveLine.High or DefensiveLine.VeryHigh;
                self.bigChanceMul *= foeHigh
                    ? 1f + BalanceConfig.CounterVsHighLine
                    : 1f - BalanceConfig.CounterWasted;
            }

            // 내 폭 대 상대 블록 — 내려선 상대는 넓게 벌려야 흔들리고,
            // 올라온 상대는 좁혀서 사이를 찔러야 한다.
            int width = Math.Clamp(selfIns.width, 1, 5) - 3;
            int foeLine = foeIns.defensiveLine switch
            {
                DefensiveLine.VeryLow => -2,
                DefensiveLine.Low => -1,
                DefensiveLine.High => 1,
                DefensiveLine.VeryHigh => 2,
                _ => 0,
            };
            // 상대가 내려섰으면(foeLine<0) 넓은 쪽이, 올라왔으면 좁은 쪽이 맞는다.
            self.qualityMul *= 1f + width * -foeLine * BalanceConfig.WidthMatchStep;

            // 상대의 높은 라인은 내 빅찬스를 늘린다. 트랩을 걸면 줄지만, 수비수가 못 맞추면 되레 는다.
            int lineRisk = foeIns.defensiveLine switch
            {
                DefensiveLine.VeryLow => -2,
                DefensiveLine.Low => -1,
                DefensiveLine.High => 1,
                DefensiveLine.VeryHigh => 2,
                _ => 0,
            };
            self.bigChanceMul *= 1f + lineRisk * BalanceConfig.LineBigChanceStep;

            if (foeIns.offsideTrap)
            {
                // 수비수 평균 13 이 본전. 잘 맞추면 내 빅찬스를 깎고, 못 맞추면 되레 열어 준다.
                float edge = (foeTrapSkill - BalanceConfig.TrapBreakEven) / BalanceConfig.TrapBreakEven;
                self.bigChanceMul *= 1f - edge * BalanceConfig.TrapSwing;
            }
        }
    }
}
