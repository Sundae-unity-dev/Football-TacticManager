using System;
using System.Text.Json.Serialization;
using System.Collections.Generic;
using System.Linq;
using FootballTactician.Balance;
using FootballTactician.Data;

namespace FootballTactician.World
{
    /// <summary>
    /// 한 판의 세계 전체. 화면은 이 객체만 보면 되고, 진행은 GameEngine 이 맡는다.
    /// Unity 시절 GameManager 가 들고 있던 상태를 여기로 옮겼다.
    /// </summary>
    public sealed class GameWorld
    {
        public int Seed;
        public GameDate Date;
        public int SeasonYear;

        public List<NationState> Nations = new();
        public Dictionary<int, ClubData> Clubs = new();
        public Dictionary<int, PlayerData> Players = new();
        public Dictionary<int, LeagueData> Leagues = new();

        /// <summary>플레이어가 맡은 구단. 선택 전에는 -1.</summary>
        public int ManagedClubId = -1;
        public ManagerProfile Manager = new();
        public string ManagerName = "";

        public List<BoardObjective> Objectives = new();
        public List<InboxItem> Inbox = new();

        /// <summary>완료된 이적 기록. 최신이 앞이다.</summary>
        public List<TransferRecord> Transfers = new();

        /// <summary>나라별 국내 컵. 시즌마다 새로 짠다.</summary>
        public Dictionary<int, CupState> Cups = new();
        public int NextOfferId = 1;

        /// <summary>성사된 이적 총 건수. Transfers 목록은 최근 것만 남기므로 집계는 따로 센다.</summary>
        public int TotalTransfers;

        /// <summary>이번 창구에서 이적 목록을 이미 정리했는지. 창구가 닫히면 다시 false 가 된다.</summary>
        public bool TransferListsReady;

        /// <summary>계약 만료를 이미 알린 선수. 같은 소식을 반복해서 보내지 않는다.</summary>
        public HashSet<int> ContractWarnings = new();

        /// <summary>주간 훈련 지시.</summary>
        public TrainingPlan Training = new();

        /// <summary>진행 중인 임대. 선수 id 로 찾는다.</summary>
        public Dictionary<int, LoanSpell> Loans = new();

        /// <summary>다음 시즌 일정에 쓸 id. 시즌을 넘길 때마다 이어서 매긴다.</summary>
        public int NextFixtureId = 100000;

        /// <summary>
        /// 일정에 없는데 <b>내가 치러야 할</b> 경기 한 판. 없으면 null.
        ///
        /// 컵처럼 리그 일정 밖에서 벌어지는 경기를 감독이 직접 뛸 수 있게 하는 자리다.
        /// 이것이 있는 동안에는 세계가 더 나아가지 않는다 — 그 경기가 끝나야 대진이
        /// 이어지고 한 주가 넘어간다.
        /// </summary>
        public PendingMatch pendingMatch;

        /// <summary>새로 만들 구단과 선수의 id. 유스 승격과 자유계약 생성에 쓴다.</summary>
        public int NextClubId = 1;
        public int NextPlayerId = 1;

        /// <summary>
        /// 능력치는 정수라 주간 성장분이 한 번에 반영되지 않는다. 남는 소수를 선수별로 이월한다.
        /// </summary>
        public Dictionary<int, float> GrowthCarry = new();

        /// <summary>켜 두면 매 경기 적합도대로 선발을 다시 뽑는다. 감독이 직접 짜면 끈다.</summary>
        public bool AutoLineup = true;

        /// <summary>이사회 신뢰도가 해고선 아래로 내려가면 세워진다. 화면은 이 값으로 종료를 띄운다.</summary>
        public bool Sacked;
        public string SackReason = "";

        /// <summary>감독 평판 0~100. 맡을 수 있는 구단 수준을 정한다.</summary>
        public int ManagerReputation = BalanceConfig.ManagerStartingReputation;

        /// <summary>지금 감독을 구하는 구단.</summary>
        public HashSet<int> VacantClubs = new();

        /// <summary>거쳐 온 구단들.</summary>
        public List<CareerSpell> CareerHistory = new();
        public int CurrentSpellStartYear;

        // ── 기자회견 ──────────────────────────────────────────────────────
        /// <summary>답변을 기다리는 기자회견. 없으면 null.</summary>
        public PressConferenceSession PendingPress;

        /// <summary>마지막으로 기자회견을 치른 라운드. 간격을 재는 데 쓴다.</summary>
        public int LastPressRound;

        /// <summary>이번 시즌 기자회견으로 이미 올린 이사회 신뢰도.</summary>
        public int PressBoardSpent;

        // ── 이적 요청 ─────────────────────────────────────────────────────
        /// <summary>답을 기다리는 이적 요청.</summary>
        public Dictionary<int, TransferRequest> TransferRequests = new();

        /// <summary>거절한 요청. 거절 횟수를 기억해 다음 요청이 더 빨리 오게 한다.</summary>
        public Dictionary<int, TransferRequest> RejectedRequests = new();

