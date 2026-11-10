using System.Linq;
using FootballTactician.Data;
using FootballTactician.World;
using Xunit;

namespace FT.Core.Tests
{
    /// <summary>
    /// 일정에 없는 내 경기 한 판 — 컵 경기를 감독이 직접 뛴다.
    ///
    /// <b>미루기는 호출자가 고른다.</b> 끄고 부르면 지금까지와 한 글자도 다르지 않아야 한다
    /// (테스트와 하네스가 전부 그 길로 돈다). 켜면 내 컵 경기 앞에서 세계가 멈춰야 한다.
    /// </summary>
    public class PendingMatchTests
    {
        static GameWorld Start(int seed)
        {
            var w = WorldGenerator.Create(seed);
            var club = w.Clubs.Values.First(c => c.reputation == 4);
            ManagerCareer.TakeCharge(w, club.id, "테스트");
            return w;
        }

        /// <summary>내 컵 경기가 걸릴 때까지 돌린다. 걸렸으면 true.</summary>
        static bool PlayUntilPending(GameWorld w, int rounds = 30)
        {
            for (int i = 0; i < rounds && w.pendingMatch == null; i++)
                GameEngine.PlayNextRound(w, deferMyCupTie: true);
            return w.pendingMatch != null;
        }

        [Fact]
        public void 끄고_부르면_미뤄_두지_않는다()
        {
            var w = Start(31337);
            for (int i = 0; i < 20; i++) GameEngine.PlayNextRound(w);

            Assert.Null(w.pendingMatch);
        }

        [Fact]
        public void 켜면_내_컵_경기_앞에서_멈춘다()
        {
            var w = Start(31337);
            Assert.True(PlayUntilPending(w), "스무 라운드를 돌려도 내 컵 경기가 안 나왔다");

            var p = w.pendingMatch!;

            // 내 경기여야 한다.
            Assert.True(p.fixture.homeClubId == w.ManagedClubId
                        || p.fixture.awayClubId == w.ManagedClubId);
            Assert.False(p.fixture.isPlayed);
            Assert.False(string.IsNullOrWhiteSpace(p.label));

            // 일정에는 들어 있지 않다 — 들어가면 순위표가 센다.
            Assert.DoesNotContain(w.MyLeague!.currentSeason!.fixtures, f => f.id == p.fixture.id);

            // 그 경기를 치르기 전에는 세계가 더 나아가지 않는다.
            var before = w.Date;
            Assert.Null(GameEngine.PlayNextRound(w, deferMyCupTie: true));
            Assert.Equal(before, w.Date);
        }

        [Fact]
        public void 바로_결과를_보면_대진이_이어진다()
        {
            var w = Start(31337);
            Assert.True(PlayUntilPending(w));

            var p = w.pendingMatch!;
            var cup = w.Cups.Values.First(c => c.id == p.cupId);
            int roundsBefore = cup.rounds.Count;
            var before = w.Date;

            var result = GameEngine.SimulatePending(w);

            Assert.NotNull(result);
            Assert.Null(w.pendingMatch);

            // 내 경기가 대진에 들어갔다 — 그 라운드에 안 치른 경기가 없어야 한다.
            var round = cup.rounds[roundsBefore - 1];
            Assert.All(round.ties, t => Assert.True(t.played));

            // 한 주가 넘어갔다.
            Assert.True(w.Date > before);
        }

        [Fact]
        public void 직접_치러도_같은_자리로_들어간다()
        {
            var w = Start(4242);
            Assert.True(PlayUntilPending(w));

            var p = w.pendingMatch!;
            var cup = w.Cups.Values.First(c => c.id == p.cupId);
            var tie = cup.Current!.ties.First(
                t => t.homeClubId == w.ManagedClubId || t.awayClubId == w.ManagedClubId);

            var session = GameEngine.BeginPendingMatch(w);
            Assert.NotNull(session);
            while (!session!.Finished) session.AdvanceBlock();

            var result = GameEngine.CompletePendingMatch(w, session);

            Assert.NotNull(result);
            Assert.Null(w.pendingMatch);
            Assert.True(tie.played);
            Assert.Equal(result!.homeGoals, tie.homeGoals);
            Assert.Equal(result.awayGoals, tie.awayGoals);
            Assert.True(tie.WinnerClubId > 0);
        }

        /// <summary>
        /// 결과에 경기 기록이 들어 있어야 한다.
        ///
        /// 골 수만 채워 넘겼더니 경기 화면이 점유율을 그리다 터졌다(Match.razor). 대진은
        /// 골 수만 있으면 되지만 화면은 아니다 — 화면이 읽는 칸은 비워 두면 안 된다.
        /// </summary>
        [Fact]
        public void 바로_본_결과에도_경기_기록이_있다()
        {
            var w = Start(31337);
            Assert.True(PlayUntilPending(w));

            var result = GameEngine.SimulatePending(w);

            Assert.NotNull(result);
            Assert.NotNull(result!.homeStats);
            Assert.NotNull(result.awayStats);
            Assert.True(result.homeStats.possession + result.awayStats.possession > 0,
                        "점유율이 둘 다 0 이면 기록이 비어 있는 것이다");
        }

        [Fact]
        public void 직접_치른_결과에도_경기_기록이_있다()
        {
            var w = Start(4242);
            Assert.True(PlayUntilPending(w));

            var session = GameEngine.BeginPendingMatch(w)!;
            while (!session.Finished) session.AdvanceBlock();
            var result = GameEngine.CompletePendingMatch(w, session);

            Assert.NotNull(result!.homeStats);
            Assert.NotNull(result.awayStats);
        }

        [Fact]
        public void 미뤄_둔_경기는_세이브를_왕복한다()
        {
            var w = Start(31337);
            Assert.True(PlayUntilPending(w));

            var p = w.pendingMatch!;
            var back = SaveGame.Deserialize(SaveGame.Serialize(w, "미뤄 둔 경기")).world;

            Assert.NotNull(back.pendingMatch);
            Assert.Equal(p.cupId, back.pendingMatch!.cupId);
            Assert.Equal(p.matchday, back.pendingMatch.matchday);
            Assert.Equal(p.label, back.pendingMatch.label);
            Assert.Equal(p.fixture.homeClubId, back.pendingMatch.fixture.homeClubId);
            Assert.Equal(p.fixture.awayClubId, back.pendingMatch.fixture.awayClubId);
        }

        [Fact]
        public void 미뤄_두고_이어_달려도_시즌이_끝난다()
        {
            // 멈췄다 이어 붙이는 길이 한 시즌을 끝까지 가는지. 한 번이라도 어긋나면
            // 주가 안 넘어가거나 대진이 멈춘 채로 남는다.
            var w = Start(909);
            int seen = 0;

            for (int guard = 0; guard < 120 && !w.SeasonFinished; guard++)
            {
                if (w.pendingMatch != null)
                {
                    seen++;
                    GameEngine.SimulatePending(w);
                    continue;
                }
                GameEngine.PlayNextRound(w, deferMyCupTie: true);
            }

            Assert.True(w.SeasonFinished, "시즌이 끝나지 않았다");
            Assert.True(seen > 0, "컵 경기를 한 번도 안 미뤘다 — 길을 탔는지 알 수 없다");
            Assert.Null(w.pendingMatch);

            // 리그마다 라운드 수가 다르다. 남은 경기를 마치면 하나도 빠짐이 없어야 한다.
            SeasonRollover.FinishRemainingFixtures(w);
            Assert.All(w.Leagues.Values,
                       l => Assert.All(l.currentSeason!.fixtures, f => Assert.True(f.isPlayed)));
        }
    }
}
