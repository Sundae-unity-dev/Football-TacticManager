using System;
using System.Collections.Generic;

namespace FootballTactician.Data
{
    /// <summary>
    /// 선수 간 관계망. 탈의실 화학 반응, 리더십, 사기에 영향을 준다.
    /// </summary>
    [Serializable]
    public class PlayerRelationship
    {
        public int              playerId;
        public int              targetPlayerId;
        public RelationshipType type;
        public int              strength;   // 0~100 (강도)
        public GameDate         since;      // 관계 형성 시점
    }

    public enum RelationshipType
    {
        // 긍정적
        Teammate,       // 일반 동료 (기본값)
        Friend,         // 친한 동료 → 사기 보너스
        Mentor,         // 멘토 역할 (경험자 → 젊은 선수)
        Mentee,         // 멘티 역할
        Captain,        // 주장-선수 관계 → 지시 영향력 증가
        NationalTeam,   // 같은 국가대표 출신 → 친밀도 초기값 높음

        // 부정적
        Rival,          // 라이벌 → 같은 팀 내 긴장감
        Dislike,        // 비호감 → 팀워크 페널티
        Conflict,       // 갈등 → 이적 요청 가능성 증가
    }

    /// <summary>
    /// 탈의실 분위기. 클럽 단위로 관리되며 선수 개별 관계망을 집계한다.
    /// </summary>
    [Serializable]
    public class DressingRoomMorale
    {
        public int  clubId;
        public float overallHarmony;      // 0~100 (전체 화합도)
        public float leadershipStrength;  // 0~100 (리더십 구조 안정성)
        public float discontent;          // 0~100 (불만 수위 — 이적 요청 확률에 영향)

        public List<PlayerRelationship> relationships;

        // 주요 인물
        public int captainPlayerId;
        public int viceCaptainPlayerId;
        public List<int> influencerPlayerIds;  // 탈의실 영향력 상위 선수들
    }
}