        /// <summary>이사회에 마지막으로 무언가를 요청한 날. 연달아 조르지 못하게 막는다.</summary>
        public int LastBoardAskDay;

        /// <summary>지난 시즌들의 수상 기록.</summary>
        public List<SeasonAwardSet> Awards = new();

        /// <summary>부임 시점의 구단 통산 우승 수. 내 임기 성과를 따로 세려면 기준점이 필요하다.</summary>
        public int TitlesAtTakeover;

        // ── 스태프 ────────────────────────────────────────────────────────
        /// <summary>세계의 모든 스태프. 구단 소속과 자유 스태프가 함께 들어 있다.</summary>
        public Dictionary<int, StaffData> Staff = new();

        public StaffData StaffMember(int id) => Staff.GetValueOrDefault(id);

        // ── 스카우트 ──────────────────────────────────────────────────────
        /// <summary>선수별 스카우트 보고서 수. 본 선수만 들어가므로 세이브에 거의 부담이 없다.</summary>
        public Dictionary<int, int> ScoutReports = new();

        /// <summary>이번 주에 남은 스카우트 횟수.</summary>
        public int ScoutingLeft;

        // ── 조회 ──────────────────────────────────────────────────────────
        public ClubData Club(int id) => Clubs.TryGetValue(id, out var c) ? c : null;
        public PlayerData Player(int id) => Players.TryGetValue(id, out var p) ? p : null;
        public LeagueData League(int id) => Leagues.TryGetValue(id, out var l) ? l : null;

        [JsonIgnore] public ClubData MyClub => Club(ManagedClubId);
        [JsonIgnore] public LeagueData MyLeague => MyClub == null ? null : LeagueOf(ManagedClubId);

        /// <summary>
        /// 내 리그의 일정이 다 끝났는지. 맡은 구단이 없으면 false.
        /// 진행 엔진에 있던 질문인데, 결산·하네스·테스트가 모두 묻는다 —
        /// 상태를 묻는 질문은 상태를 들고 있는 쪽에 둔다.
        /// </summary>
        [JsonIgnore] public bool SeasonFinished => MyLeague?.currentSeason is { } s && s.Finished;

        [JsonIgnore] public Func<int, PlayerData> Lookup => Player;

        public LeagueData LeagueOf(int clubId)
        {
            var c = Club(clubId);
            if (c == null) return null;
            return int.TryParse(c.leagueId, out int lid) ? League(lid) : null;
        }

        public NationState NationOf(int clubId)
        {
            var l = LeagueOf(clubId);
            if (l == null) return null;

            // LeagueIds 를 봐야 한다. LeagueId 는 1부뿐이라
            // 2·3부 구단은 나라가 없는 것으로 나온다 — 3부를 넣을 때 놓친 자리다.
            return Nations.FirstOrDefault(n => n.LeagueIds.Contains(l.id))
                ?? Nations.FirstOrDefault(n => n.LeagueId == l.id);
        }

        public IEnumerable<PlayerData> Squad(int clubId)
        {
            var c = Club(clubId);
            if (c == null) yield break;
            foreach (int id in c.playerIds)
                if (Players.TryGetValue(id, out var p)) yield return p;
        }

        /// <summary>능력치 평균(1~20)을 CA(1~200)로 환산. 화면 표기는 전부 이 값을 쓴다.</summary>
        public static float Ca(PlayerData p) => BalanceConfig.PowerToCa(p.AbilityScore);

        /// <summary>
        /// 그 자리를 소화하는 선수 중 가장 높은 CA. 없으면 0.
        /// 이적과 임대가 모두 묻는다 — 한 명의 값을 알려고 명단을 정렬하지 않는다.
        /// </summary>
        public float BestCaAt(int clubId, Position pos)
        {
            float best = 0f;
            foreach (var p in Squad(clubId))
                if ((p.positionFlags & pos) != 0)
                {
                    float ca = Ca(p);
                    if (ca > best) best = ca;
                }
            return best;
        }

        /// <summary>구단 전력 — 출전 가능한 상위 11인의 CA 평균.</summary>
        public float Strength(int clubId)
        {
            // 열한 명만 필요하다. 25명을 정렬해 열한 명을 떼던 자리인데,
            // 수준이 맞는 구단을 찾는 코드가 240구단마다 이걸 부른다.
            // 열한 칸에 끼워 넣으며 한 번 훑으면 같은 열한 명이 같은 순서로 남는다 —
            // CA 가 같으면 명단에서 앞선 쪽이 앞이어야 하므로 '더 클 때만' 앞으로 끼운다.
            Span<float> top = stackalloc float[11];
            int n = 0;

            foreach (var p in Squad(clubId))
            {
                if (p.IsInjured) continue;

                float ca = Ca(p);
                int at = n;
                while (at > 0 && ca > top[at - 1]) at--;
                if (at >= 11) continue;

                for (int k = Math.Min(n, 10); k > at; k--) top[k] = top[k - 1];
                top[at] = ca;
                if (n < 11) n++;
            }

            if (n == 0) return 0f;

            // 합은 double 로 쌓는다. LINQ 의 Average 가 그렇게 세므로,
            // float 로 쌓으면 마지막 자리에서 값이 갈린다.
            double sum = 0;
            for (int i = 0; i < n; i++) sum += top[i];
            return (float)(sum / n);
        }

