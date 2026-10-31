using System;
using System.Collections.Generic;
using FootballTactician.Balance;
using FootballTactician.Data;

namespace FootballTactician.Simulation
{
    /// <summary>
    /// 선수 개인 지시를 경기에 들어가는 값으로 옮긴다.
    ///
    /// 팀 지시가 팀 전체를 움직인다면 이쪽은 <b>한 선수가 무엇을 할지</b>를 정한다.
    /// 같은 원칙이 그대로다 — 모든 항목은 거래여야 하고, 아무것도 지정하지 않은
    /// 선수(= Default)는 중립이어야 한다. 지시를 쓰지 않는 쪽이 불리해지면 안 된다.
    ///
    /// 모델에는 20개 항목이 있지만 이 엔진이 실제로 구분할 수 있는 것만 연결한다.
    /// 구분하지 못하는 항목을 연결한 척하면 화면에서 움직여도 아무 일이 없다.
    /// </summary>
    public struct PlayerInstructionEffect
    {
        /// <summary>이 선수가 슈팅을 가져갈 가중치 배수.</summary>
        public float shotShare;

        /// <summary>이 선수가 때린 슈팅의 질 배수.</summary>
        public float shotQuality;

        /// <summary>찬스를 만드는 가중치 배수.</summary>
        public float creatorShare;

        /// <summary>팀 점유율에 더하는 값.</summary>
        public float possession;

        /// <summary>팀 기대 득점에 곱하는 값.</summary>
        public float teamQuality;

        /// <summary>5분 블록마다 깎이는 체력.</summary>
        public float fatigue;

        /// <summary>카드 위험 배수.</summary>
        public float cardRisk;

        /// <summary>수비 기여 배수.</summary>
        public float defence;

        public static PlayerInstructionEffect Neutral => new()
        {
            shotShare = 1f,
            shotQuality = 1f,
            creatorShare = 1f,
            teamQuality = 1f,
            cardRisk = 1f,
            defence = 1f,
        };

