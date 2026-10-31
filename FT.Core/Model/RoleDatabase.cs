using System.Collections.Generic;

namespace FootballTactician.Data
{
    public static partial class RoleDatabase
    {
        private static Dictionary<PlayerRole, RoleDefinition> _roles;
        private static readonly object _lock = new();

        /// <summary>
        /// 역할 사전.
        ///
        /// 전에는 <c>Build()</c> 가 빈 사전을 먼저 <c>_roles</c> 에 넣고 나중에 채웠다.
        /// 두 스레드가 동시에 들어오면 한쪽이 <b>절반만 찬 사전</b>을 보고 역할을 못 찾는다.
        /// 게임은 단일 스레드라 드러나지 않았지만, 병렬로 도는 테스트가 이따금 깨졌다.
        /// 다 채운 뒤에 한 번에 공개한다.
        /// </summary>
        public static Dictionary<PlayerRole, RoleDefinition> All
        {
            get
            {
                var built = _roles;
                if (built != null) return built;

                lock (_lock)
                {
                    return _roles ??= Build();
                }
            }
        }

        public static RoleDefinition Get(PlayerRole role) => All[role];

        /// <summary>
        /// 그 자리에서 쓸 수 있는 역할들.
        ///
        /// 한 자리에 맞는 역할을 고를 때마다 43개를 전부 훑고 있었다 — 세계를 만들 때
        /// 1,300번 그러면서 점수를 8만 번 냈다. 자리별로 한 번만 추려 둔다.
        /// 순서는 <see cref="All"/> 의 순서를 그대로 지킨다. 동점이면 먼저 오는 역할이
        /// 뽑히므로, 순서가 바뀌면 어떤 역할이 뽑히는지까지 바뀐다.
        ///
        /// 자리가 여러 비트를 가진 경우(인접 자리 마스크 같은 것)는 추려 두지 않고 그때 훑는다.
        /// 포메이션의 슬롯은 모두 한 자리다.
        /// </summary>
        public static RoleDefinition[] ForPosition(Position position)
        {
            int bit = SingleBit(position);
            if (bit < 0) return Filter(position);

            var table = _byBit;
            if (table == null)
                lock (_lock)
                    table = _byBit ??= BuildByBit();

            return table[bit];
        }

        static RoleDefinition[][] _byBit;

        static RoleDefinition[][] BuildByBit()
        {
            var table = new RoleDefinition[PositionBits][];
            for (int b = 0; b < PositionBits; b++)
                table[b] = Filter((Position)(1 << b));
            return table;
        }

        static RoleDefinition[] Filter(Position position)
        {
            var list = new List<RoleDefinition>();
            foreach (var (_, def) in All)
                if ((def.applicablePositions & position) != 0) list.Add(def);
            return list.ToArray();
        }

        /// <summary>Position 은 GK(1&lt;&lt;0) 부터 CF(1&lt;&lt;14) 까지 15비트다.</summary>
        const int PositionBits = 15;

        /// <summary>한 자리면 그 비트 번호, 아니면 -1.</summary>
        static int SingleBit(Position position)
        {
            int v = (int)position;
            if (v <= 0 || (v & (v - 1)) != 0) return -1;

            int bit = 0;
            while ((v >>= 1) != 0) bit++;
            return bit < PositionBits ? bit : -1;
        }

        /// <summary>조립 중인 사전. 부분 클래스로 나뉜 BuildXx 들이 Add 로 채운다.</summary>
        private static Dictionary<PlayerRole, RoleDefinition> _building;

        private static Dictionary<PlayerRole, RoleDefinition> Build()
        {
            _building = new Dictionary<PlayerRole, RoleDefinition>();

            BuildGk();
            BuildCb();
            BuildFb();
            BuildDm();
            BuildCm();
            BuildAm();
            BuildWide();
            BuildSt();

            // 다 채운 뒤에 돌려준다. 호출자(All)가 _roles 에 한 번에 꽂는다.
            var done = _building;
            _building = null;
            return done;
        }

        private static void Add(RoleDefinition def) => _building[def.role] = def;
    }
}
