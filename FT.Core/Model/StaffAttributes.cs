using System;

namespace FootballTactician.Data
{
    /// <summary>
    /// 모든 스탭 공통 능력치.
    /// 역할 전용 능력치는 StaffData의 해당 필드(coaching/scouting/medical/analytics)에서 관리.
    /// </summary>
    [Serializable]
    public class StaffAttributesCommon
    {
        public int motivation;
        public int manManagement;
    }

    [Serializable]
    public class CoachingAttributes
    {
        public int tacticalKnowledge;
        public int technicalCoaching;
        public int physicalCoaching;
        public int mentalCoaching;
        public int gkCoaching;
    }

    [Serializable]
    public class ScoutingAttributes
    {
        public int judgingPlayerAbility;
        public int judgingPlayerPotential;
        public int adaptability;
    }

    [Serializable]
    public class MedicalAttributes
    {
        public int physioTherapy;
        public int sportsScience;
    }

    [Serializable]
    public class AnalyticsAttributes
    {
        public int dataAnalysis;
    }
}