        public int Age(PlayerData p) => p.GetAge(Date);

        /// <summary>평판 별점(1~5) → 밸런스 계수. BalanceConfig 가 쓰는 스케일이다.</summary>
        /// <summary>
        /// 리그 등급 보정. 2부 구단은 같은 별점이라도 1부보다 자원이 적다.
        /// 이 값이 없으면 2부 상위권과 1부 하위권의 전력이 같아져 승강이 의미를 잃는다.
        /// </summary>
        public static float TierFactor(LeagueTier tier) => tier switch
        {
            LeagueTier.First => 1.00f,
            LeagueTier.Second => 0.80f,
            LeagueTier.Third => 0.66f,
            _ => 0.56f,
        };

        public static float RepCoef(int stars) => stars switch
        {
            5 => 1.35f,
            4 => 1.15f,
            3 => 1.00f,
            2 => 0.90f,
            _ => 0.80f,
        };

        /// <summary>구단의 실효 평판 계수. 별점과 리그 등급을 함께 본다.</summary>
        public float RepCoefOf(int clubId)
        {
            var c = Club(clubId);
            if (c == null) return 1.0f;
            return RepCoef(c.reputation)
                 * TierFactor(LeagueOf(clubId)?.tier ?? LeagueTier.First)
                 * NationStrength(clubId);
        }

        /// <summary>
        /// 구단이 속한 나라의 수준. 세이브에는 나라 코드만 있고 수치는 목록에 있으므로
        /// 여기서 찾아 쓴다 — 구단마다 숫자를 저장하면 세이브만 늘고 값은 같다.
        /// </summary>
        public float NationStrength(int clubId)
        {
            var nation = NationOf(clubId);
            return nation == null ? 1.0f : NameForge.ByCode(nation.Code).Strength;
        }

        public LeagueTier TierOf(int clubId) => LeagueOf(clubId)?.tier ?? LeagueTier.First;

        /// <summary>소속이 없는 선수. 이적료 없이 영입할 수 있다.</summary>
        /// <summary>그 선수가 임대 중이면 계약 내용, 아니면 null.</summary>
        public LoanSpell LoanOf(int playerId)
            => Loans.TryGetValue(playerId, out var l) ? l : null;

        public LoanSpell OnLoan(int playerId) => LoanOf(playerId);

        /// <summary>내가 내보낸 임대 선수들.</summary>
        public IEnumerable<LoanSpell> LoansOut()
            => Loans.Values.Where(l => l.parentClubId == ManagedClubId);

        public IEnumerable<PlayerData> FreeAgents()
            => Players.Values.Where(p => p.clubId == 0);

        /// <summary>내 리그에서 치른 라운드 수.</summary>
        [JsonIgnore]
        public int RoundsPlayed =>
            MyLeague?.currentSeason?.fixtures.Count(f => f.isPlayed
                && (f.homeClubId == ManagedClubId || f.awayClubId == ManagedClubId)) ?? 0;

        /// <summary>이적 시장이 열려 있는지. 시즌 전과 겨울 창구.</summary>
        [JsonIgnore]
        public bool TransferWindowOpen => Date.month is 6 or 7 or 8 or 1;

        /// <param name="playerId">소식이 가리키는 선수. 화면이 그 선수로 데려간다. 0 이면 없음.</param>
        /// <param name="clubId">소식이 가리키는 구단. 0 이면 없음.</param>
        public void Post(string title, string body, InboxKind kind = InboxKind.News,
                         int playerId = 0, int clubId = 0)
        {
            Inbox.Insert(0, new InboxItem
            {
                date = Date, title = title, body = body, kind = kind,
                playerId = playerId, clubId = clubId,
            });
            if (Inbox.Count > 80) Inbox.RemoveRange(80, Inbox.Count - 80);
        }
    }

    public enum InboxKind { News, Board, Match, Transfer, Injury, Staff }

    [Serializable]
    public sealed class InboxItem
    {
        public GameDate date;
        public string title;
        public string body;
        public InboxKind kind;
        public bool read;

        // 소식이 가리키는 대상. 화면 주소가 아니라 id 만 둔다 — 핵심 로직은 화면을 모른다.
        // 0 은 저장하지 않으므로(WhenWritingDefault) 예전 세이브도, 대상 없는 소식도 그대로다.
        public int playerId;
        public int clubId;
    }

    /// <summary>국가 하나 — 지금은 1부 리그만 둔다. 하부 리그는 승강과 함께 뒤에 붙인다.</summary>
    [Serializable]
    public sealed class NationState
    {
        public string Code;
        public string Country;

        /// <summary>1부 리그. 기존 호출부 호환용이며 LeagueIds[0] 과 같다.</summary>
        public int LeagueId;

        /// <summary>등급 순 리그 목록. 0 이 1부.</summary>
        public List<int> LeagueIds = new();
    }
}
