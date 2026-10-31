using System;
using System.Text.Json.Serialization;
using System.Collections.Generic;

namespace FootballTactician.Data
{
    [Serializable]
    public class ClubData
    {
        public int    id;
        public string name;
        public string shortName;
        public string leagueId;
        public int    reputation;               // 1~5

        public ClubFinances finances;
        public StadiumData  stadium;
        public int          academyRating;          // 1~5
        public int          trainingFacilitiesRating; // 1~5 (아카데미와 별도 — 선수 성장 속도에 영향)

        public List<int> playerIds;
        public List<int> staffIds;
        public int       managerId;

        public int?      parentClubId;
        public List<int> affiliateClubIds;        // 제휴 구단 (임대 네트워크)

        public ClubHistory      history;
        public ClubColors       colors;
        public DressingRoomMorale dressingRoom;
        public HomegrownStatus  homegrownPolicy;  // 클럽 홈그로운 정책 설정

        // 전술판 저장 슬롯 (최대 10개)
        /// <summary>이사회가 더 얹어 준 급여 예산. 재산정 때 날아가지 않게 따로 들고 있는다.</summary>
        public int boardWageBonus;

        public List<TacticsData> savedTactics  = new List<TacticsData>();
        public int               activeTacticsId = -1; // TacticsData.id 참조
    }

    [Serializable]
    public class ClubFinances
    {
        public long          balance;
        public int           transferBudget;
        public int           weeklyWageBudget;
        public int           currentWageSpend;
        public long          annualRevenue;
        public long          annualExpenditure;
        public FinancialStatus status;

        public SponsorContract mainSponsor;
        public SponsorContract kitSponsor;

        [JsonIgnore] public int WageBudgetRemaining => weeklyWageBudget - currentWageSpend;
    }

    [Serializable]
    public class SponsorContract
    {
        public string  sponsorName;
        public int     annualFee;
        public GameDate expiryDate;
        public int     performanceBonus;
    }

    [Serializable]
    public class StadiumData
    {
        public string name;
        public int    capacity;
        public int    currentCondition;     // 1~100
        public int    ticketPriceAverage;

        // 분위기 — 홈 팀 선수 퍼포먼스 보정에 영향
        public float  atmosphereRating;     // 0~100 (경기장 분위기)
        public float  averageAttendanceRate; // 0~100 (평균 점유율 %)
        public int    fanBaseSize;          // 1(소규모) ~ 5(대형 팬덤)
        public float  homeAdvantageBonus;  // 홈 이점 보정값 (0~10)
    }

    [Serializable]
    public class ClubHistory
    {
        public int              founded;
        public int              leagueTitles;
        public int              cupTitles;
        public int              continentalTitles;
        public List<SeasonRecord> recentSeasons;
    }

    [Serializable]
    public class SeasonRecord
    {
        public int    season;
        public string leagueId;
        public int    finalPosition;
        public int    points;
        public int    goalsFor;
        public int    goalsAgainst;
    }

    [Serializable]
    public class ClubColors
    {
        public string primaryHex;
        public string secondaryHex;
    }
}
