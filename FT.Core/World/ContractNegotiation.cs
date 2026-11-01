using System;
using System.Collections.Generic;
using System.Linq;
using FootballTactician.Balance;
using FootballTactician.Data;

namespace FootballTactician.World
{
    /// <summary>
    /// 계약 협상. 한 번의 수락·거절이 아니라 **오가는 흥정**이다.
    ///
    /// 예전에는 주급과 기간을 정해 보내면 끝이었다. 선수는 숫자 두 개만 보고 답했고,
    /// 감독이 할 수 있는 일은 금액을 올리는 것뿐이었다. 이 파일은 그 사이를 벌린다 —
    /// 자리 약속, 사인 보너스, 득점·출전 보너스, 방출 조항, 승격 시 인상까지 **표 하나**에
    /// 올려 두고, 한쪽이 모자라면 다른 쪽으로 메울 수 있게 한다.
    ///
    /// 그래서 이런 말이 오간다 — "주급을 그만큼 주신다면 방출 조항은 넣지 않겠습니다."
    /// 수락·거절만 있을 때는 나올 수 없는 말이고, 이것이 협상이다.
    ///
    /// 계약의 깊이는 모델에 이미 있었다(ContractBonus · ContractClause). 아무도 읽고 쓰지
    /// 않아 저장조차 되지 않던 것을 여기서 처음 쓴다.
    ///
    /// AI 구단은 이 길을 타지 않는다(TransferMarket.RunAiWindow 그대로). 240구단이
    /// 왕복 흥정을 하면 한 창구에 수천 번을 돌아야 하고, 세계의 동작도 달라진다.
    /// </summary>
    public static class ContractNegotiation
    {
        /// <summary>약속하는 자리. 숫자가 클수록 높은 대우다.</summary>
        public enum Role { Prospect = 0, Rotation = 1, Starter = 2, Star = 3 }

        public static string RoleLabel(Role r) => r switch
        {
            Role.Star => "핵심 선수",
            Role.Starter => "주전",
            Role.Rotation => "로테이션",
            _ => "유망주",
        };

        /// <summary>한 시즌에 이만큼은 뛰게 해 주겠다는 뜻이다. 어기면 탈의실이 안다.</summary>
        public static int PromisedAppearances(Role r) => r switch
        {
            Role.Star => 30,
            Role.Starter => 24,
            Role.Rotation => 14,
            _ => 6,
        };

        /// <summary>협상 탁자에 올라가는 조건 한 벌.</summary>
        public sealed class Terms
        {
            public int  weeklyWage;
            public int  years;
            public int  signingBonus;
            public Role role;

            public int  goalBonus;          // 골 하나당
            public int  appearanceBonus;    // 출전 한 경기당
            public int  titleBonus;         // 리그 우승 시 한 번

            public bool releaseClause;
            public long releaseClauseAmount;
            public int  promoRaise;         // 승격 시 주급 인상(%)

            public Terms Copy() => (Terms)MemberwiseClone();
        }

        public enum Mood { Short, Enough, Generous }

        /// <summary>표의 한 줄. 화면이 그대로 그린다.</summary>
        public sealed class Line
        {
            public string Label = "";
            public string Wanted = "";
            public string Offered = "";
            public Mood Mood;
            public bool Matters;            // 상대가 신경 쓰는 항목인가
        }

        public sealed class Talks
        {
            public int  playerId;
            public int  clubId;
            public bool renewal;

            public Terms demand = new();    // 상대가 지금 바라는 것
            public Terms offer = new();     // 내가 마지막으로 낸 것

            public int  patience;           // 남은 왕복 횟수
            public bool agreed;
            public bool collapsed;

            public string headline = "";
            public List<string> log = new();
            public List<Line> lines = new();

            public bool Open => !agreed && !collapsed;
        }

        // ── 탁자 차리기 ────────────────────────────────────────────────

