using System.Collections.Generic;
using System.Linq;
using FootballTactician.Simulation;

namespace FootballTactician.World
{
    /// <summary>
    /// 씨드 토너먼트 하나를 치른다.
    ///
    /// 승격 플레이오프(④)와 챔피언 결정 플레이오프(⑥)는 치르는 방식이 같다 — 순위 순으로
    /// 씨드를 매기고, 준결승까지는 홈·원정 두 경기, 결승은 한 판. 다른 것은 <b>무엇을 걸고
    /// 싸우는가</b>뿐이라, 대진 자체는 여기 한 곳에만 둔다.
    ///
    /// 참가 팀이 2의 거듭제곱이 아니면 위 씨드가 부전승으로 올라간다(여섯 팀이면 1·2위가
    /// 준결승부터 시작한다). 합산이 같으면 <b>정규리그 순위가 높은 쪽</b>이 오른다.
    /// </summary>
    public static class Knockout
    {
        /// <summary>
        /// 대회마다 다른 값. 같은 팀들이 같은 해에 두 대진을 치러도 결과가 겹치지 않게
        /// <see cref="SeedKey"/> 를 달리 준다.
        /// </summary>
        public readonly struct Rules
        {
            /// <summary>경기 씨드를 섞는 열쇠. 대회마다 달라야 한다.</summary>
            public readonly int SeedKey;

            /// <summary>일정에 없는 경기에 붙일 id 바닥.</summary>
            public readonly int FixtureBase;

            /// <summary>마지막 줄에 적을 말 — "승격" 또는 "우승".</summary>
            public readonly string WinnerWord;

            public Rules(int seedKey, int fixtureBase, string winnerWord)
            {
                SeedKey = seedKey;
                FixtureBase = fixtureBase;
                WinnerWord = winnerWord;
            }
        }

        /// <summary>
        /// 대진을 끝까지 치르고 이긴 구단 id 를 준다.
        /// <paramref name="field"/> 는 순위 순이어야 한다 — 0번이 가장 높은 씨드다.
        /// </summary>
        public static int Run(GameWorld w, List<int> field, Rules rules, List<string> log)
        {
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
                        ? PlayFinal(w, high, low, rules, round, log)
                        : PlayTwoLegs(w, high, low, rules, round, log));
                }

                slots = next;
            }

            log.Add($"  {rules.WinnerWord} — {Name(w, slots[0])}");
            return slots[0];
        }

        /// <summary>준결승까지. 아래 씨드가 1차전 홈이고, 합산이 같으면 위 씨드가 오른다.</summary>
        static int PlayTwoLegs(GameWorld w, int high, int low, Rules rules, int round,
                               List<string> log)
        {
            var leg1 = Leg(w, low, high, rules, round * 10 + 1);
            var leg2 = Leg(w, high, low, rules, round * 10 + 2);

            int highGoals = leg1.away + leg2.home;
            int lowGoals = leg1.home + leg2.away;

            int winner = highGoals >= lowGoals ? high : low;
            string how = highGoals == lowGoals ? ", 순위가 높은 쪽이 오름" : "";

            log.Add($"  {Name(w, high)} {highGoals}-{lowGoals} {Name(w, low)}"
                    + $" (합산 {Score(leg1)}, {Score(leg2)}{how})");
            return winner;
        }

        /// <summary>결승은 한 판. 비기면 순위가 높은 쪽이 가져간다.</summary>
        static int PlayFinal(GameWorld w, int high, int low, Rules rules, int round,
                             List<string> log)
        {
            var (h, a) = Leg(w, high, low, rules, round * 10 + 3);

            int winner = h >= a ? high : low;
            string how = h == a ? " (비겨 순위가 높은 쪽)" : "";

            log.Add($"  결승 {Name(w, high)} {h}-{a} {Name(w, low)}{how}");
            return winner;
        }

        /// <summary>한 경기. 씨드는 구단과 회차에서 뽑아 같은 세계면 같은 결과가 나온다.</summary>
        public static (int home, int away) Leg(GameWorld w, int homeId, int awayId,
                                               Rules rules, int tag)
        {
            int seed = SeedMix.Of(w.Seed, homeId * 7919 + awayId * 31 + tag, rules.SeedKey);
            return MatchDay.SimulateOneOff(w, homeId, awayId,
                                           rules.FixtureBase + homeId * 100 + tag, seed);
        }

        public static string Score((int home, int away) r) => $"{r.home}-{r.away}";

        public static string Name(GameWorld w, int clubId) => w.Club(clubId)?.name ?? "?";

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
