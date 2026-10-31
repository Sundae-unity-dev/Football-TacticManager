namespace FootballTactician.Data
{
    public enum MatchEventType
    {
        KickOff, HalfTimeWhistle, FullTimeWhistle, ExtraTimeStart, PenaltyShootoutStart,
        Goal, OwnGoal, GoalDisallowed, HatTrick,
        PenaltyAwarded, PenaltyScored, PenaltyMissed, PenaltySaved,
        FreeKickGoal, CornerKickGoal, DirectFreeKick,
        BigSave, PostHit, BigChanceMissed,
        YellowCard, SecondYellow, RedCard,
        Substitution, InjuryStop,
        VARReview,
    }

    public enum MatchPhase
    {
        PreMatch, FirstHalf, HalfTime, SecondHalf,
        ExtraTimeFirst, ExtraTimeSecond, PenaltyShootout, PostMatch,
    }

    public enum TrainingCategory
    {
        Tactics, Physical, Technical, Defending, Attacking,
        SetPieces, Recovery, Goalkeeping, YouthDevelopment, MatchPrepare,
    }

    public enum CommentaryEventType
    {
        MatchStart, GoalScored, OwnGoal, GoalMissed, BigSave, PostHit,
        YellowCard, RedCard, Substitution, PenaltyScored, PenaltyMissed,
        HalfTimeReport, FullTimeResult, HatTrickAlert, CleanSheetAlert,
        PreMatchPressConference, PostMatchPressConference,
        TransferRumor, PlayerContractAlert, InjuryReport,
        BoardStatement, LeagueTableUpdate,
    }

    public enum CommentaryTone { Neutral, Excited, Tense, Disappointed, Humorous }

    public enum WeatherCondition { Sunny, Cloudy, Rainy, HeavyRain, Snowy, Windy, Hot }

    public enum PitchCondition   { Perfect, Good, Soft, Heavy, Frozen, Dry, Artificial }
}
