using System;
using System.Collections.Generic;

namespace FootballTactician.Data
{
    // ── 이적 제안 ──────────────────────────────────────────────────────────────

    [Serializable]
    public class TransferOffer
    {
        public int      offerId;
        public int      fromClubId;
        public int      toClubId;
        public int      playerId;
        public GameDate offerDate;

        public TransferOfferStatus status;
        public TransferType        transferType;

        // ── 영구 이적 조건 ────────────────────────────────────────────────────
        public long upfrontFee;             // 선불 이적료
        public List<InstallmentPayment> installments; // 분할 납부 스케줄

        // ── 추가 조항 ─────────────────────────────────────────────────────────
        public SellOnClause    sellOn;      // 재이적 시 수익 일부 귀속
        public BuyBackClause   buyBack;     // 원클럽 재영입 우선권
        public Solidarity      solidarity;  // 연대 기여금 (FIFA 규정, 5%)

        // ── 계약 조건 제안 ────────────────────────────────────────────────────
        public int proposedWeeklyWage;
        public int proposedContractYears;
        public int proposedSigningBonus;
    }

    public enum TransferOfferStatus
    {
        Pending,        // 협상 중
        Accepted,       // 합의
        Rejected,       // 거절
        Withdrawn,      // 철회
        Completed,      // 이적 완료
        Failed,         // 선수 개인 조건 협의 실패
    }

    // ── 분할 납부 ──────────────────────────────────────────────────────────────

    [Serializable]
    public class InstallmentPayment
    {
        public long     amount;
        public GameDate dueDate;
        public bool     isPaid;

        // 조건부 분할 납부 (예: 30경기 출전 시)
        public InstallmentCondition condition;
        public int                  conditionThreshold; // 조건 수치
    }

    public enum InstallmentCondition
    {
        None,           // 무조건 지급
        Appearances,    // 출전 경기 수
        Goals,          // 득점
        ClubLeagueFinish, // 리그 최종 순위 이하
    }

    // ── 셀온 조항 ──────────────────────────────────────────────────────────────

    [Serializable]
    public class SellOnClause
    {
        public bool enabled;
        public int  percentage;         // 재이적료의 몇 % (보통 10~20%)
        public int  appliesToTransfers; // 이후 몇 번째 이적까지 적용 (-1 = 무제한)
        public long minimumFeeThreshold; // 이 금액 이상 이적 시에만 발동
    }

    // ── 바이백 조항 ────────────────────────────────────────────────────────────

    [Serializable]
    public class BuyBackClause
    {
        public bool     enabled;
        public long     buyBackFee;
        public GameDate validUntil;
        public bool     isExclusive; // 원클럽만 행사 가능 vs 타 클럽도 동일 조건
    }

    // ── 연대 기여금 (FIFA) ─────────────────────────────────────────────────────

    [Serializable]
    public class Solidarity
    {
        public bool enabled       = true;
        public int  percentage    = 5;   // 이적료의 5%를 육성 클럽들에 분배
    }

    // ── 임대 계약 ──────────────────────────────────────────────────────────────

    [Serializable]
    public class LoanAgreement
    {
        public int      loanId;
        public int      fromClubId;     // 원소속 클럽
        public int      toClubId;       // 임대 클럽
        public int      playerId;
        public GameDate startDate;
        public GameDate endDate;

        public int  loanFee;                // 임대료 (일시불)
        public int  wageContributionPercent; // 임대 클럽 주급 부담 비율 (%)
        public bool parentClubPaysRemainder; // 원소속 클럽이 잔여 주급 부담

        public RecallClause   recall;
        public OptionToBuy    optionToBuy;
        public MandatoryBuy   mandatoryBuy;

        public bool playingTimeGuarantee;   // 일정 출전 시간 보장 요구
        public int  minimumAppearances;     // 최소 출전 경기 수
    }

    // ── 리콜 조항 ─────────────────────────────────────────────────────────────

    [Serializable]
    public class RecallClause
    {
        public bool enabled;
        public int  noticeWindowDays;   // 리콜 통보 후 복귀까지 대기 기간
        public GameDate earliestRecallDate;
        public bool requiresMutualConsent;
    }

    // ── 옵션 / 의무 구매 조항 ─────────────────────────────────────────────────

    [Serializable]
    public class OptionToBuy
    {
        public bool enabled;
        public long fee;
        public GameDate exercisableFrom;
        public GameDate exercisableUntil;
    }

    [Serializable]
    public class MandatoryBuy
    {
        public bool enabled;
        public long fee;

        // 발동 조건 (모두 충족 시 의무 구매)
        public int  appearancesRequired;
        public bool mustAvoidRelegation;
        public bool mustQualifyForContinent;
    }

    // ── 에이전트 ───────────────────────────────────────────────────────────────

    [Serializable]
    public class AgentData
    {
        public int    agentId;
        public string name;
        public int    reputation;       // 1~5
        public int    commissionPercent; // 이적료의 몇 % (보통 5~10%)
        public List<int> clientPlayerIds;

        // 에이전트 성향
        public AgentStyle style;
    }

    public enum AgentStyle
    {
        Neutral,        // 합리적 협상
        Aggressive,     // 높은 요구 조건, 자주 이적 요청
        Loyal,          // 선수 의중 최우선
        Mercenary,      // 최고 조건 우선
    }

    // ── 홈그로운 / 워크퍼밋 ───────────────────────────────────────────────────

    [Serializable]
    public class HomegrownStatus
    {
        public bool isHomegrown;           // 자국 리그 3년 이상 육성
        public bool isClubAcademy;         // 해당 클럽 아카데미 출신
        public int  yearsInNationalLeague; // 자국 리그 누적 육성 연수
        public int  yearsAtCurrentClub;    // 현 클럽 아카데미 누적 연수
    }

    [Serializable]
    public class WorkPermitStatus
    {
        public bool    hasPermit;
        public GameDate permitExpiry;
        public WorkPermitTier tier;
        public string  issuingCountry;
    }

    public enum WorkPermitTier
    {
        NotRequired,    // 자국 또는 해당 국가 시민권
        Automatic,      // 자동 발급 (국제 경기 출전 기준 충족)
        Discretionary,  // 심사 필요
        Denied,         // 불허
    }

    /// <summary>
    /// 이적 요청.
    ///
    /// 지금까지 선수는 불만만 쌓고 아무것도 하지 않았다. 불만이 행동이 되지 않으면
    /// 탈의실 화면은 읽을거리일 뿐이다. 이제 참다 못한 선수는 이적을 요청하고,
    /// 감독은 받아들이거나 거절해야 한다. <b>어느 쪽도 공짜가 아니다.</b>
    /// </summary>
    [Serializable]
    public sealed class TransferRequest
    {
        public int playerId;
        public GameDate madeOn;
        public string reason = "";

        /// <summary>거절당한 횟수. 거듭 거절하면 더 크게 틀어진다.</summary>
        public int rejections;
    }

    /// <summary>완료된 이적 한 건. 화면의 이적 기록에 쓴다.</summary>
    [Serializable]
    public sealed class TransferRecord
    {
        public GameDate date;
        public int playerId;
        public string playerName;
        public int fromClubId;
        public int toClubId;
        public long fee;
        public int weeklyWage;
    }
}
