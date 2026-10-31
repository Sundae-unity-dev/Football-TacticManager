using System;
using System.Linq;
using FootballTactician.Data;
using FootballTactician.Diagnostics;

namespace FootballTactician.World
{
    /// <summary>
    /// AI 구단의 전술 색깔.
    ///
    /// 전부 기본값으로 두면 상대에 따라 전술을 고를 이유가 사라진다 — 매 경기 같은 상대다.
    /// 구단마다 성향을 주되 평판과 스쿼드에서 끌어내, 약팀이 강팀 흉내를 내지 않게 한다.
    /// </summary>
    public static class AiTactics
    {
        public enum Style
        {
            Possession,     // 점유 — 짧은 패스, 높은 라인
            HighPress,      // 고압박 — 전방에서 끊는다
            LowBlockCounter,// 저블록 역습 — 내려서서 뒷공간을 노린다
            DirectAttack,   // 직선적 — 빠르게 앞으로
            Balanced,       // 무난
        }

        /// <summary>
        /// 그 구단에 어울리는 색깔.
        ///
        /// 평판이 전부를 정하지는 않는다. 같은 등급이라도 갈려야 리그가 단조롭지 않다.
        /// </summary>
        public static Style Pick(GameWorld w, ClubData club, Random rng)
        {
            int rep = club.reputation;
            double roll = rng.NextDouble();

            // 강팀은 공을 쥐거나 몰아붙일 여력이 있다. 약팀이 그러면 그냥 진다.
            if (rep >= 4)
                return roll < 0.45 ? Style.Possession
                     : roll < 0.75 ? Style.HighPress
                     : roll < 0.90 ? Style.Balanced
                     : Style.DirectAttack;

            if (rep == 3)
                return roll < 0.30 ? Style.Balanced
                     : roll < 0.52 ? Style.Possession
                     : roll < 0.74 ? Style.DirectAttack
                     : roll < 0.90 ? Style.HighPress
                     : Style.LowBlockCounter;

            return roll < 0.45 ? Style.LowBlockCounter
                 : roll < 0.70 ? Style.DirectAttack
                 : roll < 0.90 ? Style.Balanced
                 : Style.Possession;
        }

        /// <summary>색깔을 실제 지시로 옮긴다.</summary>
        public static void Apply(TeamInstructions ins, Style style)
        {
            switch (style)
            {
                case Style.Possession:
                    ins.mentality = TeamMentality.Balanced;
                    ins.buildUpStyle = BuildUpStyle.ShortPassing;
                    ins.tempo = 2;
                    ins.pressingIntensity = 4;
                    ins.defensiveLine = DefensiveLine.High;
                    ins.width = 4;
                    break;

                case Style.HighPress:
                    ins.mentality = TeamMentality.Attacking;
                    ins.buildUpStyle = BuildUpStyle.Mixed;
                    ins.tempo = 4;
                    ins.pressingIntensity = 5;
                    ins.counterPress = true;
                    ins.defensiveLine = DefensiveLine.VeryHigh;
                    ins.width = 3;
                    break;

                case Style.LowBlockCounter:
                    ins.mentality = TeamMentality.Defensive;
                    ins.buildUpStyle = BuildUpStyle.Direct;
                    ins.tempo = 3;
                    ins.pressingIntensity = 2;
                    ins.defensiveLine = DefensiveLine.Low;
                    ins.width = 2;
                    ins.directCounterAttack = true;
                    break;

                case Style.DirectAttack:
                    ins.mentality = TeamMentality.Balanced;
                    ins.buildUpStyle = BuildUpStyle.LongBall;
                    ins.tempo = 4;
                    ins.pressingIntensity = 3;
                    ins.defensiveLine = DefensiveLine.Medium;
                    ins.width = 4;
                    break;

                default:
                    ins.mentality = TeamMentality.Balanced;
                    ins.buildUpStyle = BuildUpStyle.Mixed;
                    ins.tempo = 3;
                    ins.pressingIntensity = 3;
                    ins.defensiveLine = DefensiveLine.Medium;
                    ins.width = 3;
                    break;
            }

            // 오프사이드 트랩은 수비진이 받쳐 줄 때만 건다. 못 맞추면 뒷공간이 되레 열린다.
            ins.offsideTrap = false;
        }

