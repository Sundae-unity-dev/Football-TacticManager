using System;
using System.Collections.Generic;
using FootballTactician.Data;

namespace FootballTactician.Simulation
{
    public class PressContext
    {
        public bool wasRecentWin;
        public bool wasRecentLoss;
        public int  currentLeaguePos;     // 1 = 1위
        public int  totalLeagueTeams;
        public int  seed = -1;
    }

    /// <summary>
    /// 기자회견 — 질문 → 선택지 생성, 답변 선택 시 ManagerProfile 갱신.
    /// 답변 tone과 ManagerPressStyle 매칭에 따라 효과가 증감된다.
    /// Unity 의존성 없음 — 단위 테스트 가능.
    /// </summary>
    public static class PressConferenceSystem
    {
        // ── 진입점 ────────────────────────────────────────────────────────────

        public static PressConferenceSession GenerateSession(
            PressConferenceQuestion question, PressContext ctx)
        {
            var session = new PressConferenceSession
            {
                question     = question,
                questionText = QuestionText(question),
            };

            switch (question)
            {
                case PressConferenceQuestion.NextMatchPreparation: BuildNextMatch(session);    break;
                case PressConferenceQuestion.RecentResults:        BuildRecentResults(session, ctx); break;
                case PressConferenceQuestion.PlayerInjuryUpdate:   BuildInjury(session);       break;
                case PressConferenceQuestion.TransferPlans:        BuildTransfer(session);     break;
                case PressConferenceQuestion.TeamPerformance:      BuildPerformance(session);  break;
                case PressConferenceQuestion.ManagerJobSecurity:   BuildJobSecurity(session);  break;
                case PressConferenceQuestion.RivalryComment:       BuildRivalry(session);      break;
                case PressConferenceQuestion.YouthPlayerPromotion: BuildYouth(session);        break;
                case PressConferenceQuestion.TacticsExplained:     BuildTactics(session);      break;
                default:                                           BuildNoComment(session);    break;
            }

            return session;
        }

        public static void ApplyAnswer(
            ManagerProfile mp, PressConferenceSession session, int optionIndex)
        {
            if (mp == null || session == null) return;
            if (optionIndex < 0 || optionIndex >= session.options.Count) return;

            session.selectedIndex = optionIndex;
            var opt = session.options[optionIndex];

            int mult = StyleMatchMultiplier(mp.pressStyle, opt.tone);  // 70/100/130

            mp.pressReputation = SimMath.Clamp(mp.pressReputation + Scale(opt.pressDelta, mult), 0, 100);
            mp.fanApproval     = SimMath.Clamp(mp.fanApproval     + Scale(opt.fanDelta,   mult), 0, 100);
            mp.boardConfidence = SimMath.Clamp(mp.boardConfidence + Scale(opt.boardDelta, mult), 0, 100);
        }

        /// <summary>
        /// 효과에 성향 배수를 건다.
        ///
        /// 정수로 나누면 버림이 일어나 130%가 사실상 사라진다 — 2 * 130 / 100 은 2 다.
        /// 효과값이 대개 1~4 라 반올림하지 않으면 보너스 쪽만 죽고 감점 쪽만 산다.
        /// </summary>
        public static int Scale(int delta, int multiplier)
        {
            if (delta == 0) return 0;

            int scaled = (int)Math.Round(delta * multiplier / 100.0, MidpointRounding.AwayFromZero);

            // 부호가 뒤집히거나 0 이 되면 방향 자체가 사라진다. 최소 1 은 남긴다.
            return scaled == 0 ? Math.Sign(delta) : scaled;
        }

        // ── 스타일 매칭 ───────────────────────────────────────────────────────

        public static int StyleMatchMultiplier(
            ManagerPressStyle managerStyle, ManagerPressStyle answerTone)
        {
            if (managerStyle == answerTone) return 130;
            if (IsCompatible(managerStyle, answerTone)) return 100;
            return 70;
        }

        // 호환 쌍 — 같은 결의 어조 (감정 vs 분석 vs 회피). 양방향이라 (x,y)만 한 방향 보유.
        private static readonly (ManagerPressStyle a, ManagerPressStyle b)[] _compatPairs =
        {
            (ManagerPressStyle.Diplomatic, ManagerPressStyle.Reserved),
            (ManagerPressStyle.Diplomatic, ManagerPressStyle.Analytical),
            (ManagerPressStyle.Analytical, ManagerPressStyle.Reserved),
            (ManagerPressStyle.Passionate, ManagerPressStyle.Confrontational),
        };

        private static bool IsCompatible(ManagerPressStyle a, ManagerPressStyle b)
        {
            for (int i = 0; i < _compatPairs.Length; i++)
            {
                var (x, y) = _compatPairs[i];
                if ((a == x && b == y) || (a == y && b == x)) return true;
            }
            return false;
        }

