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
