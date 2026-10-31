using FootballTactician.Data;

namespace FootballTactician.Simulation
{
    /// <summary>
    /// 자리마다의 관례적인 역할.
    ///
    /// 일부러 <b>그 선수에게 가장 맞는 역할을 고르지 않는다.</b> 자동으로 최적을 잡아 주면
    /// 감독이 역할을 손댈 이유가 사라지고, 역할은 보이지 않는 공짜 보너스가 된다.
    /// 무난한 기본값을 두고, 선수의 장점에 맞춰 바꾸는 것을 감독의 몫으로 남긴다.
    /// </summary>
    public static class RoleDefaults
    {
        public static PlayerRole ForPosition(Position pos)
        {
            if ((pos & Position.GK) != 0) return PlayerRole.Goalkeeper;
            if ((pos & Position.CB) != 0) return PlayerRole.CentralDefender;
            if ((pos & (Position.LWB | Position.RWB)) != 0) return PlayerRole.Wingback;
            if ((pos & (Position.LB | Position.RB)) != 0) return PlayerRole.Fullback;
            if ((pos & Position.CDM) != 0) return PlayerRole.DefensiveMidfielder;
            if ((pos & Position.CM) != 0) return PlayerRole.CentralMidfielder;
            if ((pos & Position.CAM) != 0) return PlayerRole.AttackingMidfielder;
            if ((pos & (Position.LM | Position.RM)) != 0) return PlayerRole.WideMidfielder;
            if ((pos & (Position.LW | Position.RW)) != 0) return PlayerRole.Winger;
            if ((pos & (Position.ST | Position.CF)) != 0) return PlayerRole.AdvancedForward;
            return PlayerRole.CentralMidfielder;
        }

        /// <summary>역할이 비어 있는 슬롯만 관례값으로 채운다. 감독이 고른 역할은 건드리지 않는다.</summary>
        public static void Fill(TacticsData tactics)
        {
            if (tactics?.slots == null) return;

            foreach (var slot in tactics.slots)
            {
                if (slot.role != null) continue;

                var role = ForPosition(slot.position);
                slot.role = new RoleAssignment
                {
                    role = role,
                    duty = RoleDatabase.All.TryGetValue(role, out var def)
                        ? RoleFit.DefaultDuty(def)
                        : RoleDuty.Support,
                };
            }
        }
    }
}