        // ── 질문 텍스트 ───────────────────────────────────────────────────────

        private static string QuestionText(PressConferenceQuestion q) => q switch
        {
            PressConferenceQuestion.NextMatchPreparation => "다음 경기 준비는 어떻게 되어 가나요?",
            PressConferenceQuestion.RecentResults        => "최근 결과에 대해 어떻게 평가하시나요?",
            PressConferenceQuestion.PlayerInjuryUpdate   => "부상 선수의 회복 상황은 어떤가요?",
            PressConferenceQuestion.TransferPlans        => "이번 이적 시장에서 영입 계획이 있나요?",
            PressConferenceQuestion.TeamPerformance      => "팀의 최근 퍼포먼스를 어떻게 보시나요?",
            PressConferenceQuestion.ManagerJobSecurity   => "감독님의 자리에 대한 우려가 있다는 보도가 있는데요?",
            PressConferenceQuestion.RivalryComment       => "라이벌 팀에 대한 한마디 부탁드립니다.",
            PressConferenceQuestion.YouthPlayerPromotion => "유스 선수의 1군 승격 계획은요?",
            PressConferenceQuestion.TacticsExplained     => "오늘 사용한 전술에 대해 설명해 주시겠어요?",
            _                                            => "노코멘트 하시겠습니까?",
        };

        // ── 선택지 빌더 ───────────────────────────────────────────────────────

        private static void BuildNextMatch(PressConferenceSession s)
        {
            s.options.Add(Opt("준비는 완벽합니다. 우리는 승리를 위해 출전합니다.",
                ManagerPressStyle.Passionate, press: 0, fans: +3, board: +1));
            s.options.Add(Opt("쉽지 않은 경기지만 우리 선수들을 믿습니다.",
                ManagerPressStyle.Diplomatic, press: +2, fans: +1, board: 0));
            s.options.Add(Opt("상대 팀에 대해 분석을 마쳤고 명확한 계획이 있습니다.",
                ManagerPressStyle.Analytical, press: +3, fans: 0, board: +2));
            s.options.Add(NoCommentOpt());
        }

        private static void BuildRecentResults(PressConferenceSession s, PressContext ctx)
        {
            if (ctx != null && ctx.wasRecentWin)
            {
                s.options.Add(Opt("결과에 만족합니다. 선수단이 정말 잘 해주고 있어요.",
                    ManagerPressStyle.Passionate, press: +1, fans: +3, board: +2));
                s.options.Add(Opt("좋은 흐름이지만 방심하지 않겠습니다.",
                    ManagerPressStyle.Diplomatic, press: +2, fans: +1, board: +1));
            }
            else if (ctx != null && ctx.wasRecentLoss)
            {
                s.options.Add(Opt("개선해야 할 점이 많습니다. 책임은 저에게 있습니다.",
                    ManagerPressStyle.Diplomatic, press: +3, fans: +1, board: +1));
                s.options.Add(Opt("심판 판정에 분명 문제가 있었습니다.",
                    ManagerPressStyle.Confrontational, press: -3, fans: +2, board: -1));
                s.options.Add(Opt("데이터 상으로는 불운한 패배였습니다. 방향성은 맞습니다.",
                    ManagerPressStyle.Analytical, press: +2, fans: 0, board: +1));
            }
            else
            {
                s.options.Add(Opt("기복이 있지만 전체적으로는 발전하고 있습니다.",
                    ManagerPressStyle.Analytical, press: +1, fans: +1, board: 0));
                s.options.Add(Opt("선수들의 노력에 대해서는 만족합니다.",
                    ManagerPressStyle.Diplomatic, press: 0, fans: +2, board: 0));
            }
            s.options.Add(NoCommentOpt());
        }

        private static void BuildInjury(PressConferenceSession s)
        {
            s.options.Add(Opt("회복 중이며 의료팀과 출전 가능 여부를 상의 중입니다.",
                ManagerPressStyle.Diplomatic, press: +2, fans: 0, board: +1));
            s.options.Add(Opt("선수의 회복을 우선시합니다. 무리하게 복귀시키지 않을 겁니다.",
                ManagerPressStyle.Passionate, press: +1, fans: +3, board: 0));
            s.options.Add(Opt("아직 정확한 진단이 내려지지 않아 답변 드리기 어렵습니다.",
                ManagerPressStyle.Reserved, press: +1, fans: 0, board: +1));
            s.options.Add(NoCommentOpt());
        }

