using System.Linq;
using FootballTactician.Data;
using FootballTactician.World;
using Xunit;

namespace FT.Core.Tests
{
    /// <summary>
    /// 계약 협상. 한 번의 수락·거절이 아니라 오가는 흥정이라는 것을 지킨다.
    ///
    /// 여기서 보려는 것은 금액 계산이 아니라 <b>주고받기</b>다 — 한쪽을 넉넉히 주면
    /// 다른 쪽을 접어 주는가, 모자라면 몇 번 만에 자리를 뜨는가, 합의한 것이
    /// 계약서에 그대로 적히고 세이브를 왕복하는가.
    /// </summary>
    public class ContractNegotiationTests
    {
        static GameWorld Managed(int seed = 7788)
        {
            var w = WorldGenerator.Create(seed);
            var club = w.Clubs.Values.First(c => c.reputation == 3);
            ManagerCareer.TakeCharge(w, club.id, "테스트 감독");
            return w;
        }

        /// <summary>협상 상대를 고른다. 성격을 지정하면 그 성격으로 바꿔 세운다.</summary>
        static PlayerData Target(GameWorld w, Personality? personality = null)
        {
            var p = w.Squad(w.ManagedClubId).OrderByDescending(GameWorld.Ca).Skip(4).First();
            if (personality is { } k) p.personality = k;
            return p;
        }

        [Fact]
        public void 요구대로_주면_합의한다()
        {
            var w = Managed();
            var p = Target(w);
            var t = ContractNegotiation.Open(w, p, w.ManagedClubId, renewal: true);

            ContractNegotiation.Propose(w, t, ContractNegotiation.Suggest(t));

            Assert.True(t.agreed);
            Assert.False(t.collapsed);
        }

        [Fact]
        public void 요구보다_모자라면_맞받아_온다()
        {
            var w = Managed();
            var p = Target(w, Personality.QuietPro);
            var t = ContractNegotiation.Open(w, p, w.ManagedClubId, renewal: true);

            int wanted = t.demand.weeklyWage;
            var low = ContractNegotiation.Suggest(t);
            low.weeklyWage = (int)(wanted * 0.9f);      // 모욕은 아니지만 모자라다

            ContractNegotiation.Propose(w, t, low);

            Assert.False(t.agreed);
            Assert.False(t.collapsed);
            Assert.True(t.demand.weeklyWage < wanted);   // 가운데로 다가왔다
            Assert.True(t.demand.weeklyWage > low.weeklyWage);
        }

        [Fact]
        public void 터무니없는_제안은_두_배로_깎아먹는다()
        {
            var w = Managed();
            var p = Target(w, Personality.QuietPro);     // 인내심 4
            var t = ContractNegotiation.Open(w, p, w.ManagedClubId, renewal: true);

            var insult = ContractNegotiation.Suggest(t);
            insult.weeklyWage = (int)(t.demand.weeklyWage * 0.5f);

            ContractNegotiation.Propose(w, t, insult);
            Assert.Equal(2, t.patience);                 // 한 번이 두 번분이다
            Assert.False(t.collapsed);

            ContractNegotiation.Propose(w, t, insult);
            Assert.True(t.collapsed);
        }

        [Fact]
        public void 주급이_넉넉하면_방출_조항_없이도_수락한다()
        {
            // 조항은 거절 사유 하나일 뿐 넘을 수 없는 벽이 아니다. 돈으로 메울 수 있다.
            var w = Managed();
            var p = Target(w, Personality.Ambitious);    // 야망 있는 선수는 조항을 원한다
            var t = ContractNegotiation.Open(w, p, w.ManagedClubId, renewal: true);
            Assert.True(t.demand.releaseClause);

            var rich = ContractNegotiation.Suggest(t);
            rich.weeklyWage = (int)(t.demand.weeklyWage * 1.15f);
            rich.releaseClause = false;                  // 조항은 못 주겠다

            ContractNegotiation.Propose(w, t, rich);

            Assert.True(t.agreed);
        }

