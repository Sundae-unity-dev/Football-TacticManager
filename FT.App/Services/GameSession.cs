using FootballTactician.Balance;
using System.Linq;
using FootballTactician.Data;
using FootballTactician.Simulation;
using FootballTactician.World;

namespace FT.App.Services;

/// <summary>
/// 화면이 공유하는 한 판의 상태. 페이지는 여기서 읽고, 바뀌면 Changed 로 알린다.
/// 규칙은 전부 FT.Core 에 있고 이 클래스는 들고 전달하는 일만 한다.
/// </summary>
public sealed class GameSession(SaveStore store)
{
    public GameWorld? World { get; private set; }
    public bool HasGame => World != null && World.ManagedClubId > 0;

    /// <summary>마지막으로 치른 경기 결과 — 경기 화면이 보여준다.</summary>
    public MatchResultData? LastResult { get; private set; }
    public FixtureData? LastFixture { get; private set; }

    public event Action? Changed;
    public void NotifyChanged() => Changed?.Invoke();

    // ── 시작과 저장 ───────────────────────────────────────────────────────

    public GameWorld NewWorld(int seed)
    {
        World = WorldGenerator.Create(seed);
        LastResult = null;
        LastFixture = null;
        NotifyChanged();
        return World;
    }

    public async Task TakeChargeAsync(int clubId, string managerName)
    {
        if (World == null) return;
        ManagerCareer.TakeCharge(World, clubId, managerName);
        NotifyChanged();
        await SaveAsync();
    }

    /// <summary>
    /// 새로고침으로 메모리가 비었을 때 저장된 판을 되살린다. 판당 한 번만 시도한다.
    /// 자동 저장이 있으므로 새로고침해도 진행이 사라지지 않아야 한다.
    /// </summary>
    public async Task<bool> TryResumeAsync()
    {
        if (HasGame || resumeTried) return HasGame;
        resumeTried = true;
        try { return await LoadAsync(); }
        catch { return false; }
    }

    bool resumeTried;

    public async Task<bool> LoadAsync()
    {
        var w = await store.LoadAsync();
        if (w == null) return false;
        World = w;
        LastResult = null;
        LastFixture = null;
        NotifyChanged();
        return true;
    }

    /// <summary>
    /// 저장 비용을 단계별로 잰다. 브라우저에서만 드러나는 병목을 찾을 때 임시로 화면에 붙여 쓴다.
    /// 평소에는 호출하는 곳이 없다.
    /// </summary>
    public string ProfileSave()
    {
        if (World is not { } w) return "진행 중인 판이 없습니다.";
        var p = SaveGame.Profile(w);
        return $"JSON {p.jsonMs}ms · gzip {p.gzipMs}ms · base64 {p.base64Ms}ms · " +
               $"{p.rawBytes / 1024 / 1024f:F1}MB → {p.packedBytes / 1024}KB";
    }

    public async Task SaveAsync()
    {
        if (World == null) return;
        await store.SaveAsync(World, World.MyClub?.name ?? "");
    }

    // ── 진행 ──────────────────────────────────────────────────────────────

    public FixtureData? NextFixture => World == null ? null : GameEngine.NextUserFixture(World);

    /// <summary>다음 라운드를 치른다. 결과는 LastResult 에 담긴다.</summary>
    public void PlayNextRound()
    {
        if (World == null || World.Sacked) return;

        var fixture = GameEngine.NextUserFixture(World);
        if (fixture == null) return;

        LastFixture = fixture;
        LastResult = GameEngine.PlayNextRound(World);
        NotifyChanged();
    }

    /// <summary>
    /// 경기를 치르고 저장한다. 새로고침해도 진행이 날아가지 않게 한다.
    ///
    /// 결과를 먼저 그리고 저장은 그 뒤에 한다. 브라우저에서 직렬화에 2초가 걸리는데,
    /// 저장을 먼저 하면 그동안 화면이 옛 결과를 들고 멈춰 있다.
    /// </summary>
    public async Task PlayNextRoundAsync(int rounds = 1)
    {
        for (int i = 0; i < rounds; i++)
        {
            if (World == null || World.Sacked || NextFixture == null) break;
            PlayNextRound();
        }

        NotifyChanged();
        await Task.Yield();     // 결과를 먼저 그린다
        await SaveAsync();
    }

    // ── 직접 치르는 경기 ──────────────────────────────────────────────────

    /// <summary>진행 중인 경기. 블록 단위로 넘기며 교체와 지시를 넣을 수 있다.</summary>
    public MatchSession? Live { get; private set; }
    public FixtureData? LiveFixture { get; private set; }

    /// <summary>플레이어 구단이 홈인지. 교체와 지시를 어느 쪽에 걸지 정한다.</summary>
    public bool LiveIsHome => LiveFixture?.homeClubId == World?.ManagedClubId;

