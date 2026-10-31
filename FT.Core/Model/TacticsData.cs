using System;
using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace FootballTactician.Data
{
    [Serializable]
    public class TacticsData
    {
        public int    id;
        public string name;
        public string formationShape;   // "4-3-3", "4-2-3-1" 등 표시용 문자열

        public List<FormationSlot> slots        = new List<FormationSlot>(); // 11개 고정
        public TeamInstructions    instructions = new TeamInstructions();
        public SetPieceConfig      setPieces    = new SetPieceConfig();
    }

    // ── 포메이션 슬롯 ─────────────────────────────────────────────────────────

    [Serializable]
    public class FormationSlot
    {
        public int            slotIndex;         // 0(GK) ~ 10
        public Position       position;
        public RoleAssignment role;
        public int            assignedPlayerId;  // -1 = 미배정

        // 좌표: 0~1 정규화 (x: 좌=0 우=1, y: 자기 골문=0 상대 골문=1)
        //
        // 저장하지 않는다. 전술 보드가 그리는 데만 쓰고 아무도 바꾸지 않으므로,
        // 포메이션 프리셋에서 그대로 되살릴 수 있다. 구단 240개 × 슬롯 11개 × 실수 4개를
        // 매번 쓰면 세이브와 직렬화 시간만 늘어난다.
        // 감독이 보드에서 선수를 끌어 옮길 수 있게 되면 여기 [JsonIgnore] 를 떼야 한다.
        [JsonIgnore] public float attackingX;
        [JsonIgnore] public float attackingY;
        [JsonIgnore] public float defendingX;
        [JsonIgnore] public float defendingY;

        // null = 역할(RoleDefinition) 기본 지시사항 사용, 감독이 수동 수정 시 채워짐
        public PlayerInstructions customInstructions;
    }

    /// <summary>
    /// 팀 지시.
    ///
    /// 세이브는 기본값을 빼고 쓴다(WhenWritingDefault). 그런데 여기 필드들은 초기값이
    /// default(T) 와 다르다 — 예를 들어 buildUpStyle 의 초기값은 Mixed(1) 인데
    /// default 는 ShortPassing(0) 이다. 그래서 짧은 패스를 고르면 저장에서 빠지고,
    /// 불러올 때 초기값 Mixed 로 되살아난다.
    /// 그런 필드에는 항상 쓰도록 표시해 둔다.
    /// </summary>
    [Serializable]
    public class TeamInstructions
    {
        [JsonIgnore(Condition = JsonIgnoreCondition.Never)]
        public TeamMentality mentality = TeamMentality.Balanced;

        // 압박
        [JsonIgnore(Condition = JsonIgnoreCondition.Never)]
        public int  pressingIntensity = 3;      // 1(저블록) ~ 5(고강도)
        public bool counterPress      = false;  // 볼 유실 직후 즉각 압박

        // 빌드업
        [JsonIgnore(Condition = JsonIgnoreCondition.Never)]
        public BuildUpStyle buildUpStyle = BuildUpStyle.Mixed;
        [JsonIgnore(Condition = JsonIgnoreCondition.Never)]
        public int          tempo        = 3;   // 1(느림) ~ 5(빠름)

        // 수비 형태
        [JsonIgnore(Condition = JsonIgnoreCondition.Never)]
        public DefensiveLine defensiveLine = DefensiveLine.Medium;
        [JsonIgnore(Condition = JsonIgnoreCondition.Never)]
        public int           width         = 3; // 1(좁음) ~ 5(넓음)
        public bool          offsideTrap   = false;

        // 전환
        public bool directCounterAttack = false;
    }

    public enum TeamMentality { VeryDefensive, Defensive, Balanced, Attacking, VeryAttacking }
    public enum BuildUpStyle  { ShortPassing, Mixed, LongBall, Direct }
    public enum DefensiveLine { VeryLow, Low, Medium, High, VeryHigh }

    // ── 세트피스 설정 ─────────────────────────────────────────────────────────

    [Serializable]
    public class SetPieceConfig
    {
        public List<SetPieceRoutine> cornerKickLeft   = new List<SetPieceRoutine>();
        public List<SetPieceRoutine> cornerKickRight  = new List<SetPieceRoutine>();
        public List<SetPieceRoutine> freeKickRoutines = new List<SetPieceRoutine>();
    }

    [Serializable]
    public class SetPieceRoutine
    {
        [JsonIgnore(Condition = JsonIgnoreCondition.Never)]
        public SetPieceType     type;
        [JsonIgnore(Condition = JsonIgnoreCondition.Never)]
        public SetPieceDelivery delivery;
        [JsonIgnore(Condition = JsonIgnoreCondition.Never)]
        public int              takerPlayerId = -1;
        public List<SetPiecePlayerRun> runs  = new List<SetPiecePlayerRun>();
    }

    [Serializable]
    public class SetPiecePlayerRun
    {
        public int         playerId;
        public SetPieceRun runType;
    }

    public enum SetPieceType     { CornerKick, FreeKickDirect, FreeKickIndirect, ThrowIn, GoalKick }
    public enum SetPieceDelivery { Inswinger, Outswinger, ShortPass, DrivenLow, Lofted }
    public enum SetPieceRun      { NearPost, FarPost, EdgeOfBox, Pullback, SecondBall, Block }
}