        private static void BuildTransfer(PressConferenceSession s)
        {
            s.options.Add(Opt("현재 스쿼드에 만족하며 추가 영입은 고려하지 않고 있습니다.",
                ManagerPressStyle.Diplomatic, press: 0, fans: +2, board: +1));
            s.options.Add(Opt("이사회와 보강 방안을 논의하고 있습니다.",
                ManagerPressStyle.Analytical, press: +2, fans: 0, board: +2));
            s.options.Add(Opt("구체적인 영입 대상에 대해서는 말씀드릴 수 없습니다.",
                ManagerPressStyle.Reserved, press: -1, fans: 0, board: +2));
            s.options.Add(NoCommentOpt());
        }

        private static void BuildPerformance(PressConferenceSession s)
        {
            s.options.Add(Opt("선수들이 노력하고 있고, 결과는 곧 따라올 것입니다.",
                ManagerPressStyle.Passionate, press: 0, fans: +3, board: +1));
            s.options.Add(Opt("전술적으로 점검해야 할 부분이 분명히 있습니다.",
                ManagerPressStyle.Analytical, press: +3, fans: 0, board: +1));
            s.options.Add(Opt("팀 전체의 책임이지만, 결국은 제 책임입니다.",
                ManagerPressStyle.Diplomatic, press: +3, fans: +2, board: +1));
            s.options.Add(NoCommentOpt());
        }

        private static void BuildJobSecurity(PressConferenceSession s)
        {
            s.options.Add(Opt("이사회와의 관계는 견고합니다. 제 자리에 대해 걱정하지 않습니다.",
                ManagerPressStyle.Confrontational, press: -1, fans: +1, board: +3));
            s.options.Add(Opt("결과로 답하겠습니다.",
                ManagerPressStyle.Reserved, press: +2, fans: +1, board: +2));
            s.options.Add(Opt("그런 질문은 부적절하다고 봅니다. 다음 질문 받겠습니다.",
                ManagerPressStyle.Confrontational, press: -3, fans: 0, board: -1));
            s.options.Add(NoCommentOpt());
        }

        private static void BuildRivalry(PressConferenceSession s)
        {
            s.options.Add(Opt("상대 팀에 대한 존중이 우선입니다. 좋은 경기를 만들겠습니다.",
                ManagerPressStyle.Diplomatic, press: +3, fans: 0, board: +1));
            s.options.Add(Opt("어떤 경기든 이기기 위해 나갈 뿐입니다.",
                ManagerPressStyle.Passionate, press: 0, fans: +2, board: +1));
            s.options.Add(Opt("그들이 우리를 이기지 못한 지 꽤 됐죠.",
                ManagerPressStyle.Confrontational, press: -2, fans: +4, board: -1));
            s.options.Add(NoCommentOpt());
        }

        private static void BuildYouth(PressConferenceSession s)
        {
            s.options.Add(Opt("재능이 있다면 나이는 중요하지 않습니다. 기회는 열려 있습니다.",
                ManagerPressStyle.Passionate, press: +1, fans: +3, board: +2));
            s.options.Add(Opt("신중하게 단계적으로 기회를 부여할 계획입니다.",
                ManagerPressStyle.Diplomatic, press: +2, fans: 0, board: +1));
            s.options.Add(Opt("현재로서는 1군 경험이 우선이 아닙니다.",
                ManagerPressStyle.Reserved, press: 0, fans: -1, board: 0));
            s.options.Add(NoCommentOpt());
        }

        private static void BuildTactics(PressConferenceSession s)
        {
            s.options.Add(Opt("구체적인 전술은 공개할 수 없습니다.",
                ManagerPressStyle.Reserved, press: -1, fans: 0, board: +2));
            s.options.Add(Opt("데이터를 기반으로 가장 효율적인 시스템을 적용하고 있습니다.",
                ManagerPressStyle.Analytical, press: +3, fans: 0, board: +1));
            s.options.Add(Opt("적극적으로 주도권을 잡는 축구를 하려 합니다.",
                ManagerPressStyle.Passionate, press: +1, fans: +2, board: 0));
            s.options.Add(NoCommentOpt());
        }

        private static void BuildNoComment(PressConferenceSession s)
        {
            s.options.Add(NoCommentOpt());
        }

        // ── 헬퍼 ──────────────────────────────────────────────────────────────

        private static PressAnswerOption Opt(string text, ManagerPressStyle tone,
            int press, int fans, int board) =>
            new PressAnswerOption
            {
                text       = text,
                tone       = tone,
                pressDelta = press,
                fanDelta   = fans,
                boardDelta = board,
            };

        private static PressAnswerOption NoCommentOpt() =>
            Opt("노코멘트하겠습니다.", ManagerPressStyle.Reserved, press: -2, fans: -1, board: 0);

    }
}
