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

    /// <summary>
    /// 선수의 국적. 값(순서)이 세이브에 들어가므로 <b>중간에 끼워 넣지 않는다</b> —
    /// 새 나라는 반드시 끝에 붙인다. 이름만 바꾸는 것은 안전하다.
    ///
    /// 2026-11-02 에 가상 국적 열여섯 개를 실제 나라 코드로 바꿨다. 값은 그대로이므로
    /// 예전 세이브도 열리지만, 그 안의 선수 국적 표기는 새 이름으로 보인다.
    /// </summary>
    public enum Nationality
    {
        // 유럽
        ENG, ESP, ITA, GER, FRA, NED, POR, BEL,
        TUR, SCO, RUS, UKR, AUT, SUI, DEN, SWE,
        NOR, POL, GRE, CZE, SRB, CRO,
        // 남아메리카
        BRA, ARG, URU, COL, CHI, PER,
        // 북중미
        MEX, USA, CAN, CRC,
        // 아시아
        JPN, KOR, CHN, KSA, IRN, AUS, QAT, UAE,
        // 아프리카
        EGY, MAR, TUN, ALG, NGA, GHA, SEN, RSA,
        // 오세아니아
        NZL,

        // ── 2026-11-03 에 더한 나라들 ──────────────────────────────
        // 값(순서)이 세이브에 들어가므로 반드시 **끝에만** 붙인다.
        ROU, HUN, BUL, SVK, SVN, BIH, ALB, FIN, ISL, IRL,
        WAL, CYP, ISR, KAZ,
        ECU, PAR, BOL, VEN,
        HON, GUA, PAN, JAM,
        IND, THA, VIE, IDN, MAS, SIN, UZB, IRQ, JOR,
        CIV, CMR, COD, MLI, ZAM, ANG, KEN,
        FIJ, PNG,

        // ── 2026-11-03 (3차) ──────────────────────────────────────
        GEO, ARM, AZE, BLR, MDA, LVA, LTU, EST, LUX, MLT,
        MNE, MKD, NIR, FRO, KOS,
        BFA, GUI, SDN, LBY, TAN, UGA, ETH, ZIM, MOZ, GAB,
        BEN, TOG, MAD, RWA, CPV,
        PHI, SYR, LIB, KUW, BHR, OMA, HKG, TPE, MYA, CAM,
        TJK, KGZ
    }

    public enum PlayerStatus  { Active, Injured, Suspended, OnLoan, Retired }
    public enum TransferStatus { NotListed, ListedForTransfer, ListedForLoan, WantAway }
    public enum MoraleLevel   { Superb, Good, Okay, Poor, Miserable }
}