        /// <summary>
        /// 한 선수의 지시를 효과로 옮긴다. 지시가 없으면 중립이다.
        /// </summary>
        public static PlayerInstructionEffect From(PlayerInstructions ins)
        {
            var e = Neutral;
            if (ins == null) return e;

            // 슈팅 — 많이 때리면 기회가 늘지만 하나하나가 나빠진다.
            switch (ins.shootingFrequency)
            {
                case ShootingFrequency.ShootOften:
                    e.shotShare *= 1f + BalanceConfig.ShootOftenShare;
                    e.shotQuality *= 1f - BalanceConfig.ShootOftenQuality;
                    break;
                case ShootingFrequency.RarelyShoot:
                    e.shotShare *= 1f - BalanceConfig.ShootOftenShare;
                    e.shotQuality *= 1f + BalanceConfig.ShootOftenQuality;
                    break;
            }

            // 중거리 — 더 멀리서 때린다. 슈팅은 늘고 질은 더 크게 떨어진다.
            switch (ins.longShotTendency)
            {
                case LongShotTendency.ShootFromDistance:
                    e.shotShare *= 1f + BalanceConfig.LongShotShare;
                    e.shotQuality *= 1f - BalanceConfig.LongShotQuality;
                    break;
                case LongShotTendency.AvoidLongShots:
                    e.shotQuality *= 1f + BalanceConfig.LongShotQuality * 0.5f;
                    e.shotShare *= 1f - BalanceConfig.LongShotShare * 0.5f;
                    break;
            }

            // 드리블 — 돌파하면 좋은 기회가 나지만 공을 자주 잃는다.
            switch (ins.dribblingTendency)
            {
                case DribblingTendency.DribbleMore:
                    e.creatorShare *= 1f + BalanceConfig.DribbleMoreCreate;
                    e.possession -= BalanceConfig.DribbleMorePossession;
                    break;
                case DribblingTendency.DribbleLess:
                    e.creatorShare *= 1f - BalanceConfig.DribbleMoreCreate * 0.6f;
                    e.possession += BalanceConfig.DribbleMorePossession * 0.6f;
                    break;
            }

            // 패스 — 짧게 돌리면 공을 쥐고, 과감하면 기회의 질이 오르지만 끊긴다.
            switch (ins.passingStyle)
            {
                case PassingStyle.Risky:
                    e.teamQuality *= 1f + BalanceConfig.RiskyPassQuality;
                    e.possession -= BalanceConfig.RiskyPassPossession;
                    break;
                case PassingStyle.Safe:
                    e.teamQuality *= 1f - BalanceConfig.RiskyPassQuality * 0.6f;
                    e.possession += BalanceConfig.RiskyPassPossession * 0.6f;
                    break;
            }

            // 압박 — 개인 단위로도 체력과 카드를 쓴다.
            switch (ins.pressingIntensity)
            {
                case Data.PressingIntensity.PressMore:
                    e.possession += BalanceConfig.PlayerPressPossession;
                    e.fatigue += BalanceConfig.PlayerPressFatigue;
                    e.cardRisk *= 1f + BalanceConfig.PlayerPressCard;
                    break;
                case Data.PressingIntensity.PressLess:
                    e.possession -= BalanceConfig.PlayerPressPossession * 0.6f;
                    break;
            }

            // 태클 — 세게 들어가면 더 끊지만 카드를 받는다.
            switch (ins.tacklingStyle)
            {
                case TacklingStyle.TackleHarder:
                    e.defence *= 1f + BalanceConfig.HardTackleDefence;
                    e.cardRisk *= 1f + BalanceConfig.HardTackleCard;
                    break;
                case TacklingStyle.EasyTackles:
                    e.defence *= 1f - BalanceConfig.HardTackleDefence * 0.7f;
                    e.cardRisk *= 1f - BalanceConfig.HardTackleCard * 0.6f;
                    break;
            }

            // 침투 — 앞으로 많이 뛰면 골 기회가 늘지만 뒤가 빈다.
            switch (ins.offTheBallRuns)
            {
                case OffTheBallRuns.MakeMoreRuns:
                    e.shotShare *= 1f + BalanceConfig.MoreRunsShare;
                    e.defence *= 1f - BalanceConfig.MoreRunsDefence;
                    e.fatigue += BalanceConfig.MoreRunsFatigue;
                    break;
                case OffTheBallRuns.MakeFewerRuns:
                    e.shotShare *= 1f - BalanceConfig.MoreRunsShare * 0.6f;
                    e.defence *= 1f + BalanceConfig.MoreRunsDefence * 0.6f;
                    break;
            }

            return e;
        }

        /// <summary>선발 전원의 지시를 팀 단위 값으로 모은다.</summary>
        public static (float possession, float quality, float fatigue) TeamTotals(
            IEnumerable<PlayerInstructions> squad)
        {
            float possession = 0f, quality = 1f, fatigue = 0f;

            foreach (var ins in squad)
            {
                var e = From(ins);
                possession += e.possession;
                quality *= e.teamQuality;
                fatigue += e.fatigue;
            }

            return (possession, quality, fatigue);
        }

        /// <summary>
        /// 슬롯 목록을 그대로 받는다. 경기마다 두 번 불리는 자리라 지시만 뽑아내는
        /// LINQ 한 겹도 아깝다 — 같은 순서로 같은 값을 쌓는다.
        /// </summary>
        public static (float possession, float quality, float fatigue) TeamTotals(
            List<FormationSlot> slots)
        {
            float possession = 0f, quality = 1f, fatigue = 0f;

            for (int i = 0; i < slots.Count; i++)
            {
                var e = From(slots[i].customInstructions);
                possession += e.possession;
                quality *= e.teamQuality;
                fatigue += e.fatigue;
            }

            return (possession, quality, fatigue);
        }
    }
}
