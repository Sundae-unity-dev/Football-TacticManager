using System;
using System.Collections.Generic;
using System.Linq;
using FootballTactician.Balance;
using FootballTactician.Data;
using FootballTactician.World;
using Xunit;

namespace FT.Core.Tests
{
    /// <summary>
    /// 이사회와의 협상, 시즌 시상과 기록 보관.
    ///
    /// 이사회는 목표를 내리고 신뢰도를 매기기만 했고, 우승해도 구단 기록은 늘지 않았다.
    /// </summary>
    public class BoardAndHonoursTests
    {
    public class RetirementTests
    {
        static GameWorld Make(int seed = 31415, int stars = 4)
        {
            var w = WorldGenerator.Create(seed);
            var club = w.Clubs.Values.First(c => c.reputation == stars);
            ManagerCareer.TakeCharge(w, club.id, "테스트");
            return w;
        }

        static void Seasons(GameWorld w, int n)
        {
            for (int s = 0; s < n; s++)
            {
                int guard = 0;
                while (!w.SeasonFinished && guard++ < 60)
                    if (GameEngine.PlayNextRound(w) == null) break;
                SeasonRollover.StartNextSeason(w);
            }
        }

        // ── 이사회 협상 ───────────────────────────────────────────────────

        [Fact]
        public void 연달아_조를_수_없다()
        {
            // 공짜로 조를 수 있으면 매주 예산을 올려 달라고 하는 것 말고 할 일이 없다.
            var w = Make();
            Assert.False(BoardTalks.OnCooldown(w));

            // 곳간에 기대지 않는 요청을 쓴다. 돈이 없어 "없던 일" 이 되면
            // 기다림도 함께 되돌아가므로 쿨다운을 볼 수 없다.
            var first = BoardTalks.Request(w, BoardTalks.Ask.EaseObjective, new Random(1));
            Assert.NotEqual(BoardTalks.Verdict.NoFunds, first.verdict);
            Assert.True(BoardTalks.OnCooldown(w));

            var again = BoardTalks.Request(w, BoardTalks.Ask.WageBudget, new Random(1));
            Assert.Equal(BoardTalks.Verdict.TooSoon, again.verdict);
        }

        [Fact]
        public void 거절당해도_신뢰도가_깎인다()
        {
            // 밑져야 본전이면 늘 조르는 것이 답이 된다.
            var w = Make();
            w.Manager!.boardConfidence = 1;        // 거의 확실히 거절

            int before = w.Manager.boardConfidence;
            var a = BoardTalks.Request(w, BoardTalks.Ask.EaseObjective, new Random(99));

            Assert.NotEqual(BoardTalks.Verdict.Granted, a.verdict);
            Assert.True(w.Manager.boardConfidence < before);
            Assert.True(a.confidenceCost > 0);
        }

        [Fact]
        public void 신뢰도가_높을수록_잘_들어준다()
        {
            var w = Make();

            w.Manager!.boardConfidence = 95;
            float high = BoardTalks.Odds(w, BoardTalks.Ask.TransferBudget);

            w.Manager.boardConfidence = 10;
            float low = BoardTalks.Odds(w, BoardTalks.Ask.TransferBudget);

            Assert.True(high > low);
        }

        [Fact]
        public void 어려운_요청일수록_덜_들어준다()
        {
            // 문턱이 같으면 늘 가장 비싼 것만 조른다.
            var w = Make();
            w.Manager!.boardConfidence = 80;

            Assert.True(BoardTalks.Odds(w, BoardTalks.Ask.TransferBudget)
                      > BoardTalks.Odds(w, BoardTalks.Ask.EaseObjective));
        }

        [Fact]
        public void 들어주면_실제로_바뀐다()
        {
            var w = Make();
            var club = w.MyClub!;
            w.Manager!.boardConfidence = 100;
            club.finances.balance = (long)club.finances.annualRevenue * 2;

            int budgetBefore = club.finances.weeklyWageBudget;

            // 확실히 들어줄 때까지 날짜를 밀며 되풀이한다.
            for (int i = 0; i < 40 && club.finances.weeklyWageBudget == budgetBefore; i++)
            {
                w.LastBoardAskDay = 0;
                BoardTalks.Request(w, BoardTalks.Ask.WageBudget, new Random(i));
                w.Manager.boardConfidence = 100;
            }

            Assert.True(club.finances.weeklyWageBudget > budgetBefore);
            Assert.True(club.boardWageBonus > 0);
        }

