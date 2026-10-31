using System;
using System.Linq;
using FootballTactician.Data;
using FootballTactician.Simulation;
using FootballTactician.World;
using Xunit;

namespace FT.Core.Tests
{
    /// <summary>
    /// 기자회견. 말로 경질을 피할 수 있으면 경기 결과가 의미를 잃는다.
    /// </summary>
    public class PressTests
    {
        static GameWorld Make(int seed = 4242)
        {
            var w = WorldGenerator.Create(seed);
            var club = w.Clubs.Values.First(c => c.reputation == 3);
            ManagerCareer.TakeCharge(w, club.id, "테스트");
            return w;
        }

        /// <summary>이사회 신뢰도를 가장 많이 올리는 답을 고른다.</summary>
        static int BestBoardOption(PressConferenceSession s)
        {
            int best = 0;
            for (int i = 1; i < s.options.Count; i++)
                if (s.options[i].boardDelta > s.options[best].boardDelta) best = i;
            return best;
        }

        [Fact]
        public void 기자회견은_간격을_두고_열린다()
        {
            var w = Make();
            Assert.False(PressRoom.IsDue(w));                 // 개막 전에는 없다

            for (int i = 0; i < PressRoom.MinRoundsBetween; i++) GameEngine.PlayNextRound(w);
            Assert.True(PressRoom.IsDue(w));

            var session = PressRoom.Open(w);
            Assert.NotNull(session);
            Assert.NotEmpty(session!.options);
            Assert.False(string.IsNullOrWhiteSpace(session.questionText));

            PressRoom.Answer(w, 0);
            Assert.Null(w.PendingPress);
            Assert.False(PressRoom.IsDue(w));                 // 바로 다음 라운드에는 안 열린다
        }

        [Fact]
        public void 말로는_경질을_피할_수_없다()
        {
            // 상한이 없으면 매번 이사회 신뢰도를 최대로 올려 성적과 무관하게 버틸 수 있다.
            var w = Make();
            w.Manager.boardConfidence = 50;
            int start = w.Manager.boardConfidence;

            for (int round = 0; round < 38; round++)
            {
                GameEngine.PlayNextRound(w);
                if (w.Sacked) break;

                var s = PressRoom.Open(w);
                if (s != null) PressRoom.Answer(w, BestBoardOption(s));
            }

            Assert.True(w.PressBoardSpent <= PressRoom.BoardBudgetPerSeason,
                $"기자회견으로 신뢰도를 {w.PressBoardSpent} 올렸다 — 한도 {PressRoom.BoardBudgetPerSeason}");
        }

        [Fact]
        public void 실언으로_깎이는_것은_한도에_걸리지_않는다()
        {
            // 한도는 올리는 쪽만 막는다. 깎이는 것까지 막으면 위험한 답변이 공짜가 된다.
            var w = Make();
            w.PressBoardSpent = PressRoom.BoardBudgetPerSeason;   // 한도 소진
            w.Manager.boardConfidence = 50;
            w.Manager.pressStyle = ManagerPressStyle.Confrontational;

            var session = PressConferenceSystem.GenerateSession(
                PressConferenceQuestion.ManagerJobSecurity, new PressContext());
            w.PendingPress = session;

            int worst = 0;
            for (int i = 1; i < session.options.Count; i++)
                if (session.options[i].boardDelta < session.options[worst].boardDelta) worst = i;
            Assert.True(session.options[worst].boardDelta < 0, "깎이는 선택지가 없다");

            PressRoom.Answer(w, worst);
            Assert.True(w.Manager.boardConfidence < 50, "한도가 하락까지 막아 버렸다");
        }

        [Theory]
        [InlineData(2, 130, 3)]    // 2 * 1.3 = 2.6 → 3. 정수 나눗셈이면 2 로 버려져 보너스가 사라진다
        [InlineData(3, 130, 4)]
        [InlineData(4, 130, 5)]
        [InlineData(3, 70, 2)]
        [InlineData(-3, 130, -4)]
        [InlineData(1, 70, 1)]     // 방향이 0 으로 사라지면 안 된다
        [InlineData(-1, 70, -1)]
        [InlineData(0, 130, 0)]
        public void 성향_배수가_실제로_효과를_낸다(int delta, int mult, int expected)
            => Assert.Equal(expected, PressConferenceSystem.Scale(delta, mult));

        [Fact]
        public void 성향이_맞으면_손해가_아니다()
        {
            // 130%를 받고 더 적게 오르면 성향 체계가 거꾸로 작동하는 셈이다.
            for (int d = 1; d <= 6; d++)
            {
                Assert.True(PressConferenceSystem.Scale(d, 130) >= PressConferenceSystem.Scale(d, 100));
                Assert.True(PressConferenceSystem.Scale(d, 70) <= PressConferenceSystem.Scale(d, 100));
            }
        }

