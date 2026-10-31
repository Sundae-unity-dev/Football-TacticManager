using System;
using System.Collections.Generic;
using System.Linq;
using FootballTactician.Data;
using FootballTactician.Simulation;

namespace FootballTactician.World
{
    /// <summary>
    /// 기자회견을 언제 열고, 답변이 게임에 어떻게 돌아오는지.
    ///
    /// <see cref="PressConferenceSystem"/> 이 질문과 선택지를, 여기서는 일정과 결과를 맡는다.
    /// </summary>
    public static class PressRoom
    {
        /// <summary>기자회견 사이 최소 간격(라운드). 매 경기 열면 답변이 의례가 된다.</summary>
        public const int MinRoundsBetween = 3;

        /// <summary>
        /// 한 시즌에 기자회견으로 움직일 수 있는 이사회 신뢰도의 총량.
        ///
        /// 신뢰도는 경질을 가르는 수치다. 상한이 없으면 성적이 어떻든 말만 잘해서
        /// 자리를 지킬 수 있고, 그러면 경기 결과가 의미를 잃는다.
        /// </summary>
        public const int BoardBudgetPerSeason = 8;

        /// <summary>
        /// 팬 지지도·언론 평판의 중립값. 기자회견에 한 번도 나가지 않은 감독이 여기 선다.
        /// 이 값이 효과의 기준선이라, 출발점이 다르면 가만히 있어도 보정이 붙는다.
        /// </summary>
        public const int Neutral = 50;

        /// <summary>팬 지지도가 입장 수입에 미치는 폭. 중립값을 기준으로 ±이 값만큼.</summary>
        public const float FanRevenueSwing = 0.06f;

        /// <summary>언론 평판이 시즌 말 감독 평판에 더하는 폭.</summary>
        public const int PressReputationSwing = 3;

        // ── 일정 ──────────────────────────────────────────────────────────

        /// <summary>이번 라운드에 기자회견이 열리는지.</summary>
        public static bool IsDue(GameWorld w)
        {
            if (w.Sacked || w.ManagedClubId <= 0) return false;
            if (w.PendingPress != null) return true;

            int round = w.RoundsPlayed;
            if (round < 1) return false;
            return round - w.LastPressRound >= MinRoundsBetween;
        }

        /// <summary>
        /// 기자회견을 연다. 이미 열려 있으면 그것을 돌려준다.
        /// 질문은 구단 상황에서 고른다 — 지고 있으면 성적을, 부상자가 있으면 부상을 묻는다.
        /// </summary>
        public static PressConferenceSession Open(GameWorld w)
        {
            if (w.PendingPress != null) return w.PendingPress;
            if (!IsDue(w)) return null;

            var ctx = BuildContext(w);
            var rng = new Random(SeedMix.Of(w.Seed, w.RoundsPlayed * 37, 4409));

            w.PendingPress = PressConferenceSystem.GenerateSession(PickQuestion(w, ctx, rng), ctx);
            return w.PendingPress;
        }

        /// <summary>건너뛴다. 아무 말도 하지 않으면 언론이 조금 식는다.</summary>
        public static void Skip(GameWorld w)
        {
            if (w.PendingPress == null) return;

            w.Manager.pressReputation = Math.Clamp(w.Manager.pressReputation - 1, 0, 100);
            w.PendingPress = null;
            w.LastPressRound = w.RoundsPlayed;
        }

        /// <summary>
        /// 답변한다. 이사회 신뢰도 변화는 시즌 예산 안에서만 반영된다.
        /// </summary>
        public static void Answer(GameWorld w, int optionIndex)
        {
            var session = w.PendingPress;
            if (session == null) return;
            if (optionIndex < 0 || optionIndex >= session.options.Count) return;

            int boardBefore = w.Manager.boardConfidence;
            int pressBefore = w.Manager.pressReputation;
            int fanBefore = w.Manager.fanApproval;

            PressConferenceSystem.ApplyAnswer(w.Manager, session, optionIndex);
            int moved = w.Manager.boardConfidence - boardBefore;

            // 올리는 쪽만 예산을 쓴다. 실언으로 깎이는 것까지 막아 주면 위험이 사라진다.
            if (moved > 0)
            {
                int allowed = Math.Max(0, BoardBudgetPerSeason - w.PressBoardSpent);
                int granted = Math.Min(moved, allowed);
                w.Manager.boardConfidence = boardBefore + granted;
                w.PressBoardSpent += granted;
            }

            var chosen = session.options[optionIndex];

            // 성향 배수와 이사회 한도를 거친 뒤의 실제 변화를 적는다.
            // 선택지에 적힌 숫자를 그대로 쓰면 화면과 결과가 어긋난다.
            w.Post("기자회견",
                $"\"{Trim(chosen.text)}\" — {Summary(
                    w.Manager.pressReputation - pressBefore,
                    w.Manager.fanApproval - fanBefore,
                    w.Manager.boardConfidence - boardBefore)}",
                InboxKind.News);