    public void BeginLiveMatch()
    {
        if (World is not { } w || w.Sacked) return;

        var fixture = GameEngine.NextUserFixture(w);
        if (fixture == null) return;

        Live = GameEngine.BeginUserMatch(w, fixture);
        LiveFixture = fixture;
        if (Live != null)
        {
            // 감독이 직접 교체하는 쪽은 자동 교체를 끈다.
            Live.AutoSubstitutions = LiveIsHome ? (false, true) : (true, false);
        }
        NotifyChanged();
    }

    public void LiveAdvance(int blocks = 1)
    {
        if (Live == null || Live.Finished) return;
        for (int i = 0; i < blocks && !Live.Finished; i++) Live.AdvanceBlock();
        NotifyChanged();
    }

    public void LiveAdvanceToHalfTime()
    {
        if (Live == null) return;
        Live.AdvanceToHalfTime();
        NotifyChanged();
    }

    /// <summary>경기를 끝내고 결과를 세계에 반영한다.</summary>
    public async Task FinishLiveMatchAsync()
    {
        if (World is not { } w || Live == null || LiveFixture == null) return;

        LastFixture = LiveFixture;
        LastResult = GameEngine.CompleteUserMatch(w, LiveFixture, Live);
        Live = null;
        LiveFixture = null;

        NotifyChanged();
        await SaveAsync();
    }

    public MatchSession.SubResult LiveSubstitute(int outId, int inId)
    {
        if (Live == null) return MatchSession.SubResult.NotOnPitch;
        var r = Live.Substitute(LiveIsHome, outId, inId);
        NotifyChanged();
        return r;
    }

    public void LiveSetMentality(TeamMentality m)
    {
        Live?.SetMentality(LiveIsHome, m);
        NotifyChanged();
    }

    public void LiveSetPressing(int intensity)
    {
        Live?.SetPressing(LiveIsHome, intensity);
        NotifyChanged();
    }

    // ── 이적 ──────────────────────────────────────────────────────────────

    public bool WindowOpen => World != null && TransferMarket.IsWindowOpen(World);
    public string WindowLabel => World == null ? "—" : TransferMarket.WindowLabel(World);

    /// <summary>제안을 보내고, 합의되면 바로 체결한다.</summary>
    public async Task<TransferMarket.OfferOutcome> MakeOfferAsync(
        PlayerData target, long fee, int weeklyWage, int years)
    {
        if (World is not { } w)
            return new TransferMarket.OfferOutcome { Message = "진행 중인 판이 없습니다." };

        if (!TransferMarket.IsWindowOpen(w))
            return new TransferMarket.OfferOutcome { Message = "이적 창구가 닫혀 있습니다." };

        var offer = TransferMarket.BuildOffer(w, target, fee, weeklyWage, years);
        var outcome = TransferMarket.Evaluate(w, offer);

        if (outcome.Accepted)
        {
            TransferMarket.Execute(w, offer);
            w.Post($"{target.FullName} 영입",
                   $"{w.Club(offer.toClubId)?.name} 에서 이적료 {fee / 1_000_000d:F1}M€ 에 영입했습니다.",
                   InboxKind.Transfer);
            NotifyChanged();
            await SaveAsync();
        }

        return outcome;
    }

    /// <summary>내 구단 선수를 이적 목록에 올리거나 내린다.</summary>
    public async Task ToggleTransferListAsync(PlayerData p)
    {
        p.transferStatus = p.transferStatus == TransferStatus.ListedForTransfer
            ? TransferStatus.NotListed
            : TransferStatus.ListedForTransfer;
        NotifyChanged();
        await SaveAsync();
    }

    // ── 재계약 ────────────────────────────────────────────────────────────

    public IEnumerable<PlayerData> ExpiringContracts =>
        World == null ? [] : ContractTalks.Expiring(World, World.ManagedClubId);

    public int MonthsLeft(PlayerData p) => World == null ? 0 : ContractTalks.MonthsLeft(World, p);

    public async Task<ContractTalks.Outcome> OfferRenewalAsync(
        PlayerData p, int weeklyWage, int years)
    {
        if (World is not { } w)
            return new ContractTalks.Outcome { Message = "진행 중인 판이 없습니다." };

        var outcome = ContractTalks.Evaluate(w, p, weeklyWage, years);
        if (outcome.Accepted)
        {
            ContractTalks.Sign(w, p, weeklyWage, years);
            w.ContractWarnings.Remove(p.id);
            NotifyChanged();
            await SaveAsync();
        }
        return outcome;
    }

    // ── 훈련 ──────────────────────────────────────────────────────────────

    public TrainingPlan? Training => World?.Training;

    public async Task SetTrainingAsync(Action<TrainingPlan> change)
    {
        if (World is not { } w) return;
        change(w.Training);
        NotifyChanged();
        await SaveAsync();
    }

