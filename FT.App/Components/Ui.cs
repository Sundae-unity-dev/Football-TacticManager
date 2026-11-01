using System.Linq;
using System.Reflection;
using FootballTactician.Data;
using FootballTactician.World;

namespace FT.App.Components;

/// <summary>화면 공통 표기 규칙. 포지션 색, 금액 단위, 평점 색을 한 곳에 둔다.</summary>
public static class Ui
{
    /// <summary>
    /// 지금 돌고 있는 버전("v0.1.0"). 시작 화면 구석에 찍어 둔다 —
    /// Pages 가 파일을 캐시하므로, 고친 것이 올라갔는지 눈으로 보는 것이 가장 빠르다.
    /// 값은 FT.App.csproj 의 &lt;Version&gt; 하나다.
    /// </summary>
    public static string Version { get; } = ReadVersion();

    static string ReadVersion()
    {
        // InformationalVersion 쪽이 "0.1.0+커밋해시" 까지 들고 있어 먼저 본다.
        // 트리머가 어셈블리 속성을 지우는 일은 없지만, 없어도 화면이 비지 않게
        // 어셈블리 이름의 버전(0.1.0.0)으로 떨어뜨린다.
        var attr = typeof(Ui).Assembly
            .GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion;
        if (!string.IsNullOrEmpty(attr))
        {
            int plus = attr.IndexOf('+');
            return "v" + (plus < 0 ? attr : attr[..plus]);
        }

        var v = typeof(Ui).Assembly.GetName().Version;
        return v is null ? "" : $"v{v.Major}.{v.Minor}.{v.Build}";
    }

    public static string PosLabel(Position p) => p switch
    {
        Position.GK => "GK",
        Position.CB => "CB",
        Position.LB => "LB",
        Position.RB => "RB",
        Position.LWB => "LWB",
        Position.RWB => "RWB",
        Position.CDM => "DM",
        Position.CM => "CM",
        Position.CAM => "AM",
        Position.LM => "LM",
        Position.RM => "RM",
        Position.LW => "LW",
        Position.RW => "RW",
        Position.ST => "ST",
        Position.CF => "CF",
        _ => p.ToString(),
    };

    /// <summary>비트마스크에 여러 포지션이 섞여 있으면 첫 번째만 표기한다.</summary>
    public static string PosLabelOf(PlayerData p)
    {
        var pos = p.preferredPosition != Position.None ? p.preferredPosition : p.positionFlags;
        foreach (Position flag in Enum.GetValues<Position>())
            if (flag != Position.None && (pos & flag) != 0) return PosLabel(flag);
        return "—";
    }

    public static string PosClass(Position pos)
    {
        if ((pos & Position.GK) != 0) return "gk";
        if ((pos & (Position.CB | Position.LB | Position.RB | Position.LWB | Position.RWB)) != 0) return "def";
        if ((pos & (Position.ST | Position.CF | Position.LW | Position.RW)) != 0) return "att";
        return "mid";
    }

    public static string PosClassOf(PlayerData p)
        => PosClass(p.preferredPosition != Position.None ? p.preferredPosition : p.positionFlags);

    /// <summary>유로 금액을 읽기 쉬운 단위로.</summary>
    public static string Money(long euros)
    {
        if (euros >= 1_000_000_000) return $"{euros / 1_000_000_000d:F2}B€";
        if (euros >= 1_000_000) return $"{euros / 1_000_000d:F1}M€";
        if (euros >= 1_000) return $"{euros / 1_000d:F0}K€";
        return $"{euros}€";
    }

    public static string Money(int euros) => Money((long)euros);

    public static string Wage(int weekly) => $"{Money(weekly)}/주";

    /// <summary>CA 를 0~100 막대 비율로. 1부 리그는 대략 90~190 사이에 몰린다.</summary>
    public static int CaBar(float ca) => (int)Math.Clamp((ca - 70f) / 120f * 100f, 4, 100);

    public static string BarClass(float value01)
        => value01 >= 0.66f ? "" : value01 >= 0.4f ? "warn" : "bad";

    public static string ConditionClass(float condition)
        => condition >= 85 ? "" : condition >= 65 ? "warn" : "bad";

