using System;

namespace FootballTactician.Data
{
    /// <summary>
    /// 경기 내 규정. 리그/대회별로 설정되며 MatchSimulation이 참조한다.
    /// </summary>
    [Serializable]
    public class MatchRules
    {
        // ── 교체 규정 ──────────────────────────────────────────────────────────
        public int  maxSubstitutions         = 5;   // 정규 시간 교체 한도
        public int  maxSubstitutionWindows   = 3;   // 교체 가능 구간 수 (3 = 전/후반 + 연장)
        public bool extraTimeSubstitutionAllowed = true;  // 연장전 추가 교체 허용
        public bool concussionSubAllowed     = true; // 뇌진탕 추가 교체 (독립)

        // ── VAR ────────────────────────────────────────────────────────────────
        public bool varEnabled           = true;
        public bool semiAutomatedOffside = true;   // 반자동 오프사이드 판독

        // ── 카드 / 징계 ────────────────────────────────────────────────────────
        public int yellowCardsForSuspension = 5;   // 누적 옐로우 → 1경기 출전 정지
        public int yellowCardResetMatchday  = 28;  // 이 라운드 이후 누적 초기화
        public int redCardSuspensionMatches = 1;   // 퇴장 기본 출전 정지 경기 수
        public int violentConductMatches    = 3;   // 폭력 행위 추가 정지

        // ── 경기 시간 ──────────────────────────────────────────────────────────
        public int  halfLengthMinutes       = 45;
        public int  minInjuryTimeMinutes    = 2;
        public int  maxInjuryTimeMinutes    = 10;
        public bool extraTimeEnabled        = true;
        public int  extraTimeLengthMinutes  = 15;  // 전·후반 각각

        // ── 승부차기 ───────────────────────────────────────────────────────────
        public bool penaltyShootoutEnabled  = true;
        public int  penaltyKickersPerTeam   = 5;

        // ── 기타 필드 규칙 ─────────────────────────────────────────────────────
        public bool awayGoalRule     = false;  // 원정 다득점 원칙 (대부분 폐지)
        public bool professionalFoul = true;   // 결정적 기회 저지 = 레드카드

        // ── 오프사이드 ─────────────────────────────────────────────────────────
        public OffsideRule offsideRule = OffsideRule.Standard;
    }

    public enum OffsideRule
    {
        Standard,           // 일반 오프사이드
        ActivePlay,         // 플레이에 관여한 경우만
        Daylight,           // 명백한 차이가 있는 경우만 (여자축구 적용 논의)
    }
}
