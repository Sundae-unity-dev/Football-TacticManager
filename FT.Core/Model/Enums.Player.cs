using System;

namespace FootballTactician.Data
{
    [Flags]
    public enum Position
    {
        None = 0,
        GK  = 1 << 0,
        CB  = 1 << 1,
        LB  = 1 << 2,
        RB  = 1 << 3,
        LWB = 1 << 4,
        RWB = 1 << 5,
        CDM = 1 << 6,
        CM  = 1 << 7,
        CAM = 1 << 8,
        LM  = 1 << 9,
        RM  = 1 << 10,
        LW  = 1 << 11,
        RW  = 1 << 12,
        ST  = 1 << 13,
        CF  = 1 << 14,
    }

    public enum PlayerRole
    {
        // ── Goalkeeper ──────────────────────────────
        Goalkeeper,
        SweeperKeeper,

        // ── Centre Back ─────────────────────────────
        CentralDefender,
        BallPlayingDefender,
        LimitedDefender,
        StopperCB,
        CoverCB,
        LiberoDefender,

        // ── Full Back / Wing Back ────────────────────
        Fullback,
        InvertedFullback,
        Wingback,
        InvertedWingback,
        CompleteWingback,

        // ── Defensive Midfielder ────────────────────
        Anchor,
        DeepLyingPlaymaker,
        BallWinningMidfielder,
        HalfBack,
        DefensiveMidfielder,
        SegundoVolante,

        // ── Central Midfielder ──────────────────────
        CentralMidfielder,
        BoxToBox,
        Carrilero,
        Mezzala,
        RoamingPlaymaker,
        AdvancedPlaymaker,

        // ── Attacking Midfielder ────────────────────
        AttackingMidfielder,
        ShadowStriker,
        Trequartista,
        Enganche,

        // ── Wide / Winger ────────────────────────────
        Winger,
        WideMidfielder,
        InsideForward,
        InvertedWinger,
        Raumdeuter,
        WidePlaymaker,
        WideTargetForward,

        // ── Striker / Forward ────────────────────────
        AdvancedForward,
        CompleteForward,
        Poacher,
        DeepLyingForward,
        FalseNine,
        PressingForward,
        TargetMan,
    }

    public enum RoleDuty { Defend, Support, Attack }

    public enum PreferredFoot { Left, Right, Either }

    public enum Personality
    {
        Leader,
        QuietPro,
        Ambitious,
        Troublemaker,
        Loyal,
        Mercenary,
        Mentee,
        Veteran
    }

    public enum Nationality
    {
        NOR, EST, VEL, KAR, SOL, DRA, MER, CAL,
        PAX, RYN, ALT, BOR, FEN, GAL, HYR, IRL
    }

    public enum PlayerStatus  { Active, Injured, Suspended, OnLoan, Retired }
    public enum TransferStatus { NotListed, ListedForTransfer, ListedForLoan, WantAway }
    public enum MoraleLevel   { Superb, Good, Okay, Poor, Miserable }
}
