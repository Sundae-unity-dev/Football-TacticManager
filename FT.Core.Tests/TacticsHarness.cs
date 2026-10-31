using System;
using System.Collections.Generic;
using System.Linq;
using FootballTactician.Data;
using FootballTactician.Simulation;
using FootballTactician.World;

namespace FT.Core.Tests
{
    /// <summary>
    /// 전술 검사용 맞대결. 같은 전력의 두 팀을 붙여 지시만 바꾼다.
    /// 전력이 다르면 전술의 효과가 전력 차에 묻힌다.
    /// </summary>
    static class TacticsHarness
    {
        /// <summary>
        /// 같은 세계에서 전력이 가장 비슷한 두 구단.
        ///
        /// <b>1부에서만 고른다.</b> 전체에서 고르면 하위 리그의 고만고만한 약팀 둘이 뽑히는데,
        /// 능력치가 전반적으로 낮아 전술 보정의 폭도 함께 눌린다. 전술은 1부 수준에서 재야
        /// 대표성이 있다.
        /// </summary>
        public static (GameWorld w, ClubData a, ClubData b) Pair(int seed = 31415)
        {
            var w = WorldGenerator.Create(seed);
            var ranked = w.Clubs.Values
                .Where(c => c.playerIds.Count >= 16 && w.TierOf(c.id) == LeagueTier.First)
                .OrderByDescending(c => w.Strength(c.id))
                .ToList();

            // 전력 순으로 인접한 두 구단을 고른다.
            var best = (a: ranked[0], b: ranked[1], gap: float.MaxValue);
            for (int i = 0; i + 1 < ranked.Count; i++)
            {
                float gap = Math.Abs(w.Strength(ranked[i].id) - w.Strength(ranked[i + 1].id));
                if (gap < best.gap) best = (ranked[i], ranked[i + 1], gap);
            }

            return (w, best.a, best.b);
        }

        public static void Copy(TeamInstructions from, TeamInstructions to)
        {
            to.mentality = from.mentality;
            to.pressingIntensity = from.pressingIntensity;
            to.counterPress = from.counterPress;
            to.buildUpStyle = from.buildUpStyle;
            to.tempo = from.tempo;
            to.defensiveLine = from.defensiveLine;
            to.width = from.width;
            to.offsideTrap = from.offsideTrap;
            to.directCounterAttack = from.directCounterAttack;
        }

        static MatchLineup Lineup(GameWorld w, ClubData c, TeamInstructions ins)
        {
            var tac = FormationDatabase.CreateTactics("4-2-3-1", "검사");
            var squad = w.Squad(c.id).ToList();
            LineupBuilder.Assign(tac, squad);
            Copy(ins, tac.instructions);

            return new MatchLineup
            {
                clubId = c.id,
                tactics = tac,
                benchIds = LineupBuilder.Bench(tac, squad),
            };
        }

        /// <summary>a 의 지시로 b 의 지시를 상대했을 때 a 의 경기당 승점.</summary>
        public static double Duel(TeamInstructions insA, TeamInstructions insB, int matches, int seed)
        {
            var (w, ca, cb) = Pair();
            var la = Lineup(w, ca, insA);
            var lb = Lineup(w, cb, insB);
            var rng = new Random(7000 + seed);

            int pts = 0;
            for (int m = 0; m < matches; m++)
            {
                // 홈·원정을 번갈아 치러 홈 어드밴티지를 상쇄한다.
                bool aHome = m % 2 == 0;
                var ctx = new MatchSimContext
                {
                    home = aHome ? la : lb,
                    away = aHome ? lb : la,
                    weather = WeatherCondition.Cloudy,
                    pitch = PitchCondition.Good,
                    seed = rng.Next(),
                };

                var r = MatchEngine.Simulate(ctx, w.Lookup);
                int ag = aHome ? r.homeGoals : r.awayGoals;
                int bg = aHome ? r.awayGoals : r.homeGoals;
                pts += ag > bg ? 3 : ag == bg ? 1 : 0;
            }

            return (double)pts / matches;
        }

        /// <summary>멘탈리티별 (공격 배수, 수비 배수). TeamPower 로 바깥에서 잰다.</summary>
        public static (float attack, float defense) MentalityMultipliers(TeamMentality m)
        {
            var (w, c, _) = Pair();
            var baseline = MatchEngine.TeamPower(
                Lineup(w, c, new TeamInstructions { mentality = TeamMentality.Balanced }), w.Lookup);
            var tuned = MatchEngine.TeamPower(
                Lineup(w, c, new TeamInstructions { mentality = m }), w.Lookup);

            return (tuned.Item1 / baseline.Item1, tuned.Item2 / baseline.Item2);
        }
    }
}