    // ── 임대 ──────────────────────────────────────────────────────────────

    public IEnumerable<PlayerData> Loanable =>
        World == null ? [] : LoanMarket.Loanable(World, World.ManagedClubId);

    public IEnumerable<LoanSpell> LoansOut =>
        World == null ? [] : World.LoansOut();

    public List<ClubData> Suitors(PlayerData p)
        => World == null ? new List<ClubData>() : LoanMarket.Suitors(World, p);

    public async Task<LoanMarket.Outcome> SendOnLoanAsync(PlayerData p, ClubData host)
    {
        if (World is not { } w)
            return new LoanMarket.Outcome { Message = "진행 중인 판이 없습니다." };

        var outcome = LoanMarket.Evaluate(w, p, host);
        if (outcome.Accepted)
        {
            LoanMarket.Send(w, p, host);
            w.Post($"{p.FullName} 임대",
                   $"{host.name} 으로 시즌 말까지 임대 보냈습니다.", InboxKind.Transfer);
            NotifyChanged();
            await SaveAsync();
        }
        return outcome;
    }

    public async Task RecallLoanAsync(int playerId)
    {
        if (World is not { } w) return;
        var p = w.Player(playerId);
        if (p == null) return;

        LoanMarket.Recall(w, p);
        w.Post($"{p.FullName} 임대 복귀", "임대를 중도에 끝내고 불러들였습니다.", InboxKind.Transfer);
        NotifyChanged();
        await SaveAsync();
    }

    // ── 세트피스 ──────────────────────────────────────────────────────────

    PlayerData?[] Starters()
    {
        if (World is not { } w || ActiveTactics is not { } tac) return Array.Empty<PlayerData?>();
        return tac.slots.OrderBy(s => s.slotIndex).Select(s => w.Player(s.assignedPlayerId)).ToArray();
    }

    public float AerialThreat => SetPieces.AerialThreat(Starters()!);
    public float MovementThreat => SetPieces.MovementThreat(Starters()!);

    public SetPieceDelivery CornerDelivery
        => ActiveTactics is { } t ? SetPieces.DeliveryOf(t, SetPieces.Kind.Corner) : SetPieceDelivery.Inswinger;

    public SetPieceDelivery SuggestedDelivery => SetPieces.SuggestDelivery(Starters()!);

    public PlayerData? CornerTaker
        => SetPieces.Taker(Starters()!, ActiveTactics, SetPieces.Kind.Corner);

    public PlayerData? PenaltyTaker
        => SetPieces.Taker(Starters()!, ActiveTactics, SetPieces.Kind.Penalty);

    /// <summary>
    /// 배달 방식별로 이 스쿼드가 다음 상대에게서 기대할 수 있는 코너 득점.
    /// 상대가 없으면 평범한 상대를 가정한다.
    /// </summary>
    public float CornerXgFor(SetPieceDelivery delivery)
    {
        var starters = Starters()!;
        if (starters.Length == 0) return 0f;

        float foeAerial = 13.5f, foeGk = 15f, foeMove = 13f;

        if (World is { } w && NextOpponent is { } foe)
        {
            var tac = foe.savedTactics?.FirstOrDefault(t => t.id == foe.activeTacticsId);
            if (tac != null)
            {
                var fs = tac.slots.Select(s => w.Player(s.assignedPlayerId)).ToArray();
                var keeper = fs.FirstOrDefault(p => p != null && (p.positionFlags & Position.GK) != 0);
                foeAerial = SetPieces.AerialDefence(fs, keeper);
                foeGk = keeper?.goalkeeper is { } g ? (g.commandArea + g.aerialAbility) / 2f : 15f;
                foeMove = SetPieces.MovementThreat(fs);
            }
        }

        return SetPieces.Xg(
            SetPieces.Kind.Corner,
            SetPieces.TakerQuality(CornerTaker, SetPieces.Kind.Corner),
            SetPieces.AerialThreat(starters), foeAerial,
            delivery, foeGk,
            foeMove <= 0f ? 1f : SetPieces.MovementThreat(starters) / foeMove);
    }

    public async Task SetCornerDeliveryAsync(SetPieceDelivery delivery)
    {
        if (ActiveTactics is not { } tac) return;

        tac.setPieces ??= new SetPieceConfig();
        int taker = tac.setPieces.cornerKickLeft is { Count: > 0 } l ? l[0].takerPlayerId : -1;

        tac.setPieces.cornerKickLeft = new List<SetPieceRoutine>
        {
            new() { type = SetPieceType.CornerKick, delivery = delivery, takerPlayerId = taker },
        };
        tac.setPieces.cornerKickRight = new List<SetPieceRoutine>(tac.setPieces.cornerKickLeft);

        await TouchAsync();
    }

