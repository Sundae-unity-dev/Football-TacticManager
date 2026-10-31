using System;
using System.Collections.Generic;
using System.Globalization;
using FootballTactician.Data;

namespace FootballTactician.Simulation
{
    public class CommentaryContext
    {
        public int               homeClubId;
        public int               awayClubId;
        public string            homeClubName;
        public string            awayClubName;
        public Func<int, string> getPlayerName;     // 미존재/null 시 "선수"로 폴백
        public int               seed = -1;         // -1 = 무작위
    }

    /// <summary>
    /// 경기 결과의 MatchEvent 목록을 한국어 해설 라인으로 변환.
    /// Unity 의존성 없음 — 단위 테스트 가능.
    /// 템플릿은 위치형 자리표시자({0},{1},…)를 쓴다 — 라인당 string.Format 1회로 할당 최소화.
    /// </summary>
    public static class CommentarySystem
    {
        private static readonly CultureInfo _ci = CultureInfo.InvariantCulture;

        // ── 진입점 ────────────────────────────────────────────────────────────

        public static List<CommentaryLine> Generate(
            MatchResultData result,
            CommentaryContext ctx)
        {
            var rng   = ctx.seed >= 0 ? new Random(ctx.seed) : new Random();
            var lines = new List<CommentaryLine>(result.events.Count + 4);

            lines.Add(BuildKickoff(ctx, rng));

            int homeGoals = 0, awayGoals = 0;
            bool halfTimeInserted = false;

            var sorted = new List<MatchEvent>(result.events);
            sorted.Sort((a, b) => a.minute.CompareTo(b.minute));

            foreach (var e in sorted)
            {
                if (!halfTimeInserted && e.minute > 45)
                {
                    lines.Add(BuildHalfTime(ctx, homeGoals, awayGoals, rng));
                    halfTimeInserted = true;
                }

                bool isHomeAttack = e.clubId == ctx.homeClubId;
                if (e.eventType == MatchEventType.Goal)
                {
                    if (isHomeAttack) homeGoals++; else awayGoals++;
                }
                else if (e.eventType == MatchEventType.OwnGoal)
                {
                    if (isHomeAttack) awayGoals++; else homeGoals++;
                }

                var line = BuildEventLine(e, ctx, homeGoals, awayGoals, rng);
                if (line != null) lines.Add(line);
            }

            if (!halfTimeInserted)
                lines.Add(BuildHalfTime(ctx, homeGoals, awayGoals, rng));

            // sorted는 minute 오름차순 — 마지막 원소가 최대값. 별도 순회 불필요.
            int ftMinute = sorted.Count > 0 && sorted[sorted.Count - 1].minute > 90
                ? sorted[sorted.Count - 1].minute
                : 90;
            lines.Add(BuildFullTime(ctx, result.homeGoals, result.awayGoals, ftMinute + 1, rng));

            return lines;
        }

        // ── 빌더 ──────────────────────────────────────────────────────────────

        private static CommentaryLine BuildKickoff(CommentaryContext ctx, Random rng)
        {
            string text = string.Format(_ci, Pick(_kickoff, rng),
                ctx.homeClubName, ctx.awayClubName);
            return MakeLine(0, CommentaryEventType.MatchStart, CommentaryTone.Neutral, text, false);
        }

        private static CommentaryLine BuildHalfTime(
            CommentaryContext ctx, int hg, int ag, Random rng)
        {
            string text = string.Format(_ci, Pick(_halfTime, rng),
                ctx.homeClubName, $"{hg}-{ag}", ctx.awayClubName);
            return MakeLine(45, CommentaryEventType.HalfTimeReport, CommentaryTone.Neutral, text, false);
        }

        private static CommentaryLine BuildFullTime(
            CommentaryContext ctx, int hg, int ag, int minute, Random rng)
        {
            string template;
            CommentaryTone tone;
            string score = $"{hg}-{ag}";
            string winner = hg > ag ? ctx.homeClubName : ag > hg ? ctx.awayClubName : "";

            string text;
            if (hg == ag)
            {
                template = Pick(_fullTimeDraw, rng);
                tone     = CommentaryTone.Neutral;
                text     = string.Format(_ci, template, ctx.homeClubName, score, ctx.awayClubName);
            }
            else if (Math.Abs(hg - ag) >= 3)
            {
                template = Pick(_fullTimeBlowout, rng);
                tone     = CommentaryTone.Excited;
                text     = string.Format(_ci, template, winner, score);
            }
            else
            {
                template = Pick(_fullTimeClose, rng);
                tone     = CommentaryTone.Excited;
                text     = string.Format(_ci, template, winner, score, ctx.homeClubName, ctx.awayClubName);
            }

            return MakeLine(minute, CommentaryEventType.FullTimeResult, tone, text, true);
        }

