using System;
using System.Collections.Generic;
using FootballTactician.Data;

namespace FootballTactician.Simulation
{
    /// <summary>
    /// 이사회 신뢰도 및 시즌 목표 관리.
    /// boardConfidence는 0~100 정수로 ManagerProfile에 저장되며,
    /// BoardConfidenceLevel은 그 값에서 파생된다.
    /// Unity 의존성 없음 — 단위 테스트 가능.
    /// </summary>
    public static class BoardSystem
    {
        // ── 신뢰도 레벨 변환 ──────────────────────────────────────────────────

        public static BoardConfidenceLevel ConfidenceLevel(int boardConfidence)
        {
            if (boardConfidence >= 80) return BoardConfidenceLevel.FullSupport;
            if (boardConfidence >= 60) return BoardConfidenceLevel.BackingManager;
            if (boardConfidence >= 40) return BoardConfidenceLevel.Concerned;
            if (boardConfidence >= 20) return BoardConfidenceLevel.UnderPressure;
            return BoardConfidenceLevel.Sacked;
        }

        // ── 매치 결과 반영 ────────────────────────────────────────────────────

        public static void ApplyMatchResult(ManagerProfile mp, BoardMatchOutcome o)
        {
            if (mp == null) return;

            int delta;
            int repDiff = o.opponentReputation - o.ourClubReputation;  // 양수 = 강팀 상대

            if (o.wasWin)
            {
                // 약팀 상대 승은 당연, 강팀 상대 승은 큰 보너스
                delta = repDiff >= 1 ? +3 : repDiff <= -1 ? +1 : +2;
                if (!o.wasHome) delta += 1;       // 원정 승 추가 보너스
            }
            else if (o.wasDraw)
            {
                // 약팀 상대 무승부는 페널티, 강팀 상대는 +1
                delta = repDiff >= 1 ? +1 : repDiff <= -1 ? -1 : 0;
            }
            else
            {
                // 약팀 상대 패배는 큰 타격
                delta = repDiff >= 1 ? -1 : repDiff <= -1 ? -3 : -2;
            }

            mp.boardConfidence = SimMath.Clamp(mp.boardConfidence + delta, 0, 100);
        }

        // ── 시즌 목표 생성 ────────────────────────────────────────────────────

        public static List<BoardObjective> GenerateSeasonObjectives(
            ClubData club, int leagueTier, int leagueSize, GameDate seasonEnd)
        {
            var list = new List<BoardObjective>();
            int rep = Math.Max(1, Math.Min(5, club.reputation));

            // 리그 순위 목표 — 평판 기준
            int targetPos = LeagueTargetForReputation(rep, leagueSize);
            if (rep <= 1)
            {
                list.Add(new BoardObjective
                {
                    type        = BoardObjectiveType.AvoidRelegation,
                    targetValue = leagueSize - 2,             // 강등권 시작
                    deadline    = seasonEnd,
                    weight      = 5,
                    description = $"리그 잔류 (최소 {leagueSize - 3}위 이상)",
                });
            }
            else
            {
                list.Add(new BoardObjective
                {
                    type        = BoardObjectiveType.LeagueFinish,
                    targetValue = targetPos,
                    deadline    = seasonEnd,
                    weight      = rep >= 4 ? 5 : 4,
                    description = $"리그 {targetPos}위 이내 마무리",
                });
            }

            // 컵 목표 — 평판 4 이상은 우승, 그 외는 라운드 진출
            if (rep >= 4)
            {
                list.Add(new BoardObjective
                {
                    type        = BoardObjectiveType.WinDomesticCup,
                    targetValue = -1,
                    deadline    = seasonEnd,
                    weight      = 3,
                    description = "국내 컵 우승",
                });
            }

            // 컨티넨탈 목표 (1부 + 평판 4↑)
            if (leagueTier == 1 && rep >= 4)
            {
                list.Add(new BoardObjective
                {
                    type        = rep == 5 ? BoardObjectiveType.WinContinentalCup
                                           : BoardObjectiveType.ReachContinentalKnockout,
                    targetValue = -1,
                    deadline    = seasonEnd,
                    weight      = rep == 5 ? 4 : 3,
                    description = rep == 5 ? "대륙 대회 우승" : "대륙 대회 토너먼트 진출",
                });
            }

            // 유스 출전 목표 — 모든 클럽 공통
            int youthMinutes = rep <= 2 ? 2500 : rep == 3 ? 1500 : 800;
            list.Add(new BoardObjective
            {
                type        = BoardObjectiveType.DevelopYouthMinutes,
                targetValue = youthMinutes,
                deadline    = seasonEnd,
                weight      = 2,
                description = $"U21 선수 누적 {youthMinutes}분 출전",
            });

            // 재정 목표 — 모든 클럽 공통
            list.Add(new BoardObjective
            {
                type        = BoardObjectiveType.StayWithinBudget,
                targetValue = 0,
                deadline    = seasonEnd,
                weight      = 3,
                description = "시즌 말 재정 흑자 유지",
            });

            return list;
        }