    public async Task SetCornerTakerAsync(int playerId)
    {
        if (ActiveTactics is not { } tac) return;

        tac.setPieces ??= new SetPieceConfig();
        var delivery = tac.setPieces.cornerKickLeft is { Count: > 0 } l
            ? l[0].delivery : SetPieceDelivery.Inswinger;

        tac.setPieces.cornerKickLeft = new List<SetPieceRoutine>
        {
            new() { type = SetPieceType.CornerKick, delivery = delivery, takerPlayerId = playerId },
        };
        tac.setPieces.cornerKickRight = new List<SetPieceRoutine>(tac.setPieces.cornerKickLeft);

        await TouchAsync();
    }

    /// <summary>페널티 키커는 선수 개인 지시에 표시한다.</summary>
    public async Task SetPenaltyTakerAsync(int playerId)
    {
        if (ActiveTactics is not { } tac) return;

        foreach (var slot in tac.slots)
        {
            if (slot.assignedPlayerId == playerId)
            {
                slot.customInstructions ??= new PlayerInstructions();
                slot.customInstructions.takePenalties = true;
            }
            else if (slot.customInstructions != null)
            {
                slot.customInstructions.takePenalties = false;
            }
        }

        await TouchAsync();
    }

    // ── 전술 궁합 ─────────────────────────────────────────────────────────

    /// <summary>다음 경기 상대. 없으면 null.</summary>
    public ClubData? NextOpponent
    {
        get
        {
            if (World is not { } w || NextFixture is not { } f) return null;
            int id = f.homeClubId == w.ManagedClubId ? f.awayClubId : f.homeClubId;
            return w.Club(id);
        }
    }

    public TeamInstructions OpponentInstructions(int clubId)
    {
        var club = World?.Club(clubId);
        var tac = club?.savedTactics?.FirstOrDefault(t => t.id == club.activeTacticsId);
        return tac?.instructions ?? new TeamInstructions();
    }

    /// <summary>
    /// 내 지시가 상대 지시에 어떻게 맞물리는지 말로 풀어 준다.
    /// 숫자만 보여 주면 왜 유리한지 알 수 없고, 전술 화면이 슬라이더 모음이 된다.
    /// </summary>
    public List<(bool good, string text)> MatchupNotes(TeamInstructions mine, int foeId)
    {
        var notes = new List<(bool, string)>();
        if (World is not { } w) return notes;

        var foe = OpponentInstructions(foeId);
        bool foeHigh = foe.defensiveLine is DefensiveLine.High or DefensiveLine.VeryHigh;
        bool foeDeep = foe.defensiveLine is DefensiveLine.Low or DefensiveLine.VeryLow;
        bool foePresses = foe.pressingIntensity >= 4 || foe.counterPress;

        if (foePresses)
            notes.Add(mine.buildUpStyle == BuildUpStyle.ShortPassing
                ? (false, "상대가 강하게 압박합니다. 짧은 패스는 끊기기 쉽습니다 — 길게 차는 쪽이 넘기기 좋습니다.")
                : mine.buildUpStyle == BuildUpStyle.LongBall
                    ? (true, "상대의 압박을 롱볼로 넘길 수 있습니다.")
                    : (false, "상대가 강하게 압박합니다. 빌드업이 흔들릴 수 있습니다."));

        if (foeHigh)
            notes.Add(mine.directCounterAttack
                ? (true, "상대가 라인을 올렸습니다. 역습 집중이 뒷공간을 정확히 노립니다.")
                : (true, "상대 뒷공간이 비어 있습니다. 역습 집중을 켜 볼 만합니다."));
        else if (mine.directCounterAttack)
            notes.Add((false, "상대가 내려서 있어 역습으로 노릴 뒷공간이 없습니다."));

        if (foeDeep)
            notes.Add(mine.width >= 4
                ? (true, "내려선 상대를 넓게 벌려 흔들 수 있습니다.")
                : (false, "상대가 내려섰습니다. 폭을 넓혀야 틈이 생깁니다."));
        else if (foeHigh)
            notes.Add(mine.width <= 2
                ? (true, "올라온 상대의 사이 공간을 좁은 폭으로 찌를 수 있습니다.")
                : (false, "상대가 라인을 올렸습니다. 폭을 좁혀 사이를 노리는 쪽이 낫습니다."));

        if (foe.offsideTrap)
        {
            float skill = TrapSkillOf(foeId);
            notes.Add(skill >= BalanceConfig.TrapBreakEven
                ? (false, $"상대 수비진이 오프사이드 트랩을 잘 맞춥니다(숙련 {skill:F0}). 뒷공간 침투가 막힙니다.")
                : (true, $"상대가 트랩을 걸지만 수비진이 서툽니다(숙련 {skill:F0}). 뒷공간이 열립니다."));
        }

        if (mine.pressingIntensity >= 4 && mine.tempo >= 4)
            notes.Add((false, "압박과 템포를 모두 높였습니다. 후반에 체력이 먼저 떨어집니다."));

        if (notes.Count == 0)
            notes.Add((true, "특별히 맞물리는 부분이 없습니다. 전력으로 겨루는 경기입니다."));

        return notes;
    }

