namespace FootballTactician.Data
{
    public enum StaffRole
    {
        HeadCoach, AssistantCoach, PhysicalCoach, GoalkeeperCoach,
        HeadScout, RegionalScout, DataAnalyst,
        TeamDoctor, Physio, YouthCoordinator
    }

    public enum ContractStatus { Active, ExpiringSoon, Expired, LoanedOut, LoanedIn }

    public enum LeagueTier
    {
        First  = 1,
        Second = 2,
        Third  = 3,
        Fourth = 4,
        Fifth  = 5
    }

    public enum CompetitionType
    {
        League, NationalCup, LeagueCup, ContinentalCup, ContinentalLeague
    }

    public enum MatchResult    { Win, Draw, Loss }
    public enum TransferType   { Permanent, Loan, Free, Youth }
    public enum FinancialStatus { Excellent, Stable, Tight, Crisis }
    public enum InjurySeverity  { Minor, Moderate, Serious, Career }
    public enum TrainingIntensity { VeryLight, Light, Normal, Heavy, VeryHeavy }

    public enum BoardObjectiveResult  { Achieved, Failed, Pending, PartiallyMet }
    public enum BoardConfidenceLevel  { FullSupport, BackingManager, Concerned, UnderPressure, Sacked }
    public enum PressConferenceQuestion
    {
        NextMatchPreparation, RecentResults, PlayerInjuryUpdate, TransferPlans,
        TeamPerformance, ManagerJobSecurity, RivalryComment,
        YouthPlayerPromotion, TacticsExplained, NoComment,
    }
}