        [Fact]
        public void 이사회가_얹어_준_급여는_재산정에도_남는다()
        {
            // 다음 시즌에 날아가면 얻어 낸 의미가 없다.
            var w = Make();
            var club = w.MyClub!;
            w.Manager!.boardConfidence = 100;
            club.finances.balance = (long)club.finances.annualRevenue * 2;

            for (int i = 0; i < 40 && club.boardWageBonus == 0; i++)
            {
                w.LastBoardAskDay = 0;
                BoardTalks.Request(w, BoardTalks.Ask.WageBudget, new Random(i));
                w.Manager.boardConfidence = 100;
            }

            Assert.True(club.boardWageBonus > 0);
            int bonus = club.boardWageBonus;

            Seasons(w, 1);
            Assert.Equal(bonus, w.Club(club.id)!.boardWageBonus);
        }

        [Fact]
        public void 협상_기록이_세이브를_왕복한다()
        {
            var w = Make();
            BoardTalks.Request(w, BoardTalks.Ask.Facilities, new Random(3));

            var back = SaveGame.Deserialize(SaveGame.Serialize(w, "이사회")).world!;
            Assert.Equal(w.LastBoardAskDay, back.LastBoardAskDay);
            Assert.Equal(w.MyClub!.boardWageBonus, back.MyClub!.boardWageBonus);
        }

        // ── 시상·명예 ─────────────────────────────────────────────────────

        [Fact]
        public void 우승이_구단_기록에_남는다()
        {
            // 지금까지 20년을 지배해도 명예는 세계를 만들 때의 숫자 그대로였다.
            var w = Make(606);
            int before = w.Clubs.Values.Sum(c =>
                c.history.leagueTitles + c.history.cupTitles + c.history.continentalTitles);

            Seasons(w, 3);

            int after = w.Clubs.Values.Sum(c =>
                c.history.leagueTitles + c.history.cupTitles + c.history.continentalTitles);

            // 1부 4곳 + 국내컵 4곳 + 대륙 1곳 = 시즌당 9
            Assert.Equal(before + 27, after);
        }

        [Fact]
        public void 하부_리그_1위는_우승이_아니다()
        {
            // 2·3부 1위는 승격이지 우승이 아니다.
            var w = Make(606);
            Seasons(w, 2);

            // 시즌당 리그 우승은 1부 수만큼만 늘어야 한다.
            int leagueTitles = w.Clubs.Values.Sum(c => c.history.leagueTitles);
            var lower = w.Leagues.Values.Where(l => l.tier != LeagueTier.First).ToList();
            Assert.NotEmpty(lower);   // 하부 리그가 있는 세계여야 의미가 있다

            var fresh = WorldGenerator.Create(606);
            int baseline = fresh.Clubs.Values.Sum(c => c.history.leagueTitles);
            int firstTierCount = w.Leagues.Values.Count(l => l.tier == LeagueTier.First);

            Assert.Equal(baseline + firstTierCount * 2, leagueTitles);
        }

        [Fact]
        public void 상이_두_경기_뛴_선수에게_가지_않는다()
        {
            var w = Make(606);
            Seasons(w, 3);

            var sets = w.Awards.ToList();
            Assert.NotEmpty(sets);

            foreach (var a in sets)
            {
                Assert.False(string.IsNullOrEmpty(a.bestPlayerName));
                var p = w.Player(a.bestPlayerId);
                if (p?.careerStats != null)
                    Assert.True(p.careerStats.appearances >= 5,
                        $"{a.bestPlayerName} 이 {p.careerStats.appearances}경기로 MVP 를 받았다");
            }
        }

