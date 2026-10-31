using System;
using System.Collections.Generic;

namespace FootballTactician.Data
{
    /// <summary>
    /// 리그/대회별 규정. LeagueData에 포함되어 시즌 전반에 걸쳐 적용된다.
    /// </summary>
    [Serializable]
    public class LeagueRules
    {
        // ── 승점 체계 ──────────────────────────────────────────────────────────
        public int pointsForWin  = 3;
        public int pointsForDraw = 1;
        public int pointsForLoss = 0;

        // 동점 시 순위 결정 기준 (순서대로 적용)
        public List<TiebreakerCriteria> tiebreakers = new List<TiebreakerCriteria>
        {
            TiebreakerCriteria.GoalDifference,
            TiebreakerCriteria.GoalsFor,
            TiebreakerCriteria.HeadToHead,
            TiebreakerCriteria.AwayGoals,
        };

        // ── 스쿼드 등록 ────────────────────────────────────────────────────────
        public int squadSizeMin          = 16;
        public int squadSizeMax          = 25;
        public int homegrownPlayersMin   = 4;   // 자국 리그에서 3년 이상 육성
        public int clubAcademyPlayersMin = 2;   // 해당 클럽 아카데미 출신
        public int foreignPlayerMax      = 17;  // -1 = 제한 없음

        // ── 재정 규정 (FFP / 급여 상한) ────────────────────────────────────────
        public FinancialFairPlayRules ffp;
        public SalaryCapRules         salaryCap;

        // ── 이적 창구 ──────────────────────────────────────────────────────────
        public int maxLoanPlayersIn  = 6;   // 임대 영입 최대 인원
        public int maxLoanPlayersOut = 6;   // 임대 방출 최대 인원
        public int maxLoansFromSameClub = 2;
    }

    // ── 동점 시 순위 결정 기준 ─────────────────────────────────────────────────

    public enum TiebreakerCriteria
    {
        GoalDifference,
        GoalsFor,
        HeadToHead,
        AwayGoals,
        YellowCards,
        RedCards,
        Drawing,        // 추첨
    }

    // ── 재정 공정성 규정 ───────────────────────────────────────────────────────

    [Serializable]
    public class FinancialFairPlayRules
    {
        public bool enabled = true;

        // 3시즌 누적 허용 적자 (음수 = 적자)
        public long maxAllowedLoss3Seasons = -15_000_000;

        // 위반 시 제재
        public FfpSanction minorViolationSanction = FfpSanction.Fine;
        public FfpSanction majorViolationSanction = FfpSanction.TransferBan;
    }

    public enum FfpSanction
    {
        None,
        Warning,
        Fine,
        TransferBan,       // 이적 창구 제한
        SquadSizeReduction,
        PointDeduction,
        Relegation,
        Exclusion,         // 대회 제명
    }

    // ── 급여 상한 ──────────────────────────────────────────────────────────────

    [Serializable]
    public class SalaryCapRules
    {
        public bool enabled = false;
        public int  weeklyCapAmount;    // 팀 전체 주급 상한
        public int  individualCapAmount; // 선수 1인 주급 상한 (-1 = 없음)
        public FfpSanction violationSanction = FfpSanction.PointDeduction;
    }
}