        private static CommentaryLine BuildEventLine(
            MatchEvent e, CommentaryContext ctx,
            int homeGoals, int awayGoals, Random rng)
        {
            bool   isHome   = e.clubId == ctx.homeClubId;
            string attClub  = isHome ? ctx.homeClubName : ctx.awayClubName;
            string defClub  = isHome ? ctx.awayClubName : ctx.homeClubName;
            string actor    = ResolveName(ctx, e.playerId);
            string assister = e.assistPlayerId.HasValue ? ResolveName(ctx, e.assistPlayerId.Value) : null;

            switch (e.eventType)
            {
                case MatchEventType.Goal:
                {
                    string template = Pick(assister != null ? _goalWithAssist : _goalSolo, rng);
                    string score    = $"{homeGoals}-{awayGoals}";
                    string text     = assister != null
                        ? string.Format(_ci, template, e.minute, actor, assister, attClub, score)
                        : string.Format(_ci, template, e.minute, actor, attClub, score);
                    return MakeLine(e.minute, CommentaryEventType.GoalScored,
                        DetermineGoalTone(e.minute, homeGoals, awayGoals), text, true);
                }

                case MatchEventType.OwnGoal:
                {
                    string text = string.Format(_ci, Pick(_ownGoal, rng), e.minute, actor, defClub);
                    return MakeLine(e.minute, CommentaryEventType.OwnGoal,
                        CommentaryTone.Disappointed, text, true);
                }

                case MatchEventType.BigSave:
                {
                    string text = string.Format(_ci, Pick(_bigSave, rng), e.minute, actor, attClub);
                    return MakeLine(e.minute, CommentaryEventType.BigSave,
                        CommentaryTone.Tense, text, false);
                }

                case MatchEventType.PostHit:
                {
                    string text = string.Format(_ci, Pick(_postHit, rng), e.minute, attClub);
                    return MakeLine(e.minute, CommentaryEventType.PostHit,
                        CommentaryTone.Tense, text, false);
                }

                case MatchEventType.YellowCard:
                {
                    string text = string.Format(_ci, Pick(_yellow, rng), e.minute, actor, attClub);
                    return MakeLine(e.minute, CommentaryEventType.YellowCard,
                        CommentaryTone.Neutral, text, false);
                }

                case MatchEventType.SecondYellow:
                case MatchEventType.RedCard:
                {
                    string text = string.Format(_ci, Pick(_red, rng), e.minute, actor, attClub);
                    return MakeLine(e.minute, CommentaryEventType.RedCard,
                        CommentaryTone.Tense, text, true);
                }

                case MatchEventType.PenaltyScored:
                {
                    string text = string.Format(_ci, Pick(_penaltyScored, rng), e.minute, actor, attClub);
                    return MakeLine(e.minute, CommentaryEventType.PenaltyScored,
                        CommentaryTone.Excited, text, true);
                }

                case MatchEventType.PenaltyMissed:
                case MatchEventType.PenaltySaved:
                {
                    string text = string.Format(_ci, Pick(_penaltyMissed, rng), e.minute, actor, attClub);
                    return MakeLine(e.minute, CommentaryEventType.PenaltyMissed,
                        CommentaryTone.Disappointed, text, true);
                }

                case MatchEventType.Substitution:
                {
                    string outName = actor;
                    string inName  = assister ?? "교체 선수";
                    string text = string.Format(_ci, Pick(_substitution, rng),
                        e.minute, outName, inName, attClub);
                    return MakeLine(e.minute, CommentaryEventType.Substitution,
                        CommentaryTone.Neutral, text, false);
                }

                default:
                    return null;
            }
        }

        // ── 톤 결정 ───────────────────────────────────────────────────────────

        private static CommentaryTone DetermineGoalTone(int minute, int hg, int ag)
        {
            int diff = Math.Abs(hg - ag);
            if (minute >= 80 && diff <= 1) return CommentaryTone.Excited;
            if (diff >= 3)                 return CommentaryTone.Neutral;
            return CommentaryTone.Excited;
        }

        // ── 유틸 ──────────────────────────────────────────────────────────────

        private static CommentaryLine MakeLine(int minute, CommentaryEventType type,
            CommentaryTone tone, string text, bool isHighlight) =>
            new CommentaryLine
            {
                minute      = minute,
                eventType   = type,
                tone        = tone,
                text        = text,
                isHighlight = isHighlight,
            };

        private static string ResolveName(CommentaryContext ctx, int playerId)
        {
            if (ctx.getPlayerName == null || playerId < 0) return "선수";
            string name = ctx.getPlayerName(playerId);
            return string.IsNullOrEmpty(name) ? "선수" : name;
        }

        private static string Pick(string[] arr, Random rng) => arr[rng.Next(arr.Length)];

