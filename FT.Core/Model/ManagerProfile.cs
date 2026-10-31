using System;
using System.Collections.Generic;

namespace FootballTactician.Data
{
    /// <summary>
    /// StaffRole.HeadCoach 스탭에 부여되는 감독 전용 데이터.
    /// StaffData와 1:1로 연결된다 (staffId로 참조).
    /// </summary>
    [Serializable]
    public class ManagerProfile
    {
        public int          staffId;
        public string       tacticalStyle;
        public List<string> preferredFormations;
        public int          attackingMentality;   // 1(매우 수비적) ~ 5(매우 공격적)
        public int          pressingIntensity;    // 1(낮음) ~ 5(높음)

        // ── 언론 / 팬 관계 (차별화 시스템) ──────────────────────────────────
        public int   pressReputation;    // 0~100 (언론과의 관계)
        public int   fanApproval;        // 0~100 (팬 지지도)
        public int   boardConfidence;    // 0~100 (이사회 신뢰도, BoardConfidenceLevel 연동)
        public ManagerPressStyle pressStyle; // 기자회견 성향

        // ── 이력 ─────────────────────────────────────────────────────────────
        public int   totalWins;
        public int   totalDraws;
        public int   totalLosses;
        public int   titlesWon;
        public int   promotionsAchieved;
        public int   relegationsSuffered;
    }

    public enum ManagerPressStyle
    {
        Diplomatic,     // 중립적 답변, 갈등 회피
        Passionate,     // 감정적 발언, 팬 호응 높음
        Analytical,     // 데이터 기반 답변, 언론 신뢰 높음
        Confrontational, // 직설적, 때로 논란 유발
        Reserved,       // 말 아낌, 미디어 노출 최소화
    }
}
