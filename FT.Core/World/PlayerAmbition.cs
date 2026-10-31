using System;
using System.Collections.Generic;
using System.Linq;
using FootballTactician.Balance;
using FootballTactician.Data;

namespace FootballTactician.World
{
    /// <summary>
    /// 선수 개인 목표.
    ///
    /// 선수마다 이번 시즌에 바라는 것이 있다. 유망주는 출전을, 주전 공격수는 득점을,
    /// 베테랑은 우승을 바란다. 들어주지 못하면 불만이 쌓이고 끝내 이적을 요청한다.
    ///
    /// 목표는 <b>저장하지 않는다.</b> 선수의 처지(나이·실력·자리)에서 그때그때 나오므로
    /// 세이브에 넣으면 선수 4,000명분이 늘어날 뿐 같은 값이 다시 계산된다.
    /// </summary>
    public static class PlayerAmbition
    {
        public enum Kind
        {
            Appearances,    // 출전 경기
            Goals,          // 득점
            Assists,        // 도움
            StarterSpot,    // 주전 자리
            Silverware,     // 우승
            CallUp,         // 국가대표 발탁
        }

        /// <summary>목표 하나. 진행도까지 함께 들고 다닌다.</summary>
        public readonly record struct Goal(Kind Kind, int Target, int Progress, string Text)
        {
            public bool Met => Progress >= Target;
            public float Ratio => Target <= 0 ? 1f : Math.Clamp(Progress / (float)Target, 0f, 1f);
        }

        /// <summary>그 선수가 이번 시즌 바라는 것들.</summary>
        public static List<Goal> For(GameWorld w, PlayerData p)
        {
            var list = new List<Goal>();
            if (p == null || p.clubId <= 0) return list;

            var club = w.Club(p.clubId);
            if (club == null) return list;

            int age = w.Age(p);
            float ca = GameWorld.Ca(p);
            var squad = w.Squad(club.id).ToList();
            int rank = squad.Count(x => GameWorld.Ca(x) > ca) + 1;

            // 주전 자리를 요구할 만한지는 <b>선발 11인의 끝자락</b>과 대 봐야 한다.
            // 팀 최고 선수와 비교하면 아무도 요구하지 못해 이 목표가 영영 나오지 않는다.
            var ordered = squad.Select(GameWorld.Ca).OrderByDescending(x => x).ToList();
            float xiCutoff = ordered.Count >= 11 ? ordered[10] : (ordered.Count > 0 ? ordered[^1] : ca);

            var stats = p.seasonStats ?? new PlayerStats();
            int played = w.MyLeague?.currentSeason?.standings
                .FirstOrDefault(e => e.clubId == club.id)?.played ?? 0;

            // 출전 — 누구에게나 있다. 바라는 양은 처지에 따라 다르다.
            int wantGames = age <= 21
                ? BalanceConfig.YouthAppearanceGoal
                : rank <= 11 ? BalanceConfig.StarterAppearanceGoal
                             : BalanceConfig.SquadAppearanceGoal;

            list.Add(new Goal(Kind.Appearances, wantGames, stats.appearances,
                $"{wantGames}경기 이상 뛰기"));

            // 주전 — 실력이 받쳐 주는데 못 뛰면 가장 큰 불만이 된다.
            //
            // CA 순위로 보면 안 된다. 자리가 비어 실제로는 매주 나가는 선수가
            // "주전이 못 됐다"고 불만을 품는다. 실제 선발 명단을 봐야 한다.
            var tac = club.savedTactics?.FirstOrDefault(t => t.id == club.activeTacticsId);
            bool inXi = tac?.slots.Any(s => s.assignedPlayerId == p.id) == true;

            // 오늘의 명단만 보면 안 된다. 로테이션이나 피로로 한 번 빠진 선수까지
            // "주전이 못 됐다"고 불만을 품는다. 그동안 얼마나 뛰었는지도 함께 본다.
            bool playedEnough = played > 0
                && stats.appearances >= played * BalanceConfig.StarterPlayedShare;

            if (ca >= xiCutoff - BalanceConfig.StarterClaimGap && !inXi && !playedEnough)
                list.Add(new Goal(Kind.StarterSpot, 1, 0, "주전 자리 꿰차기"));

            // 득점·도움 — 공격 자원에게만.
            if ((p.positionFlags & (Position.ST | Position.CF | Position.LW | Position.RW)) != 0)
            {
                int wantGoals = Math.Max(4, (int)(ca / BalanceConfig.GoalGoalDivisor));
                list.Add(new Goal(Kind.Goals, wantGoals, stats.goals, $"{wantGoals}골 넣기"));
            }
            else if ((p.positionFlags & (Position.CAM | Position.LM | Position.RM | Position.CM)) != 0)
            {
                int wantAssists = Math.Max(3, (int)(ca / BalanceConfig.AssistGoalDivisor));
                list.Add(new Goal(Kind.Assists, wantAssists, stats.assists, $"도움 {wantAssists}개"));
            }

            // 성적 — 큰 구단의 좋은 선수만 기대한다. 약팀에서 우승을 바라면 늘 실패다.
            // 5성이 아니면 우승까지는 바라지 않는다 — 손 쓸 수 없는 불만은 분위기를 설명하지 못한다.
            if (club.reputation >= 4 && rank <= 14 && played > 0)
            {
                int pos = Standings.PositionOf(w.LeagueOf(club.id), club.id);
                int want = club.reputation >= 5 ? 1 : BalanceConfig.BigClubTargetPosition;
                list.Add(new Goal(Kind.Silverware, 1, pos > 0 && pos <= want ? 1 : 0,
                    want == 1 ? "리그 우승" : $"리그 {want}위 이내"));
            }

            // 국가대표 — 될 만한 실력이면 바란다.
            if (NationalTeam.IsCandidate(w, p))
                list.Add(new Goal(Kind.CallUp, 1, p.caps > 0 ? 1 : 0, "국가대표 발탁"));

            return list;
        }

        /// <summary>
        /// 못 이룬 목표의 무게. 탈의실 불만에 더해진다.
        /// 시즌 중에는 아직 이룰 시간이 남았으므로 진행도에 비례해 깎는다.
        /// </summary>
        public static float Frustration(GameWorld w, PlayerData p, float seasonProgress)
        {
            float sum = 0f;

            foreach (var g in For(w, p))
            {
                if (g.Met) continue;

                // 시즌 초에는 못 이룬 것이 당연하다. 끝으로 갈수록 무거워진다.
                float shortfall = 1f - g.Ratio;
                sum += Weight(g.Kind) * shortfall * seasonProgress;
            }

            return sum;
        }

        static float Weight(Kind kind) => kind switch
        {
            Kind.StarterSpot => BalanceConfig.StarterGoalWeight,
            Kind.Appearances => BalanceConfig.AppearanceGoalWeight,
            Kind.Goals or Kind.Assists => BalanceConfig.OutputGoalWeight,
            Kind.Silverware => BalanceConfig.SilverwareGoalWeight,
            Kind.CallUp => BalanceConfig.CallUpGoalWeight,
            _ => 1f,
        };

        /// <summary>시즌이 얼마나 지났는지. 0 = 개막, 1 = 종료.</summary>
        public static float SeasonProgress(GameWorld w)
        {
            var season = w.MyLeague?.currentSeason;
            if (season == null || season.totalMatchdays <= 0) return 0f;

            int played = season.standings.Count == 0 ? 0 : season.standings.Max(e => e.played);
            return Math.Clamp(played / (float)season.totalMatchdays, 0f, 1f);
        }
    }
}
