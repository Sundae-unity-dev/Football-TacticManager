using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json.Serialization;

namespace FootballTactician.Data
{
    /// <summary>진행 중인 임대 한 건.</summary>
    [Serializable]
    public sealed class LoanSpell
    {
        public int playerId;
        public int parentClubId;
        public int hostClubId;
        public GameDate startDate;
        public int endYear;
        public int wageSharePercent;
    }
}
