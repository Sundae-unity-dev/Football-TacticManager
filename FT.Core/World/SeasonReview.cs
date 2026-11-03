using System;
using System.Collections.Generic;
using System.Linq;
using FootballTactician.Balance;
using FootballTactician.Data;
using FootballTactician.Simulation;

namespace FootballTactician.World
{
    /// <summary>
    /// 이사회 목표 평가와 시즌 결산.
    ///
    /// 매주 한 번 "시즌이 끝났는가"를 보고, 끝났으면 목표를 결론 내고 감독의 평판과
    /// 빈자리를 갱신한다. 주간 진행이 부르고, 결과는 받은함에 남는다.
    ///
    /// 진행 엔진 안에 있던 부분이다. 엔진은 라운드를 돌리는 일만 하고,
    /// "그래서 어떻게 평가되는가"는 여기 모아 둔다 — 컵 결과를 봐야 하므로
    /// 컵 조회와 붙어 있어야 하는데, 엔진 안에 두면 엔진이 그 조회까지 들고 있게 된다.
    /// </summary>
    public static class SeasonReview
    {
        public static void EvaluateObjectives(GameWorld w)
        {
            var league = w.MyLeague;
            if (league == null || w.MyClub == null) return;
            int pos = Standings.PositionOf(league, w.ManagedClubId);
            foreach (var o in w.Objectives)
                BoardSystem.EvaluateObjective(o, w.MyClub, pos, w.Date);
        }

        internal static void CheckSeasonEnd(GameWorld w)
        {
            if (w.ManagedClubId <= 0 || !w.SeasonFinished) return;
            if (w.Inbox.Any(i => i.kind == InboxKind.Board && i.title.StartsWith("시즌 결산"))) return;

            int pos = Standings.PositionOf(w.MyLeague, w.ManagedClubId);
            var cup = CupCompetition.UserCup(w);
            // 대회가 대륙마다 하나씩이므로 "아무 대륙 대회"가 아니라 내 것을 봐야 한다.
            var continental = CupCompetition.ContinentalCupFor(w, w.ManagedClubId);

            foreach (var o in w.Objectives)
            {
                // 컵은 외부 시스템이 결론을 내야 한다. EvaluateObjective 는 마감일이 지나면
                // 무조건 실패로 처리하므로, 결과가 났으면 먼저 표시해 둔다.
                if (o.type == BoardObjectiveType.WinDomesticCup && cup != null
                    && cup.winnerClubId == w.ManagedClubId)
                    o.result = BoardObjectiveResult.Achieved;

                if (o.type == BoardObjectiveType.WinContinentalCup
                    && continental?.winnerClubId == w.ManagedClubId)
                    o.result = BoardObjectiveResult.Achieved;

                // 본선 진출은 참가해서 예선을 넘긴 것으로 본다.
                if (o.type == BoardObjectiveType.ReachContinentalKnockout && continental != null
                    && continental.entrants.Contains(w.ManagedClubId))
                    o.result = CupCompetition.ContinentalRoundsWon(continental, w.ManagedClubId) >= 1
                        ? BoardObjectiveResult.Achieved
                        : BoardObjectiveResult.Failed;

                // 평가는 마감일이 지나야 결론이 난다. 마지막 경기 날짜는 마감일보다 보름쯤 이르므로
                // 현재 날짜로 부르면 전부 미정으로 남는다. 일정이 끝난 시점이 곧 시즌 종료다.
                BoardSystem.EvaluateObjective(o, w.MyClub, pos, o.deadline);
                if (o.result == BoardObjectiveResult.Pending)
                    o.result = BoardObjectiveResult.Failed;
                BoardSystem.ResolveObjective(w.Manager, o, o.result);
            }

            // 감독 평판을 갱신하고 다음 시즌 빈자리를 굴린다.
            int repBefore = w.ManagerReputation;
            ManagerCareer.UpdateReputation(w, pos, w.MyLeague!.clubIds.Count,
                cup != null && cup.winnerClubId == w.ManagedClubId);
            ManagerCareer.RollVacancies(w, new Random(SeedMix.Of(w.Seed, w.SeasonYear, 4242)));

            string summary = string.Join(" · ", w.Objectives.Select(o =>
                $"{o.description}: {Label(o.result)}"));
            w.Post("시즌 결산",
                   $"리그 {pos}위. {summary} · 감독 평판 {repBefore} → {w.ManagerReputation}",
                   InboxKind.Board);

            if (w.Manager.boardConfidence < BalanceConfig.SackThreshold)
            {
                w.Sacked = true;
                w.SackReason = "시즌 목표를 채우지 못했습니다.";
            }
        }

        static string Label(BoardObjectiveResult r) => r switch
        {
            BoardObjectiveResult.Achieved => "달성",
            BoardObjectiveResult.Failed => "실패",
            BoardObjectiveResult.PartiallyMet => "부분 달성",
            _ => "진행 중",
        };

    }
}