        [Fact]
        public void 거절할_자리에서_주급이_넉넉하면_조항을_접어_준다()
        {
            // 흥정의 핵심 — 한쪽을 넉넉히 주면 다른 쪽 요구를 거둬들인다.
            // 자리를 두 칸 낮춰 그대로는 받아들일 수 없게 만들어 둔다.
            var w = Managed();
            var p = Target(w, Personality.Ambitious);
            var t = ContractNegotiation.Open(w, p, w.ManagedClubId, renewal: true);
            Assert.True(t.demand.releaseClause);

            var offer = ContractNegotiation.Suggest(t);
            offer.weeklyWage = (int)(t.demand.weeklyWage * 1.12f);
            offer.releaseClause = false;
            offer.role = (ContractNegotiation.Role)
                System.Math.Max(0, (int)t.demand.role - 2);

            ContractNegotiation.Propose(w, t, offer);

            Assert.False(t.agreed);                      // 자리가 모자라 이번엔 안 됐지만
            Assert.False(t.demand.releaseClause);        // 조항 요구는 거둬들였다
            Assert.True(t.Open);
            Assert.Contains("방출 조항", t.headline);
        }

        [Fact]
        public void 합의한_조건이_계약서에_그대로_적힌다()
        {
            var w = Managed();
            var p = Target(w);
            var t = ContractNegotiation.Open(w, p, w.ManagedClubId, renewal: true);

            var terms = ContractNegotiation.Suggest(t);
            terms.goalBonus = 4_000;
            terms.titleBonus = 120_000;
            terms.releaseClause = true;
            terms.releaseClauseAmount = 24_000_000;
            terms.promoRaise = 20;
            terms.role = ContractNegotiation.Role.Star;

            ContractNegotiation.Sign(w, p, terms, w.ManagedClubId);

            Assert.Equal(terms.weeklyWage, p.contract.weeklyWage);
            Assert.Equal(4_000, p.contract.bonuses.goalBonus);
            Assert.Equal(120_000, p.contract.bonuses.titleBonus);
            Assert.True(p.contract.clauses.hasReleaseClause);
            Assert.Equal(24_000_000, p.contract.clauses.releaseClauseAmount);
            Assert.Equal(20, p.contract.clauses.promoWageIncrease);
            Assert.Equal(30, p.contract.clauses.minimumAppearancesPerSeason);   // 핵심 선수 = 30경기
        }

        [Fact]
        public void 보너스와_조항이_세이브를_왕복한다()
        {
            var w = Managed();
            var p = Target(w);
            var t = ContractNegotiation.Open(w, p, w.ManagedClubId, renewal: true);

            var terms = ContractNegotiation.Suggest(t);
            terms.goalBonus = 3_500;
            terms.appearanceBonus = 1_500;
            terms.titleBonus = 90_000;
            terms.releaseClause = true;
            terms.releaseClauseAmount = 18_000_000;
            terms.promoRaise = 15;
            ContractNegotiation.Sign(w, p, terms, w.ManagedClubId);

            var back = SaveGame.Deserialize(SaveGame.Serialize(w, "계약")).world!;
            var c = back.Player(p.id)!.contract;

            Assert.Equal(3_500, c.bonuses.goalBonus);
            Assert.Equal(1_500, c.bonuses.appearanceBonus);
            Assert.Equal(90_000, c.bonuses.titleBonus);
            Assert.True(c.clauses.hasReleaseClause);
            Assert.Equal(18_000_000, c.clauses.releaseClauseAmount);
            Assert.Equal(15, c.clauses.promoWageIncrease);
            Assert.Equal(terms.weeklyWage, c.weeklyWage);
        }

        [Fact]
        public void 협상하지_않은_계약은_빈_묶음을_만들지_않는다()
        {
            // 계약 배열의 자리를 열하나에서 열일곱으로 늘렸지만, 뒤쪽 0 은 적지 않는다.
            // 6,000명 중 대부분이 이 경우라 세이브는 한 바이트도 늘지 않고,
            // 읽을 때도 보너스·조항 객체를 만들지 않는다.
            var w = Managed();
            var p = Target(w);
            Assert.Null(p.contract.bonuses);

            var back = SaveGame.Deserialize(SaveGame.Serialize(w, "빈 계약")).world!;
            var c = back.Player(p.id)!.contract;

            Assert.Null(c.bonuses);
            Assert.Null(c.clauses);
            Assert.Equal(p.contract.weeklyWage, c.weeklyWage);
        }

