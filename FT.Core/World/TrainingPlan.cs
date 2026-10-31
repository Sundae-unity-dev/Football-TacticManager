using System;
using System.Text.Json.Serialization;
using System.Collections.Generic;
using FootballTactician.Data;

namespace FootballTactician.World
{
    /// <summary>
    /// 주간 훈련 지시. 감독이 성장 방향과 강도를 정한다.
    ///
    /// 공짜 선택이면 안 된다. 강하게 굴리면 빨리 크지만 컨디션이 떨어지고 부상이 는다.
    /// 회복에 치중하면 몸은 가벼워도 성장이 더디다.
    /// </summary>
    [Serializable]
    public sealed class TrainingPlan
    {
        /// <summary>1(가벼움) ~ 5(혹독함).</summary>
        public int Intensity = 3;

        /// <summary>무엇에 비중을 둘지.</summary>
        public TrainingFocus Focus = TrainingFocus.Balanced;

        /// <summary>유스에 출전 시간을 몰아줄지. 성장은 빠르지만 당장의 전력은 떨어진다.</summary>
        public bool YouthPriority;

        // ── 효과 ──────────────────────────────────────────────────────────

        /// <summary>성장 배수. 강도가 높을수록 빨리 큰다.</summary>
        [JsonIgnore] public float GrowthFactor => Intensity switch
        {
            1 => 0.62f,
            2 => 0.80f,
            3 => 1.00f,
            4 => 1.22f,
            _ => 1.45f,
        };

        /// <summary>주간 컨디션 회복에 곱한다. 혹독하게 굴리면 덜 회복된다.</summary>
        [JsonIgnore] public float RecoveryFactor => Intensity switch
        {
            1 => 1.30f,
            2 => 1.15f,
            3 => 1.00f,
            4 => 0.84f,
            _ => 0.68f,
        };

        /// <summary>주간 부상 확률에 곱한다.</summary>
        [JsonIgnore] public float InjuryFactor => Intensity switch
        {
            1 => 0.60f,
            2 => 0.80f,
            3 => 1.00f,
            4 => 1.30f,
            _ => 1.70f,
        };

        /// <summary>그 선수에게 적용할 성장 배수. 중점에 맞는 선수가 더 큰다.</summary>
        public float FocusBonusFor(PlayerData p, int age)
        {
            bool gk = (p.positionFlags & Position.GK) != 0;
            bool attacker = (p.positionFlags &
                (Position.ST | Position.CF | Position.CAM | Position.LW | Position.RW)) != 0;
            bool defender = (p.positionFlags &
                (Position.CB | Position.LB | Position.RB | Position.CDM)) != 0;

            return Focus switch
            {
                TrainingFocus.Attacking => attacker ? 1.30f : 0.85f,
                TrainingFocus.Defending => defender ? 1.30f : 0.85f,
                TrainingFocus.Physical => gk ? 0.90f : 1.15f,
                TrainingFocus.Youth => age <= 21 ? 1.60f : 0.78f,
                TrainingFocus.Goalkeeping => gk ? 1.70f : 0.90f,
                _ => 1.00f,
            };
        }

        public static string IntensityLabel(int intensity) => intensity switch
        {
            1 => "매우 가볍게",
            2 => "가볍게",
            3 => "보통",
            4 => "강하게",
            _ => "혹독하게",
        };

        public static string FocusLabel(TrainingFocus f) => f switch
        {
            TrainingFocus.Balanced => "균형",
            TrainingFocus.Attacking => "공격",
            TrainingFocus.Defending => "수비",
            TrainingFocus.Physical => "체력",
            TrainingFocus.Youth => "유스 육성",
            _ => "골키퍼",
        };
    }

    public enum TrainingFocus { Balanced, Attacking, Defending, Physical, Youth, Goalkeeping }
}
