using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json.Serialization;

namespace FootballTactician.Data
{
    /// <summary>한 리그 한 시즌의 수상자. 선수 id 만 들고 있어 세이브가 가볍다.</summary>
    [Serializable]
    public sealed class SeasonAwardSet
    {
        public int leagueId;
        public int year;

        // 이름을 함께 남긴다. id 만 두면 그 선수가 은퇴해 세계에서 사라졌을 때
        // 기록실이 "(은퇴)" 만 늘어놓는다 — 수상자를 잊는 기록은 기록이 아니다.
        public int topScorerId;
        public string topScorerName;
        public int topScorerGoals;

        public int topAssistId;
        public string topAssistName;
        public int topAssists;

        public int bestPlayerId;
        public string bestPlayerName;
        public float bestRating;

        public int bestYoungId;
        public string bestYoungName;
        public float bestYoungRating;

        public int managerClubId;
        public string managerClubName;
        public int managerPlacesGained;
    }
}
