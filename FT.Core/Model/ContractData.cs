using System;
using System.Collections.Generic;

namespace FootballTactician.Data
{
    [Serializable]
    public class ContractData
    {
        public int            clubId;
        public int            weeklyWage;
        public GameDate       startDate;
        public GameDate       expiryDate;
        public ContractStatus status;

        public ContractBonus  bonuses;
        public ContractClause clauses;
        public LoanContractTerms loanTerms;   // 임대 계약인 경우에만 사용

        public int  agentId;                  // AgentData 참조
        public int  agentFeePercent;
        public int  signingBonus;
        public int  imageRightsPercent;
        public int  loyaltyBonus;             // 계약 만료까지 잔류 시 지급
        public bool hasNoShopClause;          // 이적 제안 수락 전 타 클럽 접촉 금지

        public bool IsExpiringSoon(GameDate currentDate)
            => GameDate.MonthsBetween(currentDate, expiryDate) <= 6;

        public bool IsExpired(GameDate currentDate)
            => expiryDate < currentDate;
    }

    [Serializable]
    public class ContractBonus
    {
        public int appearanceBonus;     // 경기당 출전 보너스
        public int startingBonus;       // 선발 출전 보너스
        public int goalBonus;
        public int assistBonus;
        public int cleanSheetBonus;
        public int manOfMatchBonus;
        public int promotionBonus;
        public int titleBonus;
        public int continentalBonus;
        public int topScorerBonus;
        public int playerOfYearBonus;
    }

    [Serializable]
    public class ContractClause
    {
        // 방출 조항
        public bool hasReleaseClause;
        public long releaseClauseAmount;
        public bool releaseClauseInternationalOnly; // 해외 클럽 한정 방출 조항

        // 리그 성적 연동 조항
        public bool hasContinentalClause;           // 유럽 대회 진출 실패 시 이탈 허용
        public bool hasRelegationClause;            // 강등 시 이탈 허용
        public bool hasPromoRelegationClause;       // 승강격 시 주급 변동
        public int  promoWageIncrease;              // 승격 시 주급 인상 (%)
        public int  relegationWageDecrease;         // 강등 시 주급 삭감 (%)

        // 출전 보장 조항
        public bool hasAppearanceClause;
        public int  minimumAppearancesPerSeason;    // 미달 시 이적 요청권 발동
    }

    // 임대 계약 전용 조건 (ContractData에 포함)
    [Serializable]
    public class LoanContractTerms
    {
        public int      parentClubId;
        public GameDate loanEndDate;
        public int      loanFee;
        public int      wageContributionPercent;
        public bool     recallable;
        public GameDate earliestRecallDate;
        public bool     optionToBuy;
        public long     optionToBuyFee;
        public bool     mandatoryBuy;
        public long     mandatoryBuyFee;
        public int      mandatoryBuyAppearances;
    }

    [Serializable]
    public struct GameDate : IEquatable<GameDate>, IComparable<GameDate>
    {
        public int year;
        public int month;
        public int day;

        public GameDate(int year, int month, int day)
        {
            this.year  = year;
            this.month = month;
            this.day   = day;
        }

        // 정수 비교값 — 내부 정렬/비교에 사용
        private int ToInt() => year * 10000 + month * 100 + day;

        public static int MonthsBetween(GameDate from, GameDate to)
        {
            return (to.year - from.year) * 12 + (to.month - from.month);
        }

        public static int DaysBetween(GameDate from, GameDate to)
        {
            // 단순 추정 (시즌 계산용, 윤년 무시)
            return (to.year - from.year) * 365
                 + (to.month - from.month) * 30
                 + (to.day - from.day);
        }

        // 윤년 무시 — TimeManager / TrainingSystem 양쪽이 동일 규칙 사용
        // _monthCumulative[m] = 1~m월 누적 일수 (m월 말일까지). 1년 365일 고정.
        private static readonly int[] _monthCumulative =
            { 0, 31, 59, 90, 120, 151, 181, 212, 243, 273, 304, 334, 365 };

        public GameDate AddDays(int days)
        {
            if (days <= 0) return this;

            // 연내 1-기반 통일일수 + days → 연도 진행분과 잔여 doy로 분해 (O(1))
            int totalDoy = _monthCumulative[month - 1] + day + days;
            int y        = year + (totalDoy - 1) / 365;
            int doy      = ((totalDoy - 1) % 365) + 1;

            int m = 1;
            while (m < 12 && _monthCumulative[m] < doy) m++;
            int d = doy - _monthCumulative[m - 1];

            return new GameDate(y, m, d);
        }

        public GameDate NextDay()
        {
            // 단일 일 진행 — 할당/분기 최소화 (TimeManager.StepOneDay 핫패스)
            int d = day + 1, m = month, y = year;
            if (d > _monthCumulative[m] - _monthCumulative[m - 1])
            {
                d = 1;
                m++;
                if (m > 12) { m = 1; y++; }
            }
            return new GameDate(y, m, d);
        }

        public int CompareTo(GameDate other) => ToInt().CompareTo(other.ToInt());
        public bool Equals(GameDate other)   => ToInt() == other.ToInt();

        public static bool operator <(GameDate a, GameDate b)  => a.ToInt() <  b.ToInt();
        public static bool operator >(GameDate a, GameDate b)  => a.ToInt() >  b.ToInt();
        public static bool operator <=(GameDate a, GameDate b) => a.ToInt() <= b.ToInt();
        public static bool operator >=(GameDate a, GameDate b) => a.ToInt() >= b.ToInt();
        public static bool operator ==(GameDate a, GameDate b) => a.ToInt() == b.ToInt();
        public static bool operator !=(GameDate a, GameDate b) => a.ToInt() != b.ToInt();

        public override bool Equals(object obj) => obj is GameDate d && Equals(d);
        public override int  GetHashCode()       => ToInt();
        public override string ToString()        => $"{year}/{month:D2}/{day:D2}";
    }
}