    float TrapSkillOf(int clubId)
    {
        var club = World?.Club(clubId);
        var tac = club?.savedTactics?.FirstOrDefault(t => t.id == club.activeTacticsId);
        if (World is null || tac is null) return 10f;

        var starters = tac.slots.Select(s => World.Player(s.assignedPlayerId)).ToArray();
        return TacticalProfile.TrapSkill(starters);
    }

    // ── 홈그로운 ──────────────────────────────────────────────────────────

    public string HomegrownLabel
        => World is { } w ? Homegrown.Shortfall(w, w.ManagedClubId) : "—";

    public bool MeetsHomegrown
        => World is { } w && Homegrown.Meets(w, w.ManagedClubId);

    public bool IsAcademyProduct(PlayerData p) => Homegrown.IsHomegrown(p);

    // ── 경기장·스폰서 ─────────────────────────────────────────────────────

    public string RevenueMultiplierLabel
        => World?.MyClub is { } c ? $"×{ClubBusiness.RevenueMultiplier(c):F2}" : "—";

    public string FormLabel
    {
        get
        {
            if (World?.MyClub is not { } c) return "—";

            float form = ClubBusiness.RecentForm(c);
            return form switch
            {
                >= 0.5f => "상위권입니다",
                >= 0f => "중상위권입니다",
                >= -0.5f => "중하위권입니다",
                _ => "하위권입니다",
            };
        }
    }

    // ── 기록실 ────────────────────────────────────────────────────────────

    public List<SeasonRecord> RecentSeasons
        => World?.MyClub?.history?.recentSeasons is { } list
            ? list.OrderByDescending(s => s.season).ToList()
            : new();

    public List<SeasonAwardSet> MyLeagueAwards
        => World is { } w && w.MyLeague is { } lg
            ? w.Awards.Where(a => a.leagueId == lg.id).OrderByDescending(a => a.year).ToList()
            : new();

    /// <summary>내가 부임한 뒤 들어 올린 트로피. 구단 통산과 따로 센다.</summary>
    public int TitlesUnderMe
    {
        get
        {
            if (World is not { } w || w.MyClub is not { } c) return 0;

            // 부임 전 기록과 견주려면 부임 시점 값을 들고 있어야 한다.
            var h = c.history;
            if (h == null) return 0;

            return Math.Max(0,
                h.leagueTitles + h.cupTitles + h.continentalTitles - w.TitlesAtTakeover);
        }
    }

    /// <summary>통산 출전이 많은 순. 현역만 — 은퇴하면 선수가 세계에서 사라진다.</summary>
    public List<PlayerData> CareerLeaders
        => World is { } w
            ? w.Squad(w.ManagedClubId)
                .OrderByDescending(p => p.careerStats?.appearances ?? 0)
                .ThenByDescending(p => p.careerStats?.goals ?? 0)
                .Take(15)
                .ToList()
            : new();

    // ── 이사회 협상 ───────────────────────────────────────────────────────

    public static readonly BoardTalks.Ask[] BoardAsks =
        (BoardTalks.Ask[])Enum.GetValues(typeof(BoardTalks.Ask));

    public bool BoardAskReady => World is { } w && !w.Sacked && !BoardTalks.OnCooldown(w);

    public int DaysUntilBoardAsk => World is { } w ? BoardTalks.DaysUntilCanAsk(w) : 0;

    public float BoardOdds(BoardTalks.Ask a) => World is { } w ? BoardTalks.Odds(w, a) : 0f;

    public int BoardCost(BoardTalks.Ask a) => BalanceConfig.BoardCostRefused[(int)a];

    /// <summary>순위가 전력 기준보다 앞서는지. 이사회가 말을 듣는 이유다.</summary>
    public string ExpectationGapLabel
    {
        get
        {
            if (World is not { } w || w.MyClub is not { } c) return "—";

            float gap = BoardTalks.ExpectationGap(w, c);
            return gap switch
            {
                >= 1f => $"{gap:F0}계단 앞서 있습니다",
                <= -1f => $"{-gap:F0}계단 뒤처져 있습니다",
                _ => "기대한 자리에 있습니다",
            };
        }
    }

    public async Task<BoardTalks.Answer?> AskBoardAsync(BoardTalks.Ask ask)
    {
        if (World is not { } w) return null;

        var answer = BoardTalks.Request(w, ask, new Random(w.Seed ^ (w.Date.year * 7919 + w.Date.month * 31 + w.Date.day)));

        NotifyChanged();
        await SaveAsync();
        return answer;
    }