        /// <summary>협상을 연다. 상대가 먼저 부른다 — 그래야 깎을 것이 생긴다.</summary>
        public static Talks Open(GameWorld w, PlayerData p, int clubId, bool renewal)
        {
            var club = w.Club(clubId);
            var t = new Talks { playerId = p.id, clubId = clubId, renewal = renewal };

            int wage = renewal || club == null
                ? ContractTalks.DemandedWage(w, p)
                : TransferMarket.RequiredWage(w, p, club);

            t.demand = new Terms
            {
                weeklyWage = wage,
                years = ContractTalks.DemandedYears(w, p),
                signingBonus = SigningBonusWanted(p, wage),
                role = ExpectedRole(w, p, clubId),
                goalBonus = GoalBonusWanted(p, wage),
                appearanceBonus = AppearanceBonusWanted(p, wage),
                titleBonus = 0,
                releaseClause = WantsReleaseClause(w, p),
                releaseClauseAmount = (long)(TransferMarket.AskingPrice(w, p) * 1.6),
                promoRaise = WantsPromoRaise(w, clubId) ? 15 : 0,
            };

            t.offer = t.demand.Copy();
            t.patience = Patience(p);
            t.headline = Opening(p, t);
            t.log.Add(t.headline);
            t.lines = Preview(w, t, t.offer);
            return t;
        }

        /// <summary>스쿼드에서 차지할 자리. 기량 순위로 본다.</summary>
        static Role ExpectedRole(GameWorld w, PlayerData p, int clubId)
        {
            float ca = GameWorld.Ca(p);
            int better = w.Squad(clubId).Count(x => x.id != p.id && GameWorld.Ca(x) > ca);

            // 어린 선수는 같은 순위라도 한 칸 낮게 바란다. 당장보다 자라는 것이 먼저다.
            bool young = w.Age(p) <= 21;
            return better switch
            {
                <= 2 => young ? Role.Starter : Role.Star,
                <= 6 => young ? Role.Rotation : Role.Starter,
                <= 13 => Role.Rotation,
                _ => Role.Prospect,
            };
        }

        static int Patience(PlayerData p) => p.personality switch
        {
            Personality.Loyal => 4,
            Personality.QuietPro => 4,
            Personality.Troublemaker => 2,
            Personality.Mercenary => 2,
            _ => 3,
        };

        static int SigningBonusWanted(PlayerData p, int wage) => p.personality switch
        {
            Personality.Mercenary => wage * 12,
            Personality.Ambitious => wage * 6,
            Personality.Loyal => 0,
            _ => wage * 4,
        };

        static int GoalBonusWanted(PlayerData p, int wage)
        {
            bool attacker = (p.positionFlags
                & (Position.ST | Position.CF | Position.LW | Position.RW | Position.CAM)) != 0;
            if (!attacker) return 0;
            return p.personality == Personality.Mercenary ? wage / 2 : wage / 4;
        }

        static int AppearanceBonusWanted(PlayerData p, int wage)
            => p.personality == Personality.Mercenary ? wage / 4 : 0;

        /// <summary>야망 있는 선수와 크게 자랄 어린 선수는 나갈 문을 열어 두고 싶어 한다.</summary>
        static bool WantsReleaseClause(GameWorld w, PlayerData p)
        {
            if (p.personality == Personality.Ambitious) return true;
            if (p.personality == Personality.Loyal) return false;
            return w.Age(p) <= 23 && PlayerDevelopment.Pa(p) > GameWorld.Ca(p) + 25f;
        }

        /// <summary>1부가 아니면 "올라가면 그만큼 올려 달라"가 당연한 말이 된다.</summary>
        static bool WantsPromoRaise(GameWorld w, int clubId) => (int)w.TierOf(clubId) > 1;

        static string Opening(PlayerData p, Talks t)
        {
            string role = RoleLabel(t.demand.role);
            string money = $"주 {t.demand.weeklyWage / 1000d:F0}K€";
            return p.personality switch
            {
                Personality.Mercenary => $"{p.FullName} 측은 돈 이야기부터 꺼냅니다. {money} 에 사인 보너스를 원합니다.",
                Personality.Loyal => $"{p.FullName} 은 남고 싶어 합니다. {money} 면 충분하다고 합니다.",
                Personality.Ambitious => $"{p.FullName} 은 {role} 자리를 원합니다. 조건이 맞아도 방출 조항은 넣어 달라고 합니다.",
                _ => $"{p.FullName} 측이 {money} 와 {role} 자리를 요구합니다.",
            };
        }

        // ── 흥정 ──────────────────────────────────────────────────────

        /// <summary>요구를 그대로 받아들인 조건. 화면의 "요구대로 채우기" 가 쓴다.</summary>
        public static Terms Suggest(Talks t) => t.demand.Copy();

