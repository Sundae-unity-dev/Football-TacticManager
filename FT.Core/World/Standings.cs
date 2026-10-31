using System.Collections.Generic;
using System.Linq;
using FootballTactician.Data;

namespace FootballTactician.World
{
    /// <summary>
    /// 순위표를 치른 경기로부터 다시 계산한다.
    /// 경기마다 누적하지 않고 전체를 다시 세는 쪽이, 결과를 되돌리는 기능이 붙어도 어긋나지 않는다.
    /// </summary>
    public static class Standings
    {
        public static void Recalculate(GameWorld w)
        {
            foreach (var league in w.Leagues.Values) Recalculate(league);
        }

        public static void Recalculate(LeagueData league)
        {
            var season = league.currentSeason;
            if (season == null) return;

            var table = league.clubIds.ToDictionary(
                id => id, id => new StandingEntry { clubId = id });

            foreach (var f in season.fixtures)
            {
                if (!f.isPlayed || f.result == null) continue;
                if (!table.TryGetValue(f.homeClubId, out var h)) continue;
                if (!table.TryGetValue(f.awayClubId, out var a)) continue;

                h.played++; a.played++;
                h.goalsFor += f.result.homeGoals; h.goalsAgainst += f.result.awayGoals;
                a.goalsFor += f.result.awayGoals; a.goalsAgainst += f.result.homeGoals;

                if (f.result.homeGoals > f.result.awayGoals)
                {
                    h.won++; a.lost++;
                    h.points += league.rules.pointsForWin;
                    a.points += league.rules.pointsForLoss;
                }
                else if (f.result.homeGoals < f.result.awayGoals)
                {
                    a.won++; h.lost++;
                    a.points += league.rules.pointsForWin;
                    h.points += league.rules.pointsForLoss;
                }
                else
                {
                    h.drawn++; a.drawn++;
                    h.points += league.rules.pointsForDraw;
                    a.points += league.rules.pointsForDraw;
                }
            }

            var ordered = table.Values
                .OrderByDescending(e => e.points)
                .ThenByDescending(e => e.GoalDifference)
                .ThenByDescending(e => e.goalsFor)
                .ThenBy(e => e.clubId)
                .ToList();

            for (int i = 0; i < ordered.Count; i++) ordered[i].position = i + 1;
            season.standings = ordered;
        }

        public static int PositionOf(LeagueData league, int clubId)
        {
            var e = league?.currentSeason?.standings?.FirstOrDefault(s => s.clubId == clubId);
            return e?.position ?? 0;
        }

        public static StandingEntry EntryOf(LeagueData league, int clubId)
            => league?.currentSeason?.standings?.FirstOrDefault(s => s.clubId == clubId);

        /// <summary>최근 경기 결과를 W/D/L 로 돌려준다. 최신이 마지막.</summary>
        public static List<char> RecentForm(LeagueData league, int clubId, int count = 5)
        {
            var form = new List<char>();
            if (league?.currentSeason == null) return form;

            foreach (var f in league.currentSeason.fixtures
                         .Where(f => f.isPlayed && (f.homeClubId == clubId || f.awayClubId == clubId))
                         .OrderBy(f => f.matchday)
                         .TakeLast(count))
            {
                bool home = f.homeClubId == clubId;
                int us = home ? f.result.homeGoals : f.result.awayGoals;
                int them = home ? f.result.awayGoals : f.result.homeGoals;
                form.Add(us > them ? 'W' : us == them ? 'D' : 'L');
            }
            return form;
        }
    }
}
