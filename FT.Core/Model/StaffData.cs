using System;

namespace FootballTactician.Data
{
    [Serializable]
    public class StaffData
    {
        public int         id;
        public string      firstName;
        public string      lastName;
        public int         age;
        public Nationality nationality;
        public StaffRole   role;
        public int         reputation;  // 1~5 — 능력 있는 스탭일수록 높음, 영입 비용에 영향

        public StaffAttributesCommon common;
        public CoachingAttributes   coaching;   // HeadCoach, AssistantCoach, PhysicalCoach, GoalkeeperCoach
        public ScoutingAttributes   scouting;   // HeadScout, RegionalScout
        public MedicalAttributes    medical;    // TeamDoctor, Physio
        public AnalyticsAttributes  analytics;  // DataAnalyst
        public ContractData         contract;
        public int                  clubId;

        // HeadCoach 스탭에만 사용. 다른 역할은 null.
        public ManagerProfile managerProfile;

        public string FullName => $"{firstName} {lastName}";
    }

}
