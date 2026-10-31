using System.Collections.Generic;
using FootballTactician.Data;

namespace FootballTactician.Simulation
{
    public class MatchLineup
    {
        public int        clubId;
        public TacticsData tactics;              // slots[i].assignedPlayerId 로 선발 11인 인코딩
        public List<int>  benchIds = new();
    }

    public class MatchSimContext
    {
        public int              fixtureId;
        public MatchLineup      home;
        public MatchLineup      away;
        public WeatherCondition weather = WeatherCondition.Cloudy;
        public PitchCondition   pitch   = PitchCondition.Good;
        public int              seed    = -1;    // -1 = 무작위 시드

        /// <summary>교체 한도 등 경기 규정. 리그에서 받아 넣는다.</summary>
        public MatchRules       rules   = new MatchRules();
    }
}