        // ── 한국어 템플릿 카탈로그 ────────────────────────────────────────────
        // 자리표시자 규약 (각 카테고리별 고정):
        //   kickoff:        {0}=home {1}=away
        //   goalSolo:       {0}=minute {1}=scorer {2}=club {3}=score
        //   goalWithAssist: {0}=minute {1}=scorer {2}=assister {3}=club {4}=score
        //   ownGoal/yellow/red/bigSave/penalty*: {0}=minute {1}=player {2}=club
        //   postHit:        {0}=minute {1}=club
        //   substitution:   {0}=minute {1}=out {2}=in {3}=club
        //   halfTime/fullTimeDraw: {0}=home {1}=score {2}=away
        //   fullTimeClose:  {0}=winner {1}=score {2}=home {3}=away
        //   fullTimeBlowout:{0}=winner {1}=score

        private static readonly string[] _kickoff =
        {
            "킥오프! {0}와 {1}의 경기가 시작됩니다.",
            "주심의 휘슬과 함께 경기 시작! {0} vs {1}.",
            "오늘의 매치, {0}와 {1}의 90분 승부가 막을 올립니다.",
        };

        private static readonly string[] _goalSolo =
        {
            "{0}분, {1}의 단독 골! {2} 득점, 스코어 {3}.",
            "골! {0}분 {1}이 결정적인 한 방을 꽂아넣습니다. ({3})",
            "{0}분 {1}, 직접 마무리하며 {2}이 앞서갑니다. ({3})",
        };

        private static readonly string[] _goalWithAssist =
        {
            "{0}분, {2}의 어시스트를 받은 {1}의 마무리! {3} 득점, {4}.",
            "골! {0}분 {2}의 패스를 {1}이 깔끔하게 꽂아넣습니다. ({4})",
            "{0}분 {1}의 골! {2}의 절묘한 어시스트가 빛났습니다. {3} {4}.",
        };

        private static readonly string[] _ownGoal =
        {
            "{0}분, {1}의 자책골! {2}에게 뼈아픈 실수가 나왔습니다.",
            "이런! {0}분 {1}이 자책골을 기록하고 맙니다.",
        };

        private static readonly string[] _bigSave =
        {
            "{0}분, {1}의 환상적인 선방! {2}이 위기를 넘깁니다.",
            "엄청난 세이브! {0}분 {1}이 골문을 굳게 지킵니다.",
            "{0}분 {1}, 결정적인 슈팅을 막아냅니다.",
        };

        private static readonly string[] _postHit =
        {
            "{0}분, 골대! {1}의 슈팅이 골대를 강타합니다.",
            "아쉽다! {0}분 슈팅이 포스트를 맞고 튕겨 나옵니다.",
        };

        private static readonly string[] _yellow =
        {
            "{0}분, {1}에게 옐로카드가 주어집니다.",
            "{0}분 {1}, 거친 태클로 경고를 받습니다.",
        };

        private static readonly string[] _red =
        {
            "{0}분, {1} 퇴장! {2}이 수적 열세에 놓입니다.",
            "레드카드! {0}분 {1}이 그라운드를 떠납니다.",
        };

        private static readonly string[] _penaltyScored =
        {
            "{0}분, {1}의 페널티킥 성공! {2}이 추가 득점을 올립니다.",
            "골! {0}분 {1}이 침착하게 페널티킥을 성공시킵니다.",
        };

        private static readonly string[] _penaltyMissed =
        {
            "{0}분, {1}의 페널티킥 실축! 결정적인 기회를 놓칩니다.",
            "{0}분 {1}의 페널티킥이 무산되고 맙니다.",
        };

        private static readonly string[] _substitution =
        {
            "{0}분, {3} 교체. {1} 아웃, {2} 인.",
            "{0}분 교체 카드: {1} → {2} ({3}).",
        };

        private static readonly string[] _halfTime =
        {
            "전반전 종료! 현재 스코어 {0} {1} {2}.",
            "하프타임. {0} {1} {2}로 전반을 마칩니다.",
        };

        private static readonly string[] _fullTimeDraw =
        {
            "경기 종료! {0} {1} {2}, 무승부로 마무리됩니다.",
            "풀타임 휘슬. 양 팀 모두 승점 1점씩을 가져갑니다. ({1})",
        };

        private static readonly string[] _fullTimeClose =
        {
            "경기 종료! {0}이 {1}로 승리를 가져갑니다.",
            "주심의 종료 휘슬. {0}의 승리! 최종 스코어 {2} {1} {3}.",
        };

        private static readonly string[] _fullTimeBlowout =
        {
            "경기 종료! {0}의 일방적인 승리, {1}.",
            "풀타임. {0}이 경기 내내 압도하며 {1}로 승리합니다.",
        };
    }
}