    // ── 스태프 ────────────────────────────────────────────────────────────

    public List<StaffData> MyStaff
        => World is { } w
            ? StaffRoom.Of(w, w.ManagedClubId)
                .OrderBy(s => s.role)
                .ThenByDescending(s => s.reputation)
                .ToList()
            : new();

    public int StaffWageBill => World is { } w ? StaffRoom.WageBill(w, w.ManagedClubId) : 0;

    public int WageHeadroom
        => World?.MyClub is { } c ? Math.Max(0, c.finances.weeklyWageBudget - c.finances.currentWageSpend) : 0;

    public static readonly StaffRole[] StaffRoles = (StaffRole[])Enum.GetValues(typeof(StaffRole));

    /// <summary>네 갈래의 힘과 그것이 실제로 무엇을 바꾸는지.</summary>
    public List<(string label, float power, string effect)> StaffStrengths
    {
        get
        {
            var list = new List<(string, float, string)>();
            if (World is not { } w) return list;

            int id = w.ManagedClubId;

            float coach = StaffRoom.Coaching(w, id);
            list.Add(("코칭", coach, $"성장 ×{StaffRoom.CoachingFactor(w, id):F2}"));

            float scout = StaffRoom.ScoutingPower(w, id);
            list.Add(("스카우팅", scout,
                $"보고서 ×{StaffRoom.ReportValue(w, id):F2} · 주당 +{StaffRoom.ExtraScoutTrips(w, id)}회"));

            float med = StaffRoom.Medical(w, id);
            list.Add(("의료", med,
                $"부상 ×{StaffRoom.InjuryFactor(w, id):F2} · 회복 ×{StaffRoom.RecoveryFactor(w, id):F2}"));

            float ana = StaffRoom.Analytics(w, id);
            list.Add(("분석", ana, StaffRoom.ReadsOpponentStyle(w, id) ? "상대 색깔을 읽는다" : "상대를 읽지 못한다"));

            return list;
        }
    }

    public bool ReadsOpponentStyle
        => World is { } w && StaffRoom.ReadsOpponentStyle(w, w.ManagedClubId);

    public List<StaffData> StaffMarket(string roleFilter)
    {
        if (World is not { } w) return new();

        StaffRole? role = Enum.TryParse<StaffRole>(roleFilter, out var r) ? r : null;
        return StaffRoom.FreeAgents(w, role).Take(40).ToList();
    }

    public StaffRoom.HireVerdict CanHire(StaffData s)
        => World is { } w && w.MyClub is { } c
            ? StaffRoom.CanHire(w, c, s)
            : StaffRoom.HireVerdict.NotAvailable;

    public string HireReason(StaffRoom.HireVerdict v) => v switch
    {
        StaffRoom.HireVerdict.NoBudget => "주급 여유 없음",
        StaffRoom.HireVerdict.RoleFull => "자리 참",
        _ => "—",
    };

    public async Task HireStaffAsync(int staffId)
    {
        if (World is not { } w || w.MyClub is not { } c) return;
        if (w.StaffMember(staffId) is not { } s) return;
        if (!StaffRoom.Hire(w, c, s)) return;

        NotifyChanged();
        await SaveAsync();
    }

    public async Task ReleaseStaffAsync(int staffId)
    {
        if (World is not { } w || w.MyClub is not { } c) return;
        if (w.StaffMember(staffId) is not { } s) return;
        if (!StaffRoom.Release(w, c, s)) return;

        NotifyChanged();
        await SaveAsync();
    }

    // ── 스카우트 ──────────────────────────────────────────────────────────

    public int ScoutingLeft => World?.ScoutingLeft ?? 0;

    public int ScoutReportsOn(int playerId)
        => World?.ScoutReports.GetValueOrDefault(playerId) ?? 0;

    public bool KnowsFully(PlayerData p)
        => World is { } w && Scouting.Knowledge(w, p) >= 1f;

    /// <summary>능력치 표시. 모르는 선수는 구간으로 보인다.</summary>
    public string CaLabel(PlayerData p)
        => World is { } w ? Scouting.Label(w, p, GameWorld.Ca(p)) : "—";

    public string PaLabel(PlayerData p)
        => World is { } w ? Scouting.Label(w, p, PlayerDevelopment.Pa(p)) : "—";

    public string ConfidenceOf(PlayerData p)
        => World is { } w ? Scouting.Confidence(w, p) : "";

    public bool CanScout(PlayerData p)
        => World is { } w && w.ScoutingLeft > 0 && Scouting.Knowledge(w, p) < 1f;

    public async Task ScoutAsync(int playerId)
    {
        if (World is not { } w || w.Player(playerId) is not { } p) return;
        if (!Scouting.SendScout(w, p)) return;

        NotifyChanged();
        await SaveAsync();
    }

    // ── 2군 ───────────────────────────────────────────────────────────────

