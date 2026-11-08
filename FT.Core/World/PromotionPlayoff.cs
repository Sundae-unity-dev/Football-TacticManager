using System;
using System.Collections.Generic;
using System.Linq;
using FootballTactician.Data;

namespace FootballTactician.World
{
    /// <summary>
    /// 마지막 승격 한 자리를 토너먼트로 가린다.
    ///
    /// 순위만으로 올라가는 리그가 오히려 소수다(docs/리그-제도.md ④). 실제로는 두 모양이 있다.
    ///
    /// <b>승격 플레이오프</b> — 2부 상위권이 자기들끼리 토너먼트를 치러 마지막 한 장을 가져간다.
    /// 잉글랜드는 1·2위가 자동 승격하고 3~6위가 다툰다. 이탈리아는 여섯 팀이다.
    ///
    /// <b>승강 플레이오프</b> — 내려갈 뻔한 1부 팀과 올라올 뻔한 2부 팀이 직접 맞붙는다.
    /// 독일은 1부 16위와 2부 3위가 홈·원정으로 겨룬다. 자동 자리 위에 한 쌍이 더 붙는 셈이라,
    /// 2부 팀이 이기면 그 해에만 오가는 팀이 하나 늘어난다 — 올라간 수와 내려간 수는 그래도 같다.
    ///
    /// 대진 자체는 <see cref="Knockout"/> 에 있다. 챔피언 결정 플레이오프(⑥)와 같은 기계다.
    /// </summary>
    public static class PromotionPlayoff
    {
        /// <summary>승격 대진의 씨드 열쇠와 경기 id 바닥. 바꾸면 지난 결과가 달라진다.</summary>
        static readonly Knockout.Rules Rules = new(40519, 900000, "승격");

        /// <summary>
        /// 그 경계에서 내려갈 팀과 올라갈 팀. 플레이오프가 없으면 순위 그대로다.
        ///
        /// <paramref name="log"/> 에는 사람에게 보여 줄 한 줄짜리 설명이 쌓인다.
        /// </summary>
        public static (List<int> down, List<int> up) Resolve(
            GameWorld w, NationNames spec, int tier,
            LeagueData top, LeagueData below, int spots,
            Random rng, List<string> log)
        {
            var upByPos = below.currentSeason.standings
                .OrderBy(e => e.position).Select(e => e.clubId).ToList();
            var downByPos = top.currentSeason.standings
                .OrderByDescending(e => e.position).Select(e => e.clubId).ToList();

            // 아무 것도 못 할 만큼 순위표가 모자라면 손대지 않는다.
            if (upByPos.Count < spots || downByPos.Count < spots)
                return (new List<int>(), new List<int>());

            int entrants = spec?.PlayoffTeams(tier) ?? 0;
            bool survival = spec?.HasSurvivalPlayoff(tier) ?? false;

            // ── 승격 플레이오프 (잉글랜드식) ──────────────────────────────
            if (entrants >= 2 && spots >= 1 && upByPos.Count >= spots - 1 + entrants)
            {
                int auto = spots - 1;
                var up = upByPos.Take(auto).ToList();
                var field = upByPos.Skip(auto).Take(entrants).ToList();

                log.Add($"{below.name} 승격 플레이오프 — {entrants}팀");
                up.Add(Knockout.Run(w, field, Rules, log));

                return (downByPos.Take(spots).ToList(), up);
            }

            // ── 승강 플레이오프 (독일식) ──────────────────────────────────
            if (survival && downByPos.Count > spots && upByPos.Count > spots)
            {
                var up = upByPos.Take(spots).ToList();
                var down = downByPos.Take(spots).ToList();

                int safe = downByPos[spots];        // 가까스로 살아남은 1부 팀
                int challenger = upByPos[spots];    // 자동 승격을 놓친 2부 팀

                log.Add($"승강 플레이오프 — {Knockout.Name(w, safe)}({top.shortName}) "
                        + $"대 {Knockout.Name(w, challenger)}({below.shortName})");

                // 아래 팀이 먼저 홈에서 치른다. 두 경기 합산이 같으면 위 팀이 남는다.
                var leg1 = Knockout.Leg(w, challenger, safe, Rules, 1);
                var leg2 = Knockout.Leg(w, safe, challenger, Rules, 2);
                int challengerGoals = leg1.home + leg2.away;
                int safeGoals = leg1.away + leg2.home;

                log.Add($"  1차 {Knockout.Score(leg1)} · 2차 {Knockout.Score(leg2)}"
                        + $" → 합산 {challengerGoals}-{safeGoals}");

                if (challengerGoals > safeGoals)
                {
                    log.Add($"  {Knockout.Name(w, challenger)} 이 올라갑니다.");
                    up.Add(challenger);
                    down.Add(safe);
                }
                else
                {
                    log.Add($"  {Knockout.Name(w, safe)} 이 남습니다.");
                }

                return (down, up);
            }

            // ── 순위 그대로 ───────────────────────────────────────────────
            return (downByPos.Take(spots).ToList(), upByPos.Take(spots).ToList());
        }
    }
}
