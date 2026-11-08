using System;
using System.Linq;
using System.Text.Json.Serialization;
using System.Collections.Generic;

namespace FootballTactician.Data
{
    [Serializable]
    public class LeagueData
    {
        public int    id;
        public string name;
        public string shortName;
        public string regionName;
        public LeagueTier tier;
        public CompetitionType type;

        public int numberOfTeams;
        public int promotionSpots;
        public int relegationSpots;
        public int playoffSpots;

        public List<int> clubIds;
        public int?      promotionLeagueId;
        public int?      relegationLeagueId;

        /// <summary>
        /// 지난 시즌 우승 구단. 0 이면 아직 한 시즌도 끝나지 않았다.
        ///
        /// 시즌이 넘어가면 <see cref="LeagueSeason"/> 은 통째로 새것이 된다. 챔피언 결정전은
        /// 그 경계에서 치러지므로(⑥), 결과를 리그 쪽에 남겨 두지 않으면 화면이 "누가
        /// 우승했는지"를 영영 물어볼 수 없다.
        /// </summary>
        public int lastChampionClubId;

        /// <summary>그 우승이 몇 년도 시즌이었는지. 0 이면 없음.</summary>
        public int lastChampionYear;

        public LeagueSeason       currentSeason;
        public TransferWindowData transferWindow;
        public LeagueRules        rules;
        public MatchRules         matchRules;
    }

    [Serializable]
    public class LeagueSeason
    {
        public int year;
        public GameDate startDate;
        public GameDate endDate;
        public int currentMatchday;
        public int totalMatchdays;

        public List<StandingEntry> standings;
        public List<FixtureData>   fixtures;

        /// <summary>
        /// 스플릿이 일어난 라운드. 0 이면 아직 갈라지지 않았거나 스플릿이 없는 리그다
        /// (docs/리그-제도.md ⑤).
        /// </summary>
        public int splitMatchday;

        /// <summary>구단 → 조. 0 이 상위, 1 이 하위. 스플릿 전에는 비어 있다.</summary>
        public Dictionary<int, int> splitGroups;

        /// <summary>
        /// 구단 → 승점 보정. 승점 절반 규칙이 여기로 들어온다(보통 음수).
        ///
        /// 순위표는 매번 경기에서 다시 세므로, 줄인 승점을 따로 적어 두어야 남는다.
        /// </summary>
        public Dictionary<int, int> pointsCarry;

        /// <summary>
        /// 플레이오프로 가린 우승팀. <b>0 이면 정규리그 1위가 곧 우승팀</b>이다
        /// (docs/리그-제도.md ⑥).
        ///
        /// 0 은 저장하지 않으므로 예전 세이브도 그대로 읽힌다.
        /// </summary>
        public int championClubId;

        /// <summary>일정을 다 치렀는지. "시즌이 끝났다" 의 뜻은 여기 한 곳에만 둔다.</summary>
        [JsonIgnore] public bool Finished => fixtures.All(f => f.isPlayed);
    }

    [Serializable]
    public class StandingEntry
    {
        public int clubId;
        public int position;
        public int played;
        public int won;
        public int drawn;
        public int lost;
        public int goalsFor;
        public int goalsAgainst;
        public int points;

        [JsonIgnore] public int GoalDifference => goalsFor - goalsAgainst;
    }

    [Serializable]
    public class FixtureData
    {
        public int      id;
        public int      homeClubId;
        public int      awayClubId;
        public GameDate date;
        public int      matchday;
        public bool     isPlayed;

        public MatchResultData result;
    }

    [Serializable]
    public class MatchResultData
    {
        public int homeGoals;
        public int awayGoals;
        public List<MatchEvent> events;
        public MatchStats homeStats;
        public MatchStats awayStats;
    }

    [Serializable]
    public class MatchEvent
    {
        public int            minute;
        public MatchEventType eventType;
        public int            clubId;          // 이벤트 주체 클럽 (득점/카드/세이브)
        public int            playerId;
        public int?           assistPlayerId;
    }

    [Serializable]
    public class MatchStats
    {
        public int   shots;
        public int   shotsOnTarget;
        public float xG;
        public int   possession;
        public int   passes;
        public float passAccuracy;
        public int   tackles;
        public int   fouls;
        public int   corners;
        public int   interceptions;
        public int   aerialDuelsWon;
        public int   bigChances;
        public int   bigChancesMissed;
        public int   clearances;
        public int   yellowCards;
        public int   redCards;
    }

    [Serializable]
    public class TransferWindowData
    {
        public GameDate summerStart;
        public GameDate summerEnd;
        public GameDate winterStart;
        public GameDate winterEnd;

        public bool IsWindowOpen(GameDate currentDate)
        {
            return IsInWindow(currentDate, summerStart, summerEnd) ||
                   IsInWindow(currentDate, winterStart, winterEnd);
        }

        private static bool IsInWindow(GameDate date, GameDate start, GameDate end)
        {
            return date >= start && date <= end;
        }
    }
}
