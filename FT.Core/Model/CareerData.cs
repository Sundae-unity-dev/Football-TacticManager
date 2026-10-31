using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json.Serialization;

namespace FootballTactician.Data
{
    [Serializable]
    public sealed class JobOffer
    {
        public int clubId;
        public int requiredReputation;
        public int leagueId;
    }

    /// <summary>한 구단에서 보낸 기간.</summary>
    [Serializable]
    public sealed class CareerSpell
    {
        public int clubId;
        public string clubName;
        public int fromYear;
        public int toYear;
        public int wins, draws, losses;
        public bool endedSacked;

        [JsonIgnore] public int Games => wins + draws + losses;
        [JsonIgnore] public float WinRate => Games == 0 ? 0f : wins / (float)Games;
    }
}
