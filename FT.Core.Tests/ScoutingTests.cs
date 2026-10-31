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
    /// 선수 정보의 불확실성과 2군.
    /// 모르는 선수를 숫자로 비교할 수 있으면 스카우트를 보낼 이유가 없다.
    /// </summary>
    public class ScoutingTests
    {
        static GameWorld Make(int seed = 31415, int stars = 4)
        {
            var w = WorldGenerator.Create(seed);
            var club = w.Clubs.Values.First(c => c.reputation == stars);
            ManagerCareer.TakeCharge(w, club.id, "테스트");
            return w;
        }

        static PlayerData Stranger(GameWorld w)
        {
            // 다른 나라, 대표 경력 없는 선수라야 아무것도 모르는 상태다.
            var myNation = w.NationOf(w.ManagedClubId)?.Country;
            return w.Players.Values.First(p =>
                p.clubId > 0 && p.caps == 0 && w.NationOf(p.clubId)?.Country != myNation);
        }

        // ── 아는 정도 ─────────────────────────────────────────────────────

        [Fact]
        public void 내_선수는_정확히_안다()
        {
            var w = Make();
            foreach (var p in w.Squad(w.ManagedClubId))
            {
                Assert.Equal(1f, Scouting.Knowledge(w, p), 3);
                Assert.Equal(MathF.Round(GameWorld.Ca(p)).ToString("F0"),
                             Scouting.Label(w, p, GameWorld.Ca(p)));
            }
        }

        [Fact]
        public void 먼_나라_선수는_구간으로만_보인다()
        {
            var w = Make();
            var p = Stranger(w);

            Assert.True(Scouting.Knowledge(w, p) < 0.3f);
            Assert.Contains("~", Scouting.Label(w, p, GameWorld.Ca(p)));
        }

        [Fact]
        public void 같은_리그_선수는_어느_정도_안다()
        {
            // 매주 만나는 상대를 전혀 모른다고 하면 이상하다.
            var w = Make();
            var league = w.MyLeague!;
            var rival = w.Squad(league.clubIds.First(id => id != w.ManagedClubId)).First();

            Assert.True(Scouting.Knowledge(w, rival) >= BalanceConfig.SameLeagueKnowledge);
            Assert.True(Scouting.Knowledge(w, rival) < 1f);
        }

        [Fact]
        public void 국가대표는_누구나_어느_정도_안다()
        {
            var w = Make();
            var capped = w.Players.Values.FirstOrDefault(p => p.clubId > 0 && p.caps > 0);
            if (capped == null)
            {
                capped = Stranger(w);
                capped.caps = 5;
            }

            Assert.True(Scouting.Knowledge(w, capped) >= BalanceConfig.CappedKnowledge);
        }

        // ── 관찰 ──────────────────────────────────────────────────────────

        [Fact]
        public void 관찰할수록_구간이_좁아진다()
        {
            var w = Make();
            var p = Stranger(w);

            float Width()
            {
                var (low, high, _) = Scouting.Estimate(w, p, GameWorld.Ca(p));
                return high - low;
            }

            float before = Width();

            for (int i = 0; i < BalanceConfig.ScoutReportsForFull; i++)
            {
                Scouting.StartWeek(w);
                Assert.True(Scouting.SendScout(w, p));
            }

            Assert.True(Width() < before);
            Assert.Equal(0f, Width(), 3);        // 다 보면 정확히 안다
            Assert.Equal(1f, Scouting.Knowledge(w, p), 3);
        }

        [Fact]
        public void 주당_관찰_횟수가_정해져_있다()
        {
            // 공짜면 첫 주에 세계의 모든 선수를 알아낼 수 있다.
            var w = Make();
            Scouting.StartWeek(w);

            int allowed = w.ScoutingLeft;
            Assert.True(allowed > 0);

            var targets = w.Players.Values
                .Where(p => p.clubId > 0 && p.clubId != w.ManagedClubId && p.caps == 0)
                .Take(allowed + 5)
                .ToList();

            int done = targets.Count(p => Scouting.SendScout(w, p));
            Assert.Equal(allowed, done);
            Assert.Equal(0, w.ScoutingLeft);
        }

        [Fact]
        public void 큰_구단이_더_많이_관찰한다()
        {
            var big = Make(31415, stars: 5);
            var small = Make(31415, stars: 1);

            Scouting.StartWeek(big);
            Scouting.StartWeek(small);

            Assert.True(big.ScoutingLeft > small.ScoutingLeft);
        }

        [Fact]
        public void 내_선수는_관찰할_수_없다()
        {
            var w = Make();
            Scouting.StartWeek(w);
            int left = w.ScoutingLeft;

            Assert.False(Scouting.SendScout(w, w.Squad(w.ManagedClubId).First()));
            Assert.Equal(left, w.ScoutingLeft);
        }

        [Fact]
        public void 추정은_같은_선수에게_늘_같다()
        {
            // 볼 때마다 값이 바뀌면 추정이 아니라 잡음이다.
            var w = Make();
            var p = Stranger(w);

            var first = Scouting.Estimate(w, p, GameWorld.Ca(p));
            for (int i = 0; i < 5; i++)
                Assert.Equal(first, Scouting.Estimate(w, p, GameWorld.Ca(p)));
        }

        [Fact]
        public void 추정이_틀릴_수_있다()
        {
            // 구간이 늘 참값을 한가운데 품으면 넓이가 아무 의미가 없다.
            var w = Make();
            int offCentre = 0, total = 0;

            foreach (var p in w.Players.Values.Where(x => x.clubId > 0 && x.caps == 0).Take(300))
            {
                if (Scouting.Knowledge(w, p) >= 1f) continue;
                float real = GameWorld.Ca(p);
                var (_, _, shown) = Scouting.Estimate(w, p, real);
                total++;
                if (Math.Abs(shown - real) > 1f) offCentre++;
            }

            Assert.True(total > 100);
            Assert.True(offCentre > total / 2, "추정이 늘 참값과 같다 — 불확실성이 없다");
        }

        [Fact]
        public void 영입하면_바로_알게_된다()
        {
            var w = Make();
            var p = Stranger(w);
            Scouting.StartWeek(w);
            Scouting.SendScout(w, p);
            Assert.NotEmpty(w.ScoutReports);

            TransferMarket.Execute(w, TransferMarket.BuildOffer(
                w, p, TransferMarket.AskingPrice(w, p), p.contract?.weeklyWage ?? 1000, 3));

            Assert.Equal(1f, Scouting.Knowledge(w, p), 3);
            Assert.DoesNotContain(p.id, w.ScoutReports.Keys);
        }

        [Fact]
        public void 보고서가_세이브를_왕복한다()
        {
            var w = Make();
            var p = Stranger(w);
            Scouting.StartWeek(w);
            Scouting.SendScout(w, p);

            var back = SaveGame.Deserialize(SaveGame.Serialize(w, "스카우트")).world!;
            Assert.Equal(w.ScoutReports[p.id], back.ScoutReports[p.id]);
        }

        // ── 2군 ───────────────────────────────────────────────────────────

        [Fact]
        public void 예비팀은_1군_명단_밖의_선수로_짜인다()
        {
            var w = Make();
            var club = w.MyClub!;
            var tac = club.savedTactics.First(t => t.id == club.activeTacticsId);
            var firstTeam = tac.slots.Select(s => s.assignedPlayerId).ToHashSet();

            foreach (var p in ReserveLeague.Squad(w, club))
            {
                Assert.DoesNotContain(p.id, firstTeam);
                Assert.False(p.IsInjured);
                Assert.DoesNotContain(p.id, w.Loans.Keys);
            }
        }

        [Fact]
        public void 예비팀은_선수를_새로_만들지_않는다()
        {
            // 구단마다 2군 스쿼드를 만들면 선수가 두 배가 되고 세이브가 불어난다.
            var w = Make();
            int before = w.Players.Count;

            ReserveLeague.RunMatchday(w, new Random(7));

            Assert.Equal(before, w.Players.Count);
            Assert.Equal(WorldGenerator.Tiers * 4, w.Leagues.Count);
        }

        [Fact]
        public void 예비팀에서_뛰면_출전과_사기가_오르고_체력을_쓴다()
        {
            var w = Make();
            var squad = ReserveLeague.Squad(w, w.MyClub!);
            Assert.NotEmpty(squad);

            var before = squad.ToDictionary(p => p.id, p => (p.condition, p.morale));
            ReserveLeague.RunMatchday(w, new Random(7));

            foreach (var p in squad)
            {
                Assert.Equal(1, p.reserveAppearances);
                Assert.True(p.condition < before[p.id].condition);
                Assert.True(p.morale >= before[p.id].morale);
            }
        }

        [Fact]
        public void 예비팀_출전은_벤치보다_낫고_1군만_못하다()
        {
            // 1군과 같으면 아무도 1군에 들려 하지 않는다.
            var bench = new PlayerData { reserveAppearances = 0 };
            var reserve = new PlayerData { reserveAppearances = 3 };

            float first = ReserveLeague.MinutesFactor(reserve, startsForFirstTeam: true);
            float res = ReserveLeague.MinutesFactor(reserve, startsForFirstTeam: false);
            float sat = ReserveLeague.MinutesFactor(bench, startsForFirstTeam: false);

            Assert.True(first > res);
            Assert.True(res > sat);
        }

        [Fact]
        public void 예비팀_일정이_리그_컵_A매치와_겹치지_않는다()
        {
            Assert.Empty(ReserveLeague.Matchdays.Intersect(NationalTeam.BreakMatchdays));
            Assert.Empty(ReserveLeague.Matchdays.Intersect(CupCompetition.DomesticMatchdays));
            Assert.Empty(ReserveLeague.Matchdays.Intersect(CupCompetition.GroupMatchdays));
            Assert.Empty(ReserveLeague.Matchdays.Intersect(CupCompetition.ContinentalKnockoutMatchdays));
        }

        [Fact]
        public void 시즌을_치르면_예비팀_출전이_쌓인다()
        {
            var w = Make(606);
            int guard = 0;
            while (!w.SeasonFinished && guard++ < 60)
                if (GameEngine.PlayNextRound(w) == null) break;

            var fringe = w.Squad(w.ManagedClubId).Where(p => p.reserveAppearances > 0).ToList();
            Assert.NotEmpty(fringe);

            // 시즌이 바뀌면 비워진다.
            SeasonRollover.StartNextSeason(w);
            Assert.All(w.Players.Values, p => Assert.Equal(0, p.reserveAppearances));
        }
    }
}