        /// <summary>제안 하나를 올린다. 받아들여지거나, 맞받아 오거나, 자리를 뜬다.</summary>
        public static void Propose(GameWorld w, Talks t, Terms offer)
        {
            if (!t.Open) return;
            var p = w.Player(t.playerId);
            if (p == null) return;

            t.offer = offer.Copy();
            float score = Weigh(w, t, t.offer);
            t.lines = Preview(w, t, t.offer);

            // 주급이 너무 모자라면 다른 것으로 메울 수 없다. 모욕으로 받아들인다.
            bool insult = offer.weeklyWage < t.demand.weeklyWage * 0.85f;

            if (!insult && score >= -0.02f)
            {
                t.agreed = true;
                t.headline = $"{p.FullName} 이 조건에 합의했습니다.";
                t.log.Add(t.headline);
                return;
            }

            t.patience -= insult ? 2 : 1;
            if (t.patience <= 0)
            {
                t.collapsed = true;
                t.headline = insult
                    ? $"{p.FullName} 측이 자리를 떴습니다. 금액 차이가 너무 컸습니다."
                    : $"{p.FullName} 측이 협상을 접었습니다. 더 들을 생각이 없어 보입니다.";
                t.log.Add(t.headline);
                return;
            }

            t.headline = Counter(p, t);
            t.log.Add(t.headline);
            t.lines = Preview(w, t, t.offer);   // 요구가 바뀌었으니 표도 다시 센다
        }

        /// <summary>
        /// 맞받는다. 요구를 제안 쪽으로 조금 당기되, **넉넉히 준 항목이 있으면 그 대가로
        /// 덜 중요한 요구를 접는다.** 이것이 흥정의 전부다.
        /// </summary>
        static string Counter(PlayerData p, Talks t)
        {
            var d = t.demand;
            var o = t.offer;

            // 1) 주급을 넉넉히 받았으면 조항을 접어 준다.
            if (o.weeklyWage > d.weeklyWage * 1.08f && d.releaseClause)
            {
                d.releaseClause = false;
                return $"{p.FullName} 측: 주급을 그만큼 주신다면 방출 조항은 넣지 않겠습니다.";
            }

            // 2) 선금을 두둑히 받았으면 기간을 양보한다.
            if (o.signingBonus > d.signingBonus * 1.3f && d.years > o.years)
            {
                d.years = o.years;
                return $"{p.FullName} 측: 선금이 그 정도면 기간은 {o.years}년으로 맞추겠습니다.";
            }

            // 3) 자리가 요구보다 높으면 돈을 조금 깎는다. 뛰는 것이 먼저인 선수들이 있다.
            if (o.role > d.role && o.weeklyWage < d.weeklyWage)
            {
                int before = d.weeklyWage;
                d.weeklyWage = (int)Math.Max(o.weeklyWage, d.weeklyWage * 0.93f);
                if (d.weeklyWage < before)
                    return $"{p.FullName} 측: {RoleLabel(o.role)} 로 쓰신다면 주 {d.weeklyWage / 1000d:F0}K€ 까지 내려 보겠습니다.";
            }

            // 4) 그 밖에는 가운데로 조금 다가온다. 끝까지 버티지는 않는다.
            if (o.weeklyWage < d.weeklyWage)
            {
                int gap = d.weeklyWage - o.weeklyWage;
                d.weeklyWage -= (int)(gap * 0.35f);
                return $"{p.FullName} 측: 주 {d.weeklyWage / 1000d:F0}K€ 면 사인하겠습니다. (남은 기회 {t.patience}번)";
            }

            if (o.role < d.role)
                return $"{p.FullName} 측: 돈보다 {RoleLabel(d.role)} 자리가 먼저입니다. (남은 기회 {t.patience}번)";

            return $"{p.FullName} 측이 아직 모자라다고 합니다. (남은 기회 {t.patience}번)";
        }

        // ── 저울질 ────────────────────────────────────────────────────