        [Fact]
        public void 방출_조항이_호가의_상한이_된다()
        {
            var w = Managed();
            var p = Target(w);
            long before = TransferMarket.AskingPrice(w, p);

            var t = ContractNegotiation.Open(w, p, w.ManagedClubId, renewal: true);
            var terms = ContractNegotiation.Suggest(t);
            terms.releaseClause = true;
            terms.releaseClauseAmount = before / 2;
            ContractNegotiation.Sign(w, p, terms, w.ManagedClubId);

            Assert.Equal(before / 2, TransferMarket.AskingPrice(w, p));
        }

        [Fact]
        public void 조항이_호가보다_비싸면_호가가_그대로다()
        {
            var w = Managed();
            var p = Target(w);
            long before = TransferMarket.AskingPrice(w, p);

            var t = ContractNegotiation.Open(w, p, w.ManagedClubId, renewal: true);
            var terms = ContractNegotiation.Suggest(t);
            terms.releaseClause = true;
            terms.releaseClauseAmount = before * 3;
            ContractNegotiation.Sign(w, p, terms, w.ManagedClubId);

            Assert.Equal(before, TransferMarket.AskingPrice(w, p));
        }

        [Fact]
        public void 급여_제재선을_넘는_제안은_보내기_전에_막힌다()
        {
            var w = Managed();
            var p = Target(w);
            var t = ContractNegotiation.Open(w, p, w.ManagedClubId, renewal: true);

            var absurd = ContractNegotiation.Suggest(t);
            absurd.weeklyWage = (int)(w.MyClub.finances.annualRevenue / 52);   // 수입 전부를 한 명에게

            Assert.NotNull(ContractNegotiation.ClubBlocker(w, t, absurd));
            Assert.Null(ContractNegotiation.ClubBlocker(w, t, ContractNegotiation.Suggest(t)));
        }

        [Fact]
        public void 이미_제재선을_넘은_구단도_같은_주급이면_재계약할_수_있다()
        {
            // 넘었다는 이유로 같은 값의 재계약까지 막으면, 줄일 길이 막히고
            // 선수는 공짜로 떠난다. 제재선이 구단을 더 망가뜨리게 된다.
            var w = Managed();
            var p = Target(w);
            var club = w.MyClub!;

            // 수입을 낮춰 급여 비중을 제재선 위로 올려 둔다.
            club.finances.annualRevenue = (long)(club.finances.currentWageSpend * 52 / 0.75f);

            var t = ContractNegotiation.Open(w, p, w.ManagedClubId, renewal: true);

            var same = ContractNegotiation.Suggest(t);
            same.weeklyWage = p.contract.weeklyWage;
            Assert.Null(ContractNegotiation.ClubBlocker(w, t, same));

            var more = ContractNegotiation.Suggest(t);
            more.weeklyWage = p.contract.weeklyWage * 2;
            Assert.NotNull(ContractNegotiation.ClubBlocker(w, t, more));
        }

        [Fact]
        public void 성격이_무엇을_먼저_보는지_가른다()
        {
            var w = Managed();

            var mercenary = Target(w, Personality.Mercenary);
            var greedy = ContractNegotiation.Open(w, mercenary, w.ManagedClubId, renewal: true);

            var loyal = w.Squad(w.ManagedClubId).OrderByDescending(GameWorld.Ca).Skip(5).First();
            loyal.personality = Personality.Loyal;
            var modest = ContractNegotiation.Open(w, loyal, w.ManagedClubId, renewal: true);

            // 돈만 보는 선수는 선금을 크게 부르고, 충성하는 선수는 아예 부르지 않는다.
            Assert.True(greedy.demand.signingBonus > 0);
            Assert.Equal(0, modest.demand.signingBonus);
            Assert.False(modest.demand.releaseClause);
            Assert.True(modest.patience > greedy.patience);
        }

        [Fact]
        public void 자리_약속은_출전_약속으로_적힌다()
        {
            Assert.Equal(30, ContractNegotiation.PromisedAppearances(ContractNegotiation.Role.Star));
            Assert.Equal(24, ContractNegotiation.PromisedAppearances(ContractNegotiation.Role.Starter));
            Assert.Equal(14, ContractNegotiation.PromisedAppearances(ContractNegotiation.Role.Rotation));
            Assert.Equal(6, ContractNegotiation.PromisedAppearances(ContractNegotiation.Role.Prospect));
        }
    }
}
