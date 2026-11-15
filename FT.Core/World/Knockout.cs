using System;
using System.Collections.Generic;
using System.Linq;
using FootballTactician.Data;
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

            /// <summary>
            /// 감독이 직접 뛸 때 경기 화면에 적을 대회 이름 — "챔피언십 승격 플레이오프".
            /// 그 뒤에 "준결승 1차전" 이 붙는다.
            /// </summary>
            public readonly string Title;

            public Rules(int seedKey, int fixtureBase, string winnerWord, string title = null)
            {
                SeedKey = seedKey;
                FixtureBase = fixtureBase;
                WinnerWord = winnerWord;
                Title = title;
            }

            public Rules Titled(string title) => new(SeedKey, FixtureBase, WinnerWord, title);
        }

        /// <summary>
        /// 시즌 넘김이 내 경기 앞에서 멈췄다. <see cref="GameWorld.pendingMatch"/> 에
        /// 그 경기가 얹혀 있다 — 대진을 걷던 길을 통째로 빠져나가는 데만 쓴다.
        /// </summary>
        public sealed class Paused : Exception
        {
            public Paused() : base("내 플레이오프 경기 앞에서 멈췄다") { }
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
                string stage = RoundName(slots.Count);
                var next = new List<int>();

                for (int i = 0; i < slots.Count; i += 2)
                {
                    int high = slots[i], low = slots[i + 1];

                    if (low < 0) { next.Add(high); continue; }   // 부전승
                    if (high < 0) { next.Add(low); continue; }

                    next.Add(final
                        ? PlayFinal(w, high, low, rules, round, log)
                        : PlayTwoLegs(w, high, low, rules, round, stage, log));
                }

                slots = next;
            }

            log.Add($"  {rules.WinnerWord} — {Name(w, slots[0])}");
            return slots[0];
        }

        /// <summary>준결승까지. 아래 씨드가 1차전 홈이고, 합산이 같으면 위 씨드가 오른다.</summary>
        static int PlayTwoLegs(GameWorld w, int high, int low, Rules rules, int round,
                               string stage, List<string> log)
        {
            var leg1 = Leg(w, low, high, rules, round * 10 + 1, $"{stage} 1차전");
            var leg2 = Leg(w, high, low, rules, round * 10 + 2, $"{stage} 2차전");

            int highGoals = leg1.away + leg2.home;
            int lowGoals = leg1.home + leg2.away;

            int winner = highGoals >= lowGoals ? high : low;
            string how = highGoals == lowGoals ? ", 순위가 높은 쪽이 오름" : "";

            // "A 8-3 B (합산 1-4, 4-2)" 라고 적었더니 8-3 이 한 경기 점수로 읽혔다.
            // 합계인지 한 판인지가 숫자 앞에 있어야 한다.
            log.Add($"  {Name(w, high)} 합산 {highGoals}-{lowGoals} {Name(w, low)}"
                    + $" (1차 {Score(leg1)} · 2차 {Score(leg2)}{how})");
            return winner;
        }

        /// <summary>결승은 한 판. 비기면 순위가 높은 쪽이 가져간다.</summary>
        static int PlayFinal(GameWorld w, int high, int low, Rules rules, int round,
                             List<string> log)
        {
            var (h, a) = Leg(w, high, low, rules, round * 10 + 3, "결승");

            int winner = h >= a ? high : low;
            string how = h == a ? " (비겨 순위가 높은 쪽)" : "";

            log.Add($"  결승 {Name(w, high)} {h}-{a} {Name(w, low)}{how}");
            return winner;
        }

        /// <summary>
        /// 한 경기. 씨드는 구단과 회차에서 뽑아 같은 세계면 같은 결과가 나온다.
        ///
        /// 시즌 넘김 중이면(<see cref="GameWorld.rollover"/>) 두 가지가 더 붙는다.
        /// <b>이미 치른 경기</b>는 다시 치르지 않고 적어 둔 점수를 준다 — 멈췄다 이어 붙일 때
        /// 대진을 처음부터 다시 걷기 때문이다. <b>내 경기</b>면 치르지 않고 감독에게 넘긴 뒤
        /// <see cref="Paused"/> 로 빠져나간다.
        /// </summary>
        public static (int home, int away) Leg(GameWorld w, int homeId, int awayId,
                                               Rules rules, int tag, string stage = null)
        {
            var ro = w.rollover;
            if (ro != null && ro.TryGet(rules.SeedKey, homeId, awayId, tag, out var done))
                return done;

            var leg = new PlayoffLeg
            {
                seedKey = rules.SeedKey, homeClubId = homeId, awayClubId = awayId, tag = tag,
            };

            if (ro is { deferClubId: > 0 } && (homeId == ro.deferClubId || awayId == ro.deferClubId))
            {
                w.pendingMatch = new PendingMatch
                {
                    leg = leg,
                    label = string.Join(" ", new[] { rules.Title, stage }
                                                .Where(x => !string.IsNullOrEmpty(x))),
                    fixture = new FixtureData
                    {
                        id = FixtureId(rules.FixtureBase, homeId, tag),
                        homeClubId = homeId,
                        awayClubId = awayId,
                        date = w.Date,
                        isPlayed = false,
                    },
                };
                throw new Paused();
            }

            var r = MatchDay.SimulateOneOff(w, homeId, awayId,
                                            FixtureId(rules.FixtureBase, homeId, tag),
                                            LegSeed(w, leg));
            if (ro != null)
            {
                (leg.homeGoals, leg.awayGoals) = r;
                ro.Add(leg);
            }
            return r;
        }

        static int FixtureId(int fixtureBase, int homeId, int tag) => fixtureBase + homeId * 100 + tag;

        /// <summary>
        /// 그 한 판의 씨드. 감독이 "바로 결과 보기" 를 누르면 이 씨드로 돌린다 —
        /// 그래야 미루지 않고 넘긴 세계와 한 점도 다르지 않다.
        /// </summary>
        public static int LegSeed(GameWorld w, PlayoffLeg leg)
            => SeedMix.Of(w.Seed, leg.homeClubId * 7919 + leg.awayClubId * 31 + leg.tag, leg.seedKey);

        /// <summary>남은 자리 수로 라운드 이름을 붙인다.</summary>
        static string RoundName(int slots) => slots switch
        {
            2 => "결승",
            4 => "준결승",
            _ => $"{slots}강",
        };

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
