using System;
using FootballTactician.Data;

namespace FootballTactician.World
{
    /// <summary>
    /// 구단이 평판대로 갖추는 스태프 자리.
    ///
    /// 만드는 쪽(<see cref="StaffForge"/>)과 재는 쪽(<see cref="StaffRoom"/>)이 같은 표를 봐야 한다.
    /// 한쪽만 알면 재는 쪽이 "자리를 다 채웠을 때"를 기준으로 삼게 되고, 의료진을 한 명만 두는
    /// 1~2성 구단이 영원히 모자란 것으로 나온다 — 3부까지 있는 세계에서는 그런 구단이 다수라
    /// 세계 전체의 부상이 밀려 올라간다. 전에는 이 표가 만드는 쪽에만 있어서
    /// 재는 쪽이 만드는 쪽을 되참조했다.
    /// </summary>
    public static class StaffPlan
    {
        /// <summary>구단이 평판대로 갖추는 자리.</summary>
        public static readonly StaffRole[] CoreRoles =
        {
            // 네 갈래를 먼저 하나씩 채운다. 코치부터 줄 세우면 작은 구단에
            // 의료진이 통째로 없어 부상이 25% 많아진다 — 설계가 아니라 순서 탓이다.
            StaffRole.HeadCoach, StaffRole.Physio, StaffRole.RegionalScout, StaffRole.DataAnalyst,
            StaffRole.AssistantCoach, StaffRole.TeamDoctor, StaffRole.HeadScout,
            StaffRole.PhysicalCoach, StaffRole.GoalkeeperCoach, StaffRole.YouthCoordinator,
        };

        /// <summary>평판이 낮으면 다 못 채운다. 1성이 10자리를 갖추면 평판이 무의미해진다.</summary>
        public static int RolesFilled(int reputation) => reputation switch
        {
            >= 5 => 10,
            4 => 9,
            3 => 7,
            2 => 5,
            _ => 4,
        };

        /// <summary>
        /// 그 평판 구단이 실제로 두는, 그 갈래의 인원.
        ///
        /// StaffRoom 의 기준점이 이 값을 써야 한다. "자리를 다 채웠을 때"를 기준으로 삼으면
        /// 의료진을 한 명만 두는 1~2성 구단이 영원히 모자란 것으로 나오고,
        /// 3부까지 있는 세계에서는 그런 구단이 다수라 세계 전체의 부상이 밀려 올라간다.
        /// </summary>
        public static int TypicalHeads(int reputation, Func<StaffRole, bool> branch)
        {
            int fill = Math.Min(RolesFilled(reputation), CoreRoles.Length);
            int n = 0;
            for (int i = 0; i < fill; i++) if (branch(CoreRoles[i])) n++;
            return Math.Max(1, n);
        }
    }
}