        [Fact]
        public void 소식함_숫자가_실제_변화와_같다()
        {
            // 적힌 값과 실제 변화가 다르면 선택의 근거가 무너진다.
            var w = Make();
            w.Manager.pressStyle = ManagerPressStyle.Analytical;
            for (int i = 0; i < PressRoom.MinRoundsBetween; i++) GameEngine.PlayNextRound(w);

            var session = PressRoom.Open(w)!;
            int pick = session.options.FindIndex(o => o.tone == ManagerPressStyle.Analytical);
            if (pick < 0) pick = 0;

            int press = w.Manager.pressReputation;
            int fan = w.Manager.fanApproval;
            PressRoom.Answer(w, pick);

            var note = w.Inbox.First(m => m.title == "기자회견").body;
            int pressMoved = w.Manager.pressReputation - press;
            int fanMoved = w.Manager.fanApproval - fan;

            if (pressMoved != 0)
                Assert.Contains($"언론 {(pressMoved > 0 ? "+" : "")}{pressMoved}", note);
            if (fanMoved != 0)
                Assert.Contains($"팬 {(fanMoved > 0 ? "+" : "")}{fanMoved}", note);
        }

        [Fact]
        public void 시즌이_바뀌면_한도가_돌아오고_감정이_식는다()
        {
            var w = Make(555);
            w.PressBoardSpent = PressRoom.BoardBudgetPerSeason;
            w.Manager.fanApproval = 100;
            w.Manager.pressReputation = 0;

            int guard = 0;
            while (!w.SeasonFinished && guard++ < 60)
                if (GameEngine.PlayNextRound(w) == null) break;
            SeasonRollover.StartNextSeason(w);

            Assert.Equal(0, w.PressBoardSpent);
            Assert.Null(w.PendingPress);
            Assert.InRange(w.Manager.fanApproval, 51, 99);        // 중간으로 당겨진다
            Assert.InRange(w.Manager.pressReputation, 1, 49);
        }

        [Fact]
        public void 팬_지지도는_입장_수입을_움직인다()
        {
            // 지지도가 죽은 숫자면 기자회견의 절반은 장식이다.
            Assert.Equal(1f, PressRoom.RevenueMultiplier(new ManagerProfile { fanApproval = PressRoom.Neutral }), 3);

            float high = PressRoom.RevenueMultiplier(new ManagerProfile { fanApproval = 100 });
            float low = PressRoom.RevenueMultiplier(new ManagerProfile { fanApproval = 0 });

            Assert.Equal(1f + PressRoom.FanRevenueSwing, high, 3);
            Assert.Equal(1f - PressRoom.FanRevenueSwing, low, 3);
        }

        [Fact]
        public void 기자회견에_한_번도_안_나가도_손해는_아니다()
        {
            // 기본값이 중립이어야 한다. 안 나가는 쪽이 불리하면 사실상 강제가 된다.
            var w = Make();
            Assert.Equal(1f, PressRoom.RevenueMultiplier(w.Manager), 3);
            Assert.Equal(0, PressRoom.ReputationBonus(w.Manager));
        }

        [Fact]
        public void 건너뛰면_언론만_조금_식는다()
        {
            var w = Make();
            for (int i = 0; i < PressRoom.MinRoundsBetween; i++) GameEngine.PlayNextRound(w);
            PressRoom.Open(w);

            int press = w.Manager.pressReputation;
            int board = w.Manager.boardConfidence;
            PressRoom.Skip(w);

            Assert.Null(w.PendingPress);
            Assert.Equal(press - 1, w.Manager.pressReputation);
            Assert.Equal(board, w.Manager.boardConfidence);
        }

        [Fact]
        public void 질문은_구단_상황을_따른다()
        {
            // 자리가 위태로우면 그 질문이 나와야 한다.
            var w = Make(7788);
            w.Manager.boardConfidence = 30;

            bool asked = false;
            for (int round = 0; round < 38 && !asked; round++)
            {
                GameEngine.PlayNextRound(w);
                w.Manager.boardConfidence = 30;
                if (w.Sacked) break;

                var s = PressRoom.Open(w);
                if (s == null) continue;
                asked = s.question == PressConferenceQuestion.ManagerJobSecurity;
                PressRoom.Skip(w);
            }

            Assert.True(asked, "신뢰도가 바닥인데 자리 질문이 한 번도 안 나왔다");
        }

        [Fact]
        public void 답변이_받은_소식에_남는다()
        {
            var w = Make();
            for (int i = 0; i < PressRoom.MinRoundsBetween; i++) GameEngine.PlayNextRound(w);
            PressRoom.Open(w);

            int before = w.Inbox.Count;
            PressRoom.Answer(w, 0);

            Assert.Equal(before + 1, w.Inbox.Count);
            Assert.Contains(w.Inbox, m => m.title == "기자회견");
        }

        [Fact]
        public void 세이브를_왕복해도_기자회견이_남는다()
        {
            var w = Make(9191);
            for (int i = 0; i < PressRoom.MinRoundsBetween; i++) GameEngine.PlayNextRound(w);
            var session = PressRoom.Open(w)!;
            w.PressBoardSpent = 3;

            var back = SaveGame.Deserialize(SaveGame.Serialize(w, "기자회견")).world!;

            Assert.NotNull(back.PendingPress);
            Assert.Equal(session.questionText, back.PendingPress!.questionText);
            Assert.Equal(session.options.Count, back.PendingPress.options.Count);
            Assert.Equal(session.options[0].text, back.PendingPress.options[0].text);
            Assert.Equal(3, back.PressBoardSpent);
            Assert.Equal(w.LastPressRound, back.LastPressRound);
        }
    }
}