            w.PendingPress = null;
            w.LastPressRound = w.RoundsPlayed;
        }

        // ── 효과 ──────────────────────────────────────────────────────────

        /// <summary>
        /// 팬 지지도가 입장 수입에 미치는 배수. 지지도 50이 기준선이다.
        /// 기자회견에 한 번도 나가지 않아도 1.0 이라 손해가 아니다.
        /// </summary>
        public static float RevenueMultiplier(ManagerProfile mp)
            => mp == null ? 1f : 1f + (mp.fanApproval - Neutral) / (float)Neutral * FanRevenueSwing;

        /// <summary>언론 평판이 시즌 말 감독 평판에 더하는 값.</summary>
        public static int ReputationBonus(ManagerProfile mp)
            => mp == null ? 0 : (int)MathF.Round((mp.pressReputation - Neutral) / (float)Neutral * PressReputationSwing);

        /// <summary>시즌이 바뀌면 예산이 돌아오고, 팬·언론 감정이 중간으로 식는다.</summary>
        public static void StartSeason(GameWorld w)
        {
            w.PressBoardSpent = 0;
            w.LastPressRound = 0;
            w.PendingPress = null;

            // 지난 시즌의 호감이 영원히 남으면 수입 보정이 한쪽으로 굳는다.
            w.Manager.fanApproval = Drift(w.Manager.fanApproval);
            w.Manager.pressReputation = Drift(w.Manager.pressReputation);
        }

        static int Drift(int value) => value + (int)MathF.Round((Neutral - value) * 0.35f);

        // ── 질문 고르기 ────────────────────────────────────────────────────

        static PressConferenceQuestion PickQuestion(GameWorld w, PressContext ctx, Random rng)
        {
            var pool = new List<PressConferenceQuestion>
            {
                PressConferenceQuestion.NextMatchPreparation,
                PressConferenceQuestion.TeamPerformance,
                PressConferenceQuestion.TacticsExplained,
            };

            if (ctx.wasRecentWin || ctx.wasRecentLoss)
                pool.Add(PressConferenceQuestion.RecentResults);

            // 상황에 맞는 질문은 두 번 넣어 더 자주 나오게 한다.
            if (w.Manager.boardConfidence < 45)
            {
                pool.Add(PressConferenceQuestion.ManagerJobSecurity);
                pool.Add(PressConferenceQuestion.ManagerJobSecurity);
            }

            if (w.MyClub != null && w.MyClub.playerIds.Any(id => w.Player(id)?.injury?.isActive == true))
                pool.Add(PressConferenceQuestion.PlayerInjuryUpdate);

            if (w.TransferWindowOpen)
                pool.Add(PressConferenceQuestion.TransferPlans);

            if (w.MyClub != null && w.MyClub.playerIds.Any(id => (w.Player(id)?.GetAge(w.Date) ?? 99) <= 19))
                pool.Add(PressConferenceQuestion.YouthPlayerPromotion);

            if (ctx.currentLeaguePos <= 3)
                pool.Add(PressConferenceQuestion.RivalryComment);

            return pool[rng.Next(pool.Count)];
        }

        static PressContext BuildContext(GameWorld w)
        {
            var league = w.MyLeague;
            var last = league?.currentSeason?.fixtures
                .Where(f => f.isPlayed && f.result != null
                         && (f.homeClubId == w.ManagedClubId || f.awayClubId == w.ManagedClubId))
                .OrderBy(f => f.matchday)
                .LastOrDefault();

            int mine = 0, theirs = 0;
            if (last != null)
            {
                bool home = last.homeClubId == w.ManagedClubId;
                mine = home ? last.result.homeGoals : last.result.awayGoals;
                theirs = home ? last.result.awayGoals : last.result.homeGoals;
            }

            return new PressContext
            {
                wasRecentWin = last != null && mine > theirs,
                wasRecentLoss = last != null && mine < theirs,
                currentLeaguePos = Standings.PositionOf(league, w.ManagedClubId),
                totalLeagueTeams = league?.clubIds.Count ?? 0,
            };
        }

        // ── 표시 ──────────────────────────────────────────────────────────

        static string Trim(string text) => text.Length <= 40 ? text : text[..38] + "…";

        static string Summary(int press, int fan, int board)
        {
            var parts = new List<string>();

            if (press != 0) parts.Add($"언론 {Signed(press)}");
            if (fan != 0) parts.Add($"팬 {Signed(fan)}");
            if (board != 0) parts.Add($"이사회 {Signed(board)}");

            return parts.Count == 0 ? "별다른 반응이 없었습니다." : string.Join(" · ", parts);
        }

        static string Signed(int v) => v > 0 ? $"+{v}" : v.ToString();
    }
}
