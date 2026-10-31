using System;
using System.Linq;
using FootballTactician.Balance;
using FootballTactician.Data;
using FootballTactician.Simulation;

namespace FootballTactician.World
{
    /// <summary>
    /// AI 구단의 영입 안목.
    ///
    /// 지금까지 AI 는 능력치가 높은 선수를 집었다. 그러면 전술 색깔을 줘 놓고도
    /// 고압박 팀이 느린 선수를, 점유 팀이 패스를 못 하는 선수를 사 온다.
    ///
    /// 이제 <b>자기 시스템에 맞는 선수</b>를 본다. 다만 구단 수준만큼만 잘 본다 —
    /// 모든 AI 가 완벽하게 영입하면 감독이 시장에서 앞설 여지가 없다.
    /// </summary>
    public static class AiRecruiting
    {
        /// <summary>
        /// 지시에서 전술 색깔을 되읽는다. 색깔 자체는 저장하지 않는다 —
        /// 지시가 곧 색깔이고, 따로 저장하면 둘이 어긋날 수 있다.
        /// </summary>
        public static AiTactics.Style StyleOf(TeamInstructions ins)
        {
            if (ins == null) return AiTactics.Style.Balanced;

            if (ins.pressingIntensity >= 5 && ins.defensiveLine == DefensiveLine.VeryHigh)
                return AiTactics.Style.HighPress;

            if (ins.directCounterAttack && ins.defensiveLine is DefensiveLine.Low or DefensiveLine.VeryLow)
                return AiTactics.Style.LowBlockCounter;

            if (ins.buildUpStyle == BuildUpStyle.ShortPassing)
                return AiTactics.Style.Possession;

            if (ins.buildUpStyle == BuildUpStyle.LongBall)
                return AiTactics.Style.DirectAttack;

            return AiTactics.Style.Balanced;
        }

        /// <summary>
        /// 전술 색깔이 요구하는 쪽으로 그 선수가 얼마나 <b>치우쳤는지</b>. 1.0 이 평범함.
        /// 고압박은 활동량과 체력을, 저블록 역습은 속도를, 점유는 패스와 시야를 본다.
        ///
        /// 절대 수준을 재면 안 된다. 잘하는 선수는 모든 능력치가 높아 어떤 색깔에도 맞게 나오고,
        /// 그러면 적합도가 CA 와 같이 움직여 영입 결정을 전혀 바꾸지 못한다.
        /// 실제로 그렇게 뒀을 때 안목 0 과 1 이 같은 선수를 골랐다.
        /// (역할 적합도에서 이미 같은 실수를 한 번 했다 — RoleFit 참고.)
        /// </summary>
        public static float StyleFit(PlayerData p, AiTactics.Style style)
        {
            if (p?.mental == null || p.physical == null || p.technical == null) return 1f;
            if (style == AiTactics.Style.Balanced) return 1f;

            float key = style switch
            {
                AiTactics.Style.HighPress =>
                    (p.mental.workRate * 1.3f + p.physical.stamina + p.mental.aggression) / 3.3f,

                AiTactics.Style.LowBlockCounter =>
                    (p.physical.speed * 1.3f + p.physical.acceleration + p.mental.positioning) / 3.3f,

                AiTactics.Style.Possession =>
                    (p.technical.passing * 1.3f + p.mental.vision + p.technical.firstTouch) / 3.3f,

                AiTactics.Style.DirectAttack =>
                    (p.technical.heading * 1.2f + p.physical.strength + p.physical.jumping) / 3.2f,

                _ => 0f,
            };

            // 본인 평균과 견준다. 핵심 능력치가 평균보다 높을수록 그 색깔에 맞는 선수다.
            float overall = (p.technical.Average() + p.physical.Average() + p.mental.Average()) / 3f;
            if (overall <= 0f) return 1f;

            return Math.Clamp(
                1f + (key - overall) * BalanceConfig.StyleFitPerPoint,
                1f - BalanceConfig.StyleFitCap, 1f + BalanceConfig.StyleFitCap);
        }

        /// <summary>
        /// 그 구단이 그 자리에 쓰는 역할에 이 선수가 맞는지. 1.0 이 평범함.
        /// 역할을 지정하지 않았으면 보정하지 않는다.
        /// </summary>
        public static float RoleFitFor(ClubData buyer, PlayerData p, Position pos)
        {
            var tac = buyer.savedTactics?.FirstOrDefault(t => t.id == buyer.activeTacticsId);
            var slot = tac?.slots.FirstOrDefault(s => (s.position & pos) != 0);

            if (slot?.role == null || !RoleDatabase.All.TryGetValue(slot.role.role, out var def))
                return 1f;

            return RoleFit.Multiplier(p, def);
        }

        /// <summary>
        /// 영입 점수. 실력에 시스템 적합도를 곱한다.
        ///
        /// <paramref name="skill"/> 은 그 구단의 안목(0~1)이다. 0 이면 예전처럼 실력만 본다.
        /// </summary>
        public static float Score(GameWorld w, ClubData buyer, PlayerData p, Position pos, float skill)
        {
            float ca = GameWorld.Ca(p);
            if (skill <= 0f) return ca;

            var style = StyleOf(buyer.savedTactics?
                .FirstOrDefault(t => t.id == buyer.activeTacticsId)?.instructions);

            float fit = StyleFit(p, style) * RoleFitFor(buyer, p, pos);

            // 안목만큼만 적합도를 반영한다. 약팀은 그냥 잘하는 선수를 집는다.
            return ca * (1f + (fit - 1f) * skill);
        }

        /// <summary>구단의 영입 안목. 평판이 높을수록 시스템에 맞춰 뽑는다.</summary>
        public static float SkillOf(ClubData club)
            => Math.Clamp(BalanceConfig.RecruitSkillBase
                          + club.reputation * BalanceConfig.RecruitSkillPerStar, 0f, 1f);
    }
}
