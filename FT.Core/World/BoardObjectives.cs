using System.Collections.Generic;
using System.Linq;
using FootballTactician.Data;
using FootballTactician.Simulation;

namespace FootballTactician.World
{
    /// <summary>
    /// 이사회가 내리는 시즌 목표를 만들고 거른다.
    ///
    /// 부임할 때와 시즌이 넘어갈 때, 같은 일을 같은 순서로 해야 한다 —
    /// 전에는 두 곳에 같은 다섯 줄이 따로 있었고, 거르는 조건을 한쪽만 고치면
    /// "부임 때는 없던 컵 우승 목표가 다음 시즌에 생기는" 식으로 어긋났다.
    ///
    /// 거르는 이유: 참가하지도 않는 대회의 우승을 목표로 받으면 시작부터 실패가 확정된다.
    /// </summary>
    public static class BoardObjectives
    {
        /// <summary>그 구단이 이번 시즌 받을 목표. 달성 불가능한 것은 빼고 준다.</summary>
        public static List<BoardObjective> GenerateFor(GameWorld w, int clubId)
        {
            var club = w.Club(clubId);
            var league = w.LeagueOf(clubId);

            int size = league?.numberOfTeams ?? WorldGenerator.ClubsPerLeague;

            return BoardSystem.GenerateSeasonObjectives(
                    club, (int)(league?.tier ?? LeagueTier.First), size,
                    league?.currentSeason?.endDate ?? w.Date)
                .Select(o => Reword(o, league, size))
                .Where(o => IsAchievable(w, clubId, o.type))
                .ToList();
        }

        /// <summary>
        /// 내려갈 곳이 없는 리그의 "잔류" 목표를 순위 목표로 바꾼다.
        ///
        /// 미국·호주·인도처럼 승강이 없는 나라, 그리고 피라미드 맨 아래가 그렇다.
        /// 그냥 빼 버리면 평판 1성 구단은 <b>리그 목표를 하나도 받지 않는다</b> —
        /// 이사회가 리그에 아무 기대도 하지 않는 셈이 되므로, 느슨한 순위로 바꿔 준다.
        /// </summary>
        static BoardObjective Reword(BoardObjective o, LeagueData league, int size)
        {
            if (o.type != BoardObjectiveType.AvoidRelegation) return o;
            if ((league?.relegationSpots ?? 0) > 0) return o;

            int target = System.Math.Max(2, size - 3);
            o.type = BoardObjectiveType.LeagueFinish;
            o.targetValue = target;
            o.description = $"리그 {target}위 이내 마무리";
            o.weight = 4;
            return o;
        }

        /// <summary>
        /// 그 대회가 실제로 돌아가는지. 컵과 대륙 대회는 아직 없다.
        /// 없는 대회를 목표로 주면 시즌 말에 전부 실패로 처리되어, 상위 구단을 맡으면 무조건 경질된다.
        /// 대회를 구현할 때 여기에서 풀어 준다.
        /// </summary>
        /// <summary>
        /// 그 구단이 실제로 노릴 수 있는 목표인지.
        ///
        /// 평판만 보고 대륙 목표를 주면, 출전권이 없는 구단이 손쓸 수 없는 이유로 실패한다.
        /// 대회가 열리는지와 그 구단이 나가는지를 함께 본다.
        /// </summary>
        public static bool IsAchievable(GameWorld w, int clubId, BoardObjectiveType type) => type switch
        {
            BoardObjectiveType.WinDomesticCup
                => CupCompetition.CupFor(w, clubId)?.entrants.Contains(clubId) == true,

            BoardObjectiveType.WinContinentalCup or BoardObjectiveType.ReachContinentalKnockout
                => CupCompetition.ContinentalCup(w)?.entrants.Contains(clubId) == true,

            // 잔류는 내려갈 곳이 있을 때만 뜻이 있다. 없으면 Reword 가 순위 목표로
            // 바꿔 주므로 여기까지 오지 않지만, 혹시 남으면 주지 않는다.
            BoardObjectiveType.AvoidRelegation
                => (w.LeagueOf(clubId)?.relegationSpots ?? 0) > 0,

            _ => true,
        };
    }
}
