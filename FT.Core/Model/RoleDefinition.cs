using System;
using System.Collections.Generic;

namespace FootballTactician.Data
{
    [Serializable]
    public class RoleDefinition
    {
        public PlayerRole role;
        public string displayName;
        public string shortDescription;
        public string fullDescription;
        public string tacticalBehavior;
        public string strengths;
        public string weaknesses;
        public string bestIn;

        // Position [Flags] 비트마스크 — List<Position> 대비 O(1) 확인, 메모리 절약
        public Position applicablePositions;
        public List<RoleDuty> allowedDuties;

        // 타입드 enum — 컴파일 타임 오타 방지, 리플렉션 없이 능력치 매핑 가능
        public List<TechnicalAttribute> keyTechnicalAttributes;
        public List<PhysicalAttribute>  keyPhysicalAttributes;
        public List<MentalAttribute>    keyMentalAttributes;
        public List<GkAttribute>        keyGkAttributes;  // GK 역할 전용

        public PlayerInstructions defendInstructions;
        public PlayerInstructions supportInstructions;
        public PlayerInstructions attackInstructions;

        public bool CanPlayAt(Position pos) => (applicablePositions & pos) != 0;
        public bool HasDuty(RoleDuty duty)  => allowedDuties != null && allowedDuties.Contains(duty);

        public PlayerInstructions GetDefaultInstructions(RoleDuty duty)
        {
            PlayerInstructions src = duty switch
            {
                RoleDuty.Defend => defendInstructions  ?? supportInstructions,
                RoleDuty.Attack => attackInstructions  ?? supportInstructions,
                _               => supportInstructions,
            };
            return src != null ? PlayerInstructions.CopyFrom(src) : new PlayerInstructions();
        }
    }
}
