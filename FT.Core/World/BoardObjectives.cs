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

            return BoardSystem.GenerateSeasonObjectives(
                    club, (int)(league?.tier ?? LeagueTier.First),
                    league?.numberOfTeams ?? WorldGenerator.ClubsPerLeague,
                    league?.currentSeason?.endDate ?? w.Date)
                .Where(o => IsAchievable(w, clubId, o.type))
                .ToList();
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

            _ => true,
        };
    }
}
