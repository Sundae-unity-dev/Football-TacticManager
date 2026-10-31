using System;
using System.Collections.Generic;

namespace FootballTactician.Data
{
    public enum BoardObjectiveType
    {
        LeagueFinish,            // targetValue = 최대 허용 순위 (작을수록 어려움)
        AvoidRelegation,         // targetValue = 강등권 시작 순위
        WinDomesticCup,          // targetValue = 트로피 ID (-1=미지정)
        WinContinentalCup,
        ReachContinentalKnockout,
        DevelopYouthMinutes,     // targetValue = U21 누적 출전 분
        StayWithinBudget,        // targetValue = 0 (시즌 말 적자 허용 분기점)
        ImproveOnLastSeason,     // targetValue = 지난 시즌 최종 순위
    }

    [Serializable]
    public class BoardObjective
    {
        public BoardObjectiveType    type;
        public int                   targetValue;
        public int                   progressValue;
        public GameDate              deadline;
        public BoardObjectiveResult  result = BoardObjectiveResult.Pending;
        public int                   weight = 3;     // 1(가벼움) ~ 5(필수)
        public string                description;
    }

    /// <summary>한 경기 결과로부터 이사회 신뢰도를 갱신하기 위한 입력.</summary>
    [Serializable]
    public class BoardMatchOutcome
    {
        public bool wasWin;
        public bool wasDraw;
        public int  ourClubReputation;     // 1~5
        public int  opponentReputation;    // 1~5
        public bool wasHome;
    }
}
