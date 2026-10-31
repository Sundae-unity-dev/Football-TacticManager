using System;

namespace FootballTactician.Data
{
    [Serializable]
    public class PlayerStats
    {
        public int   appearances;
        public int   goals;
        public int   assists;
        public int   yellowCards;
        public int   redCards;
        public float averageRating;
        public float minutesPlayed;
        public float xG;
        public float xA;
        public int   cleanSheets;
        public float progressiveCarries;
        public float pressureSuccessRate;

        /// <summary>
        /// 한 시즌치를 통산에 더한다. 평점은 경기 수로 가중한다 —
        /// 그냥 평균 내면 두 경기 뛴 시즌이 서른 경기 뛴 시즌과 같은 무게가 된다.
        /// </summary>
        public void Add(PlayerStats s)
        {
            if (s == null) return;

            float totalGames = appearances + s.appearances;
            if (totalGames > 0)
                averageRating = (averageRating * appearances + s.averageRating * s.appearances) / totalGames;

            appearances += s.appearances;
            goals += s.goals;
            assists += s.assists;
            yellowCards += s.yellowCards;
            redCards += s.redCards;
            minutesPlayed += s.minutesPlayed;
            xG += s.xG;
            xA += s.xA;
            cleanSheets += s.cleanSheets;
            progressiveCarries += s.progressiveCarries;
        }
    }

    [Serializable]
    public class MatchRating
    {
        public int matchId;
        public float  rating;        // 1.0~10.0
        public int    goals;
        public int    assists;
        public float  xGInvolved;
        public int    minutesPlayed;
        public bool   manOfTheMatch;
    }
}