    public List<PlayerData> ReserveSquad
        => World is { } w && w.MyClub is { } c ? ReserveLeague.Squad(w, c) : new();

    public int NextReserveMatchday
    {
        get
        {
            if (World?.MyLeague?.currentSeason is not { } s) return 0;
            int now = s.currentMatchday;
            return ReserveLeague.Matchdays.FirstOrDefault(m => m > now);
        }
    }

    // ── 개인 목표 · 이적 요청 · 국가대표 ──────────────────────────────────

    public List<PlayerAmbition.Goal> GoalsOf(PlayerData p) =>
        World is { } w ? PlayerAmbition.For(w, p) : new();

    public float SeasonProgress => World is { } w ? PlayerAmbition.SeasonProgress(w) : 0f;

    /// <summary>답을 기다리는 이적 요청. 압력이 큰 순서로.</summary>
    public List<(PlayerData player, TransferRequest request)> PendingRequests
    {
        get
        {
            if (World is not { } w) return new();
            return w.TransferRequests.Values
                .Select(r => (player: w.Player(r.playerId), request: r))
                .Where(x => x.player != null)
                .OrderByDescending(x => TransferRequests.Pressure(w, x.player!))
                .Select(x => (x.player!, x.request))
                .ToList();
        }
    }

    public bool HasRequests => World is { } w && w.TransferRequests.Count > 0;

    public int RejectionsOf(int playerId)
        => World is { } w && w.RejectedRequests.TryGetValue(playerId, out var r) ? r.rejections : 0;

    public async Task AcceptRequestAsync(int playerId)
    {
        if (World is not { } w || w.Player(playerId) is not { } p) return;
        TransferRequests.Accept(w, p);
        NotifyChanged();
        await SaveAsync();
    }

    public async Task RejectRequestAsync(int playerId)
    {
        if (World is not { } w || w.Player(playerId) is not { } p) return;
        TransferRequests.Reject(w, p);
        NotifyChanged();
        await SaveAsync();
    }

    /// <summary>이번 A매치 주간에 우리 구단에서 뽑혀 나가는 선수.</summary>
    public List<PlayerData> CalledUp
    {
        get
        {
            if (World is not { } w || w.MyClub is not { } club) return new();
            return w.Squad(club.id)
                .Where(p => NationalTeam.Squad(w, p.nationality).Any(x => x.id == p.id))
                .OrderByDescending(GameWorld.Ca)
                .ToList();
        }
    }

    public bool IsNationalCandidate(PlayerData p)
        => World is { } w && NationalTeam.IsCandidate(w, p);

    public float CallUpThreshold(PlayerData p)
        => World is { } w ? NationalTeam.Threshold(w, p.nationality) : 0f;

    // ── 탈의실 ────────────────────────────────────────────────────────────

    public DressingRoomMorale? Room => World?.MyClub?.dressingRoom;

    public List<DressingRoom.Grievance> Grievances =>
        World?.MyClub is { } c ? DressingRoom.Grievances(World, c) : new();

    public List<PlayerRelationship> RelationsOf(PlayerData p) =>
        World == null ? new() : DressingRoom.RelationsOf(World, p);

    public float InfluenceOf(PlayerData p) =>
        World == null ? 0f : DressingRoom.Influence(World, p);

    public async Task SetCaptainAsync(int playerId)
    {
        if (World?.MyClub is not { } club) return;
        DressingRoom.SetCaptain(World, club, playerId);
        NotifyChanged();
        await SaveAsync();
    }

    // ── 기자회견 ──────────────────────────────────────────────────────────

    public PressConferenceSession? Press => World?.PendingPress;

    /// <summary>답변을 기다리는 기자회견이 있는지. 메뉴에 표시를 띄운다.</summary>
    public bool PressWaiting => World is { Sacked: false } && PressRoom.IsDue(World);

    /// <summary>이번 시즌 기자회견으로 올릴 수 있는 이사회 신뢰도의 남은 몫.</summary>
    public int PressBoardLeft =>
        World == null ? 0 : Math.Max(0, PressRoom.BoardBudgetPerSeason - World.PressBoardSpent);

    public PressConferenceSession? OpenPress()
    {
        if (World == null) return null;
        var session = PressRoom.Open(World);
        NotifyChanged();
        return session;
    }

    public async Task AnswerPressAsync(int optionIndex)
    {
        if (World == null) return;
        PressRoom.Answer(World, optionIndex);
        NotifyChanged();
        await SaveAsync();
    }

    public async Task SkipPressAsync()
    {
        if (World == null) return;
        PressRoom.Skip(World);
        NotifyChanged();
        await SaveAsync();
    }

    // ── 컵 ────────────────────────────────────────────────────────────────

    public CupState? MyCup => World == null ? null : CupCompetition.UserCup(World);