        /// <summary>
        /// 세계의 모든 AI 구단에 색깔을 입힌다.
        /// 플레이어 구단은 건드리지 않는다 — 감독이 정한 지시를 덮어쓰면 안 된다.
        /// </summary>
        public static void AssignAll(GameWorld w, Random rng)
        {
            foreach (var club in w.Clubs.Values)
            {
                if (club.id == w.ManagedClubId) continue;

                var tac = club.savedTactics?.FirstOrDefault(t => t.id == club.activeTacticsId);
                if (tac == null) continue;

                using (SimProfile.Measure("  ├ 색깔"))
                {
                    var style = Pick(w, club, rng);
                    Apply(tac.instructions, style);
                }

                // 선발 명단은 한 번만 읽는다. 트랩 판단과 세트피스가 같은 배열을 본다.
                var starters = new PlayerData[tac.slots.Count];
                for (int i = 0; i < tac.slots.Count; i++)
                    starters[i] = w.Player(tac.slots[i].assignedPlayerId);

                // 수비진이 좋으면 트랩을 건다. 이것만은 스쿼드를 보고 정한다.
                using (SimProfile.Measure("  ├ 트랩 판단"))
                tac.instructions.offsideTrap =
                    Simulation.TacticalProfile.TrapSkill(starters) >= Balance.BalanceConfig.TrapBreakEven + 1.5f
                    && tac.instructions.defensiveLine is DefensiveLine.High or DefensiveLine.VeryHigh;

                // 역할용 난수를 따로 쓴다. 같은 rng 를 쓰면 추첨 횟수가 밀려
                // 다음 구단들의 전술 색깔 배분이 통째로 바뀐다.
                using (SimProfile.Measure("  ├ 역할 고르기"))
                    AssignRoles(w, club, tac, new Random(w.Seed ^ (club.id * 7919)));
                using (SimProfile.Measure("  └ 세트피스"))
                    AssignSetPieces(tac, starters);
            }
        }

        /// <summary>
        /// 스쿼드에 맞는 코너 배달과 키커를 정한다.
        ///
        /// 모두 기본값(인스윙)으로 두면 리그의 모든 팀이 같은 선택을 하게 되어,
        /// 상대에 따라 배달을 고르는 것이 의미를 잃는다.
        /// </summary>
        static void AssignSetPieces(TacticsData tac, PlayerData[] starters)
        {
            if (starters.All(p => p == null)) return;

            var delivery = Simulation.SetPieces.SuggestDelivery(starters);
            var taker = Simulation.SetPieces.Taker(starters, null, Simulation.SetPieces.Kind.Corner);

            tac.setPieces ??= new SetPieceConfig();
            tac.setPieces.cornerKickLeft = new List<SetPieceRoutine>
            {
                new() { type = SetPieceType.CornerKick, delivery = delivery, takerPlayerId = taker?.id ?? -1 },
            };
            tac.setPieces.cornerKickRight = new List<SetPieceRoutine>(tac.setPieces.cornerKickLeft);
        }

        /// <summary>
        /// 선수에 맞춰 역할을 고른다. 다만 구단 수준만큼만 잘 고른다.
        ///
        /// AI 가 전부 관례값만 쓰면 감독은 역할을 맞추는 것만으로 모든 상대보다 앞선다.
        /// 반대로 전부 최적으로 고르면 감독이 이길 여지가 없다. 평판으로 가른다.
        /// </summary>
        static void AssignRoles(GameWorld w, ClubData club, TacticsData tac, Random rng)
        {
            // 5성 구단은 대부분 맞춰 고르고, 1성 구단은 거의 관례값 그대로 둔다.
            double skill = 0.15 + club.reputation * 0.17;

            foreach (var slot in tac.slots)
            {
                var p = w.Player(slot.assignedPlayerId);
                if (p == null) continue;
                if (rng.NextDouble() > skill) continue;

                if (Simulation.RoleFit.Best(p, slot.position) is not { } best) continue;

                // 적합도만 보고 바꾸면 만드는 역할이나 넣는 역할 한쪽으로 쏠려 스스로 손해를 본다.
                var before = slot.role;
                float balanceBefore = Simulation.RoleFit.BalanceMultiplier(tac.slots);

                slot.role = new RoleAssignment
                {
                    role = best,
                    duty = Simulation.RoleFit.DefaultDuty(RoleDatabase.Get(best)),
                };

                if (Simulation.RoleFit.BalanceMultiplier(tac.slots) < balanceBefore)
                    slot.role = before;
            }
        }
    }
}