        [Fact]
        public void 올해의_감독은_우승팀이_아니라_전력_대비로_뽑는다()
        {
            // 우승팀에 주면 늘 가장 부유한 구단이 받는다.
            var w = Make(606);
            Seasons(w, 5);

            var sets = w.Awards.ToList();
            Assert.NotEmpty(sets);

            // 다섯 시즌 내내 같은(가장 부유한) 구단이 받지는 않아야 한다.
            var byLeague = sets.GroupBy(a => a.leagueId).First(g => g.Count() >= 3);
            Assert.True(byLeague.Select(a => a.managerClubId).Distinct().Count() > 1,
                "올해의 감독이 늘 같은 구단에 간다");

            Assert.All(sets, a => Assert.False(string.IsNullOrEmpty(a.managerClubName)));
        }

        [Fact]
        public void 수상자_이름이_선수가_사라져도_남는다()
        {
            // id 만 두면 은퇴한 수상자가 "(은퇴)" 가 된다 — 잊는 기록은 기록이 아니다.
            var w = Make(606);
            Seasons(w, 8);

            var gone = w.Awards.Where(a => w.Player(a.topScorerId) == null).ToList();
            Assert.All(gone, a => Assert.False(string.IsNullOrEmpty(a.topScorerName)));
        }

        [Fact]
        public void 시상_기록이_끝없이_쌓이지_않는다()
        {
            var w = Make(606);
            Seasons(w, 12);
            Assert.True(w.Awards.Count <= BalanceConfig.AwardsKept);
        }

        [Fact]
        public void 내_임기_우승을_따로_센다()
        {
            var w = Make(606);
            var h = w.MyClub!.history;

            Assert.Equal(h.leagueTitles + h.cupTitles + h.continentalTitles, w.TitlesAtTakeover);

            h.cupTitles += 2;
            int mine = h.leagueTitles + h.cupTitles + h.continentalTitles - w.TitlesAtTakeover;
            Assert.Equal(2, mine);
        }

        [Fact]
        public void 시상과_명예가_세이브를_왕복한다()
        {
            var w = Make(606);
            Seasons(w, 2);

            var back = SaveGame.Deserialize(SaveGame.Serialize(w, "기록")).world!;

            Assert.Equal(w.Awards.Count, back.Awards.Count);
            Assert.Equal(w.TitlesAtTakeover, back.TitlesAtTakeover);

            for (int i = 0; i < w.Awards.Count; i++)
            {
                Assert.Equal(w.Awards[i].bestPlayerName, back.Awards[i].bestPlayerName);
                Assert.Equal(w.Awards[i].topScorerGoals, back.Awards[i].topScorerGoals);
                Assert.Equal(w.Awards[i].managerClubName, back.Awards[i].managerClubName);
            }

            foreach (var c in w.Clubs.Values)
                Assert.Equal(c.history.leagueTitles, back.Club(c.id)!.history.leagueTitles);
        }

        [Fact]
        public void 아무것도_못_받으면_없던_일이_된다()
        {
            // 들어주겠다고 해 놓고 내줄 것이 없는데 신뢰도와 기다림만 치르면 요청이 벌이 된다.
            var w = Make();
            var club = w.MyClub!;
            w.Manager!.boardConfidence = 100;
            club.finances.balance = 0;                 // 내줄 돈이 없다
            club.finances.transferBudget = 0;

            int before = w.Manager.boardConfidence;

            // 들어주기로 결정될 때까지 돌린다 — 거절이면 정상적으로 깎여야 맞다.
            BoardTalks.Answer a = null!;
            for (int i = 0; i < 60; i++)
            {
                w.LastBoardAskDay = 0;
                w.Manager.boardConfidence = 100;
                a = BoardTalks.Request(w, BoardTalks.Ask.TransferBudget, new Random(i));
                if (a.verdict == BoardTalks.Verdict.NoFunds) break;
            }

            Assert.True(a.verdict == BoardTalks.Verdict.NoFunds,
                $"verdict={a.verdict} headline={a.headline}");
            Assert.True(a.confidenceCost == 0, $"cost={a.confidenceCost}");
            Assert.True(w.Manager.boardConfidence == 100, $"confidence={w.Manager.boardConfidence}");
            Assert.False(BoardTalks.OnCooldown(w));     // 기다림도 없던 일
        }
    }
}
}