        /// <summary>
        /// 조건 한 벌이 얼마나 마음에 드는지. -1(터무니없음) ~ +1(넘친다).
        /// 항목마다 무게가 다르고, 무게는 성격이 정한다 — 돈만 보는 선수가 있고
        /// 자리를 먼저 보는 선수가 있다.
        /// </summary>
        public static float Weigh(GameWorld w, Talks t, Terms offer, List<Line> lines = null)
        {
            var p = w.Player(t.playerId);
            if (p == null) return -1f;

            var d = t.demand;
            lines?.Clear();
            float sum = 0f, weight = 0f;

            void Add(string label, float w8, float score, string wanted, string offered, bool matters = true)
            {
                if (w8 > 0) { sum += w8 * Math.Clamp(score, -1f, 1f); weight += w8; }
                lines?.Add(new Line
                {
                    Label = label,
                    Wanted = wanted,
                    Offered = offered,
                    Matters = matters && w8 > 0,
                    Mood = score < -0.02f ? Mood.Short : score > 0.25f ? Mood.Generous : Mood.Enough,
                });
            }

            float moneyWeight = p.personality == Personality.Mercenary ? 1.4f
                              : p.personality == Personality.Loyal ? 0.8f : 1f;
            float roleWeight = p.personality == Personality.Ambitious ? 0.8f
                             : p.personality == Personality.Mercenary ? 0.3f : 0.5f;

            // 주급 — 모자란 쪽이 훨씬 아프다. 25% 모자라면 바닥이다.
            float wageGap = (offer.weeklyWage - d.weeklyWage) / (float)Math.Max(1, d.weeklyWage);
            Add("주급", moneyWeight, wageGap * 4f, Money(d.weeklyWage), Money(offer.weeklyWage));

            // 기간 — 길든 짧든 바라는 데서 멀수록 싫다.
            Add("계약 기간", 0.35f, -Math.Abs(offer.years - d.years) / 3f,
                $"{d.years}년", $"{offer.years}년");

            Add("자리 약속", roleWeight, (offer.role - d.role) * 0.5f,
                RoleLabel(d.role), RoleLabel(offer.role));

            Add("사인 보너스", d.signingBonus > 0 ? 0.3f : 0.1f,
                d.signingBonus <= 0
                    ? Math.Min(1f, offer.signingBonus / 50_000f)
                    : (offer.signingBonus - d.signingBonus) / (float)d.signingBonus * 2f,
                d.signingBonus > 0 ? Money(d.signingBonus) : "—", Money(offer.signingBonus),
                d.signingBonus > 0);

            Add("골 보너스", d.goalBonus > 0 ? 0.18f : 0f,
                d.goalBonus <= 0 ? 0f : (offer.goalBonus - d.goalBonus) / (float)d.goalBonus * 2f,
                d.goalBonus > 0 ? Money(d.goalBonus) : "—", Money(offer.goalBonus), d.goalBonus > 0);

            Add("출전 보너스", d.appearanceBonus > 0 ? 0.15f : 0f,
                d.appearanceBonus <= 0 ? 0f
                    : (offer.appearanceBonus - d.appearanceBonus) / (float)d.appearanceBonus * 2f,
                d.appearanceBonus > 0 ? Money(d.appearanceBonus) : "—",
                Money(offer.appearanceBonus), d.appearanceBonus > 0);

            Add("우승 보너스", 0.1f, offer.titleBonus > 0 ? 0.4f : 0f,
                "—", Money(offer.titleBonus), false);

            Add("방출 조항", d.releaseClause ? 0.3f : 0f,
                !d.releaseClause ? 0f
                    : offer.releaseClause && offer.releaseClauseAmount <= d.releaseClauseAmount * 1.25f ? 1f : -1f,
                d.releaseClause ? Money(d.releaseClauseAmount) : "—",
                offer.releaseClause ? Money(offer.releaseClauseAmount) : "없음",
                d.releaseClause);

            Add("승격 시 인상", d.promoRaise > 0 ? 0.2f : 0f,
                d.promoRaise <= 0 ? 0f : (offer.promoRaise - d.promoRaise) / (float)d.promoRaise,
                d.promoRaise > 0 ? $"{d.promoRaise}%" : "—",
                offer.promoRaise > 0 ? $"{offer.promoRaise}%" : "없음", d.promoRaise > 0);

            return weight <= 0 ? 0f : sum / weight;
        }

