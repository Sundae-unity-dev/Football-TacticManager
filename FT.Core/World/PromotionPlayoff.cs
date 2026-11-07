using System;
using System.Collections.Generic;
using System.Linq;
using FootballTactician.Data;
using FootballTactician.Simulation;

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
    /// 두 경기 합산이 같으면 <b>정규리그 순위가 높은 쪽</b>이 올라간다. 실제로는 연장과
    /// 승부차기까지 가지만, 한 시즌을 더 잘 치른 팀에게 이점을 주는 규정 자체는 흔하다.
    /// </summary>
    public static class PromotionPlayoff
    {
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
                up.Add(RunBracket(w, field, rng, log));

                return (downByPos.Take(spots).ToList(), up);
            }

            // ── 승강 플레이오프 (독일식) ──────────────────────────────────
            if (survival && downByPos.Count > spots && upByPos.Count > spots)
            {
                var up = upByPos.Take(spots).ToList();
                var down = downByPos.Take(spots).ToList();

                int safe = downByPos[spots];        // 가까스로 살아남은 1부 팀
                int challenger = upByPos[spots];    // 자동 승격을 놓친 2부 팀

                log.Add($"승강 플레이오프 — {Name(w, safe)}({top.shortName}) "
                        + $"대 {Name(w, challenger)}({below.shortName})");

                // 아래 팀이 먼저 홈에서 치른다. 두 경기 합산이 같으면 위 팀이 남는다.
                var leg1 = Leg(w, challenger, safe, rng, 1);
                var leg2 = Leg(w, safe, challenger, rng, 2);
                int challengerGoals = leg1.home + leg2.away;
                int safeGoals = leg1.away + leg2.home;

                log.Add($"  1차 {Score(leg1)} · 2차 {Score(leg2)}"
                        + $" → 합산 {challengerGoals}-{safeGoals}");

                if (challengerGoals > safeGoals)
                {
                    log.Add($"  {Name(w, challenger)} 이 올라갑니다.");
                    up.Add(challenger);
                    down.Add(safe);
                }
                else
                {
                    log.Add($"  {Name(w, safe)} 이 남습니다.");
                }

                return (down, up);
            }

            // ── 순위 그대로 ───────────────────────────────────────────────
            return (downByPos.Take(spots).ToList(), upByPos.Take(spots).ToList());
        }

        // ── 대진 ──────────────────────────────────────────────────────────

        /// <summary>
        /// 씨드 토너먼트. 참가 팀이 2의 거듭제곱이 아니면 위 씨드가 부전승으로 올라간다
        /// (여섯 팀이면 1·2위가 준결승부터 시작한다).
        ///
        /// 준결승까지는 홈·원정 두 경기, 결승은 한 판이다 — 실제로 그렇게 치른다.
        /// </summary>
        static int RunBracket(GameWorld w, List<int> field, Random rng, List<string> log)
        {
            // field 는 순위 순이다. 0번이 가장 높은 씨드.
            int size = 1;
            while (size < field.Count) size *= 2;

            // 씨드 자리 배치 — 1번과 2번이 결승에서야 만나게 한다.
            var slots = SeedOrder(size).Select(seed => seed < field.Count ? field[seed] : -1).ToList();

            int round = 0;
            while (slots.Count > 1)
            {
                round++;
                bool final = slots.Count == 2;
                var next = new List<int>();

                for (int i = 0; i < slots.Count; i += 2)
                {
                    int high = slots[i], low = slots[i + 1];

                    if (low < 0) { next.Add(high); continue; }   // 부전승
                    if (high < 0) { next.Add(low); continue; }

                    next.Add(final
                        ? PlayFinal(w, high, low, rng, round, log)
                        : PlayTwoLegs(w, high, low, rng, round, log));
                }

                slots = next;
            }

            log.Add($"  승격 — {Name(w, slots[0])}");
            return slots[0];
        }

        /// <summary>준결승까지. 아래 씨드가 1차전 홈이고, 합산이 같으면 위 씨드가 오른다.</summary>
        static int PlayTwoLegs(GameWorld w, int high, int low, Random rng, int round,
                               List<string> log)
        {
            var leg1 = Leg(w, low, high, rng, round * 10 + 1);
            var leg2 = Leg(w, high, low, rng, round * 10 + 2);

            int highGoals = leg1.away + leg2.home;
            int lowGoals = leg1.home + leg2.away;

            int winner = highGoals >= lowGoals ? high : low;
            string how = highGoals == lowGoals ? ", 순위가 높은 쪽이 오름" : "";

            log.Add($"  {Name(w, high)} {highGoals}-{lowGoals} {Name(w, low)}"
                    + $" (합산 {Score(leg1)}, {Score(leg2)}{how})");
            return winner;
        }

        /// <summary>결승은 한 판. 비기면 순위가 높은 쪽이 올라간다.</summary>
        static int PlayFinal(GameWorld w, int high, int low, Random rng, int round, List<string> log)
        {
            var (h, a) = Leg(w, high, low, rng, round * 10 + 3);

            int winner = h >= a ? high : low;
            string how = h == a ? " (비겨 순위가 높은 쪽)" : "";

            log.Add($"  결승 {Name(w, high)} {h}-{a} {Name(w, low)}{how}");
            return winner;
        }

        /// <summary>한 경기. 씨드는 구단과 회차에서 뽑아 같은 세계면 같은 결과가 나온다.</summary>
        static (int home, int away) Leg(GameWorld w, int homeId, int awayId,
                                        Random rng, int tag)
        {
            int seed = SeedMix.Of(w.Seed, homeId * 7919 + awayId * 31 + tag, 40519);
            return MatchDay.SimulateOneOff(w, homeId, awayId, 900000 + homeId * 100 + tag, seed);
        }

        static string Score((int home, int away) r) => $"{r.home}-{r.away}";

        static string Name(GameWorld w, int clubId) => w.Club(clubId)?.name ?? "?";

        /// <summary>
        /// 크기 size 인 대진의 씨드 순서. 1번과 2번이 결승에서야 만나는 그 배치다.
        /// size=4 면 0,3,1,2 — 1위와 4위, 2위와 3위가 먼저 붙는다.
        /// </summary>
        static List<int> SeedOrder(int size)
        {
            var order = new List<int> { 0 };
            for (int step = 1; step < size; step *= 2)
            {
                var next = new List<int>();
                foreach (int seed in order)
                {
                    next.Add(seed);
                    next.Add(step * 2 - 1 - seed);
                }
                order = next;
            }
            return order;
        }
    }
}