    public CupState? Continental => World == null ? null : CupCompetition.ContinentalCup(World);

    /// <summary>대륙 대회 출전권이 있는지.</summary>
    public bool InContinental =>
        World != null && Continental?.entrants.Contains(World.ManagedClubId) == true;

    public bool StillInCup =>
        World != null &&
        (CupCompetition.StillIn(MyCup, World.ManagedClubId)
         || (InContinental && CupCompetition.StillIn(Continental, World.ManagedClubId)));

    // ── 커리어 ────────────────────────────────────────────────────────────

    public int ManagerReputation => World?.ManagerReputation ?? 0;

    public List<JobOffer> OpenJobs(int take = 6)
        => World == null
            ? new List<JobOffer>()
            : ManagerCareer.OpenJobs(World, new Random(World.Seed + World.SeasonYear), take);

    public async Task TakeJobAsync(int clubId)
    {
        if (World is not { } w) return;
        ManagerCareer.TakeJob(w, clubId);
        LastResult = null;
        LastFixture = null;
        Live = null;
        LiveFixture = null;
        NotifyChanged();
        await SaveAsync();
    }

    // ── 시즌 ──────────────────────────────────────────────────────────────

    public bool SeasonFinished => World != null && World.SeasonFinished;

    public async Task StartNextSeasonAsync()
    {
        if (World is not { } w) return;
        SeasonRollover.StartNextSeason(w);
        LastResult = null;
        LastFixture = null;
        NotifyChanged();
        await SaveAsync();
    }

    /// <summary>경기가 없는 주를 넘긴다. 이적 창구와 훈련이 돌아간다.</summary>
    public async Task AdvanceWeekAsync()
    {
        if (World is not { } w) return;
        WeekCycle.Advance(w, w.Date.AddDays(7));
        NotifyChanged();
        await SaveAsync();
    }

    // ── 조회 헬퍼 ─────────────────────────────────────────────────────────

    public ClubData? Club(int id) => World?.Club(id);
    public PlayerData? Player(int id) => World?.Player(id);

    public string ClubName(int id) => Club(id)?.name ?? "—";
    public string ClubShort(int id) => Club(id)?.shortName ?? "—";

    public IEnumerable<PlayerData> Squad =>
        World == null ? [] : World.Squad(World.ManagedClubId);

    public TacticsData? ActiveTactics
    {
        get
        {
            var club = World?.MyClub;
            return club?.savedTactics.FirstOrDefault(t => t.id == club.activeTacticsId)
                ?? club?.savedTactics.FirstOrDefault();
        }
    }

    /// <summary>
    /// 전술을 건드린 뒤 저장한다. 저장하지 않으면 새로고침했을 때
    /// 마지막 경기 시점의 라인업으로 되돌아간다.
    /// </summary>
    public async Task TouchAsync()
    {
        NotifyChanged();
        await SaveAsync();
    }

    /// <summary>전술의 포메이션을 바꾸고 선발을 다시 뽑는다.</summary>
    public void SetFormation(string presetId)
    {
        var club = World?.MyClub;
        var tac = ActiveTactics;
        if (World == null || club == null || tac == null) return;

        var preset = FormationDatabase.Get(presetId);
        if (preset == null) return;

        var replaced = FormationDatabase.CreateTactics(presetId, tac.name);
        replaced.id = tac.id;
        replaced.instructions = tac.instructions;     // 팀 지시는 유지한다
        replaced.setPieces = tac.setPieces;

        int index = club.savedTactics.IndexOf(tac);
        if (index >= 0) club.savedTactics[index] = replaced;
        else club.savedTactics.Add(replaced);
        club.activeTacticsId = replaced.id;

        LineupBuilder.Assign(replaced, club.playerIds.Select(World.Player));
        NotifyChanged();
    }

    /// <summary>슬롯에 선수를 넣는다. 이미 다른 자리에 있으면 서로 맞바꾼다.</summary>
    public void AssignToSlot(int slotIndex, int playerId)
    {
        var tac = ActiveTactics;
        if (World == null || tac == null) return;

        var slot = tac.slots.FirstOrDefault(s => s.slotIndex == slotIndex);
        if (slot == null) return;

        var other = tac.slots.FirstOrDefault(s => s.assignedPlayerId == playerId && s != slot);
        if (other != null) other.assignedPlayerId = slot.assignedPlayerId;

        slot.assignedPlayerId = playerId;
        World.AutoLineup = false;     // 감독이 손을 댔으니 자동 선발을 끈다
        NotifyChanged();
    }

    public void ResetLineup()
    {
        var club = World?.MyClub;
        var tac = ActiveTactics;
        if (World == null || club == null || tac == null) return;

        LineupBuilder.Assign(tac, club.playerIds.Select(World.Player));
        World.AutoLineup = true;
        NotifyChanged();
    }
}