        /// <summary>
        /// 지금 올려 둔 조건에 대한 표. <b>화면이 그릴 때마다 부른다</b> —
        /// 슬라이더를 움직이는 대로 "모자람 / 충분 / 넉넉함"이 따라 바뀌어야
        /// 어디를 깎고 어디를 얹을지 보고 고를 수 있다.
        /// </summary>
        public static List<Line> Preview(GameWorld w, Talks t, Terms offer)
        {
            var lines = new List<Line>();
            Weigh(w, t, offer, lines);
            return lines;
        }

        static string Money(long v) => v <= 0 ? "—"
            : v >= 1_000_000 ? $"{v / 1_000_000d:F1}M€" : $"{v / 1000d:F0}K€";

        // ── 구단 쪽 사정 ──────────────────────────────────────────────

        /// <summary>구단이 감당할 수 있는 제안인가. 못 내는 제안은 보내지도 못하게 한다.</summary>
        public static string ClubBlocker(GameWorld w, Talks t, Terms offer)
        {
            var club = w.Club(t.clubId);
            var p = w.Player(t.playerId);
            if (club == null || p == null) return "대상을 찾을 수 없습니다.";

            long revenue = Math.Max(1, club.finances.annualRevenue);
            long current = t.renewal ? (p.contract?.weeklyWage ?? 0) : 0;
            long spend = club.finances.currentWageSpend - current + offer.weeklyWage;

            float before = club.finances.currentWageSpend * 52f / revenue;
            float after = spend * 52f / revenue;

            // 이미 넘어 있는 구단이라면 **더 밀어 올리는 제안만** 막는다.
            // 넘었다는 이유로 같은 주급의 재계약까지 막으면, 줄일 길이 막히고
            // 선수는 공짜로 떠난다 — 제재선이 구단을 더 망가뜨리게 된다.
            if (after > BalanceConfig.WageSanctionRatio && after > before)
                return before > BalanceConfig.WageSanctionRatio
                    ? $"지금도 급여 비중이 {before:P1} 로 제재선을 넘고 있습니다. 이 제안은 {after:P1} 로 더 올립니다."
                    : $"급여 비중이 {after:P0} 가 되어 제재선({BalanceConfig.WageSanctionRatio:P0})을 넘습니다.";

            if (offer.signingBonus > club.finances.transferBudget)
                return "사인 보너스를 낼 이적 예산이 없습니다.";

            return null;
        }

        // ── 서명 ──────────────────────────────────────────────────────

        /// <summary>합의한 조건을 계약서에 옮긴다. 보너스와 조항까지 전부 적는다.</summary>
        public static void Sign(GameWorld w, PlayerData p, Terms terms, int clubId)
        {
            var club = w.Club(clubId);
            if (club == null) return;

            p.contract ??= new ContractData();
            var c = p.contract;

            c.clubId = clubId;
            c.weeklyWage = terms.weeklyWage;
            c.startDate = w.Date;
            c.expiryDate = new GameDate(w.Date.year + Math.Max(1, terms.years), 6, 30);
            c.status = ContractStatus.Active;
            c.signingBonus = terms.signingBonus;

            c.bonuses ??= new ContractBonus();
            c.bonuses.goalBonus = terms.goalBonus;
            c.bonuses.appearanceBonus = terms.appearanceBonus;
            c.bonuses.titleBonus = terms.titleBonus;

            c.clauses ??= new ContractClause();
            c.clauses.hasReleaseClause = terms.releaseClause;
            c.clauses.releaseClauseAmount = terms.releaseClause ? terms.releaseClauseAmount : 0;
            c.clauses.hasPromoRelegationClause = terms.promoRaise > 0;
            c.clauses.promoWageIncrease = terms.promoRaise;

            // 자리 약속은 "한 시즌에 이만큼은 뛴다"로 적힌다. 지키지 않으면 탈의실이 안다.
            c.clauses.hasAppearanceClause = true;
            c.clauses.minimumAppearancesPerSeason = PromisedAppearances(terms.role);

            p.transferStatus = TransferStatus.NotListed;

            // 사인 보너스는 그 자리에서 나간다.
            if (terms.signingBonus > 0)
            {
                club.finances.balance -= terms.signingBonus;
                club.finances.transferBudget =
                    (int)Math.Max(0, club.finances.transferBudget - terms.signingBonus);
            }

            TransferMarket.RecalcWages(w, club);
        }
    }
}