    public static string MoraleLabel(float morale) => morale switch
    {
        >= 80 => "아주 좋음",
        >= 65 => "좋음",
        >= 50 => "보통",
        >= 35 => "나쁨",
        _ => "아주 나쁨",
    };

    public static string FinanceLabel(FinancialStatus s) => s switch
    {
        FinancialStatus.Excellent => "매우 안정",
        FinancialStatus.Stable => "안정",
        FinancialStatus.Tight => "빠듯함",
        _ => "위기",
    };

    public static string FinanceBadge(FinancialStatus s) => s switch
    {
        FinancialStatus.Excellent or FinancialStatus.Stable => "good",
        FinancialStatus.Tight => "warn",
        _ => "bad",
    };

    public static string BoardLabel(BoardConfidenceLevel l) => l switch
    {
        BoardConfidenceLevel.FullSupport => "전폭 지지",
        BoardConfidenceLevel.BackingManager => "지지",
        BoardConfidenceLevel.Concerned => "우려",
        BoardConfidenceLevel.UnderPressure => "압박",
        _ => "경질",
    };

    public static string MentalityLabel(TeamMentality m) => m switch
    {
        TeamMentality.VeryDefensive => "매우 수비적",
        TeamMentality.Defensive => "수비적",
        TeamMentality.Balanced => "균형",
        TeamMentality.Attacking => "공격적",
        _ => "매우 공격적",
    };

    public static string BuildUpLabel(BuildUpStyle b) => b switch
    {
        BuildUpStyle.ShortPassing => "짧은 패스",
        BuildUpStyle.Mixed => "혼합",
        BuildUpStyle.LongBall => "롱볼",
        _ => "다이렉트",
    };

    public static string LineLabel(DefensiveLine d) => d switch
    {
        DefensiveLine.VeryLow => "매우 낮음",
        DefensiveLine.Low => "낮음",
        DefensiveLine.Medium => "보통",
        DefensiveLine.High => "높음",
        _ => "매우 높음",
    };

    public static string EventLabel(MatchEventType t) => t switch
    {
        MatchEventType.Goal => "골",
        MatchEventType.CornerKickGoal => "코너 골",
        MatchEventType.FreeKickGoal => "프리킥 골",
        MatchEventType.PenaltyScored => "페널티 골",
        MatchEventType.PenaltyMissed => "페널티 실축",
        MatchEventType.PenaltySaved => "페널티 선방",
        MatchEventType.PenaltyAwarded => "페널티 선언",
        MatchEventType.YellowCard => "경고",
        MatchEventType.RedCard => "퇴장",
        MatchEventType.Substitution => "교체",
        MatchEventType.InjuryStop => "부상",
        MatchEventType.SecondYellow => "경고 누적 퇴장",
        MatchEventType.BigChanceMissed => "결정적 기회 실패",
        MatchEventType.PostHit => "골대",
        MatchEventType.BigSave => "선방",
        _ => t.ToString(),
    };

    public static string SeverityLabel(InjurySeverity s) => s switch
    {
        InjurySeverity.Minor => "경미",
        InjurySeverity.Moderate => "중간",
        InjurySeverity.Serious => "중상",
        _ => "커리어 위협",
    };

    static readonly Dictionary<Nationality, string> NationCodes =
        NameForge.All.ToDictionary(n => n.Nationality, n => n.Code);

    /// <summary>국적 표기는 모델 enum 이 아니라 가상 국가 코드로 보여준다.</summary>
    public static string NationCode(Nationality n)
        => NationCodes.TryGetValue(n, out var code) ? code : n.ToString();

    public static string DateLabel(GameDate d) => $"{d.year}.{d.month:D2}.{d.day:D2}";

    /// <summary>구단 색이 너무 어두우면 글자를 밝게, 밝으면 어둡게.</summary>
    public static string TextOn(string hex)
    {
        if (string.IsNullOrWhiteSpace(hex) || hex.Length < 7) return "#fff";
        try
        {
            int r = Convert.ToInt32(hex.Substring(1, 2), 16);
            int g = Convert.ToInt32(hex.Substring(3, 2), 16);
            int b = Convert.ToInt32(hex.Substring(5, 2), 16);
            return (r * 299 + g * 587 + b * 114) / 1000 > 140 ? "#0A1220" : "#FFFFFF";
        }
        catch { return "#fff"; }
    }
}