        // ── 진행도 평가 (시즌 중 주기적 호출) ─────────────────────────────────

        public static void EvaluateObjective(
            BoardObjective o, ClubData club,
            int currentLeaguePosition, GameDate now)
        {
            if (o.result != BoardObjectiveResult.Pending) return;

            switch (o.type)
            {
                case BoardObjectiveType.LeagueFinish:
                case BoardObjectiveType.ImproveOnLastSeason:
                    o.progressValue = currentLeaguePosition;
                    if (now >= o.deadline)
                        o.result = currentLeaguePosition <= o.targetValue
                            ? BoardObjectiveResult.Achieved
                            : currentLeaguePosition <= o.targetValue + 2
                                ? BoardObjectiveResult.PartiallyMet
                                : BoardObjectiveResult.Failed;
                    break;

                case BoardObjectiveType.AvoidRelegation:
                    o.progressValue = currentLeaguePosition;
                    if (now >= o.deadline)
                        o.result = currentLeaguePosition < o.targetValue
                            ? BoardObjectiveResult.Achieved
                            : BoardObjectiveResult.Failed;
                    break;

                case BoardObjectiveType.StayWithinBudget:
                    o.progressValue = (int)(club.finances?.balance ?? 0);
                    if (now >= o.deadline)
                        o.result = (club.finances?.balance ?? 0) >= 0
                            ? BoardObjectiveResult.Achieved
                            : BoardObjectiveResult.Failed;
                    break;

                case BoardObjectiveType.DevelopYouthMinutes:
                    // 진행도는 외부에서 누적해서 갱신 (BoardSystem.AddYouthMinutes)
                    if (now >= o.deadline)
                        o.result = o.progressValue >= o.targetValue
                            ? BoardObjectiveResult.Achieved
                            : o.progressValue >= o.targetValue * 0.6f
                                ? BoardObjectiveResult.PartiallyMet
                                : BoardObjectiveResult.Failed;
                    break;

                case BoardObjectiveType.WinDomesticCup:
                case BoardObjectiveType.WinContinentalCup:
                case BoardObjectiveType.ReachContinentalKnockout:
                    // 트로피/라운드 진출은 외부 컵 시스템이 ResolveObjective 호출
                    if (now >= o.deadline && o.result == BoardObjectiveResult.Pending)
                        o.result = BoardObjectiveResult.Failed;
                    break;
            }
        }

        public static void AddYouthMinutes(BoardObjective o, int minutes)
        {
            if (o.type != BoardObjectiveType.DevelopYouthMinutes) return;
            o.progressValue += minutes;
        }

        // ── 목표 완료 처리 → 신뢰도 변동 ──────────────────────────────────────

        public static void ResolveObjective(
            ManagerProfile mp, BoardObjective o, BoardObjectiveResult result)
        {
            if (mp == null) return;
            o.result = result;

            int baseDelta = result switch
            {
                BoardObjectiveResult.Achieved     => +6,
                BoardObjectiveResult.PartiallyMet => +1,
                BoardObjectiveResult.Failed       => -8,
                _                                 =>  0,
            };

            int delta = baseDelta * Math.Max(1, o.weight);
            mp.boardConfidence = SimMath.Clamp(mp.boardConfidence + delta, 0, 100);
        }

        // ── 헬퍼 ──────────────────────────────────────────────────────────────

        private static int LeagueTargetForReputation(int reputation, int leagueSize)
        {
            // 평판 5 = 우승권, 4 = 챔피언스리그권(상위 4),
            // 3 = 중상위, 2 = 하위 안전권, 1 = 잔류 (상기 분기에서 처리)
            return reputation switch
            {
                5 => 1,
                4 => Math.Max(2, leagueSize / 5),       // 약 상위 20%
                3 => Math.Max(6, leagueSize / 2),       // 중위권
                2 => leagueSize - 5,                    // 강등권 위
                _ => leagueSize - 3,
            };
        }

    }
}
