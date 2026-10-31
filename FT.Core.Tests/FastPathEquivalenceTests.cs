using System;
using System.Collections.Generic;
using System.Linq;
using FootballTactician.Balance;
using FootballTactician.Data;
using FootballTactician.Simulation;
using FootballTactician.World;

namespace FT.Core.Tests
{
    /// <summary>
    /// 빠르게 고친 자리가 원래 구현과 **같은 답**을 내는지 본다.
    ///
    /// 2026-10-31, 라운드 비용을 48 → 30ms 로 줄이면서 "한 명을 고르려고 정렬"하던 자리를
    /// 한 번 훑기로 바꿨다. 정렬과 훑기는 동점에서 갈라지기 쉽다 — 내림차순 안정 정렬은
    /// 동점일 때 먼저 오는 쪽을 남기므로, 훑으면서 '크거나 같을 때' 바꾸면 결과가 조용히 달라진다.
    /// 라인업이 한 자리만 달라도 그 시드의 모든 측정값이 밀린다.
    ///
    /// 그래서 원래의 정렬 기반 구현을 **기준**으로 여기 남겨 두고 전수로 견준다.
    /// 배치 규칙을 고칠 때는 기준 구현도 같이 고쳐야 한다 — 그게 이 테스트의 값이다.
    /// </summary>
    public class FastPathEquivalenceTests
    {
        [Theory]
        [InlineData(31415)]
        [InlineData(777)]
        [InlineData(20261031)]
        public void 라인업_배치는_정렬_기준_구현과_같다(int seed)
        {
            var w = WorldGenerator.Create(seed);
            int compared = 0;

            foreach (var club in w.Clubs.Values)
            {
                var squad = w.Squad(club.id).ToList();

                foreach (var preset in FormationDatabase.All)
                {
                    var fast = FormationDatabase.CreateTactics(preset.id, "fast");
                    var slow = FormationDatabase.CreateTactics(preset.id, "slow");

                    LineupBuilder.Assign(fast, squad);
                    AssignBySorting(slow, squad);

                    foreach (var s in fast.slots)
                    {
                        var other = slow.slots.First(x => x.slotIndex == s.slotIndex);
                        Assert.Equal(other.assignedPlayerId, s.assignedPlayerId);
                    }
                    compared++;
                }
            }

            Assert.True(compared > 2000, $"견준 라인업이 너무 적다: {compared}");
        }

        [Theory]
        [InlineData(31415)]
        [InlineData(20261031)]
        public void 스쿼드_서열은_정렬_기준_구현과_같다(int seed)
        {
            var w = WorldGenerator.Create(seed);

            foreach (var p in w.Players.Values)
            {
                int expected = w.Squad(p.clubId)
                    .OrderByDescending(GameWorld.Ca)
                    .ToList()
                    .FindIndex(x => x.id == p.id);

                Assert.Equal(expected, TransferMarket.SquadRank(w, p));
            }
        }

        [Theory]
        [InlineData(31415)]
        [InlineData(20261031)]
        public void 탈의실_주장과_영향력_3인은_정렬_기준_구현과_같다(int seed)
        {
            var w = WorldGenerator.Create(seed);

            foreach (var club in w.Clubs.Values)
            {
                var squad = w.Squad(club.id).ToList();
                if (squad.Count == 0) continue;

                DressingRoom.Update(w, club);
                var room = club.dressingRoom;

                var ranked = squad.OrderByDescending(p => DressingRoom.Influence(w, p)).ToList();

                Assert.Equal(ranked[0].id, room.captainPlayerId);
                Assert.Equal(ranked[1].id, room.viceCaptainPlayerId);
                Assert.Equal(ranked.Take(3).Select(p => p.id).ToList(), room.influencerPlayerIds);
            }
        }

        [Theory]
        [InlineData(31415)]
        [InlineData(20261031)]
        public void 구단_전력은_정렬_기준_구현과_같다(int seed)
        {
            var w = WorldGenerator.Create(seed);

            // 부상자를 섞는다 — 전력은 뛸 수 있는 선수만 센다.
            // 부상자가 없으면 '건너뛰기'가 틀려도 통과한다.
            int n = 0;
            foreach (var p in w.Players.Values)
                if (n++ % 7 == 0)
                    p.injury = new InjuryData
                    {
                        injuryType = "기준 비교",
                        severity = InjurySeverity.Moderate,
                        injuryDate = w.Date,
                        expectedReturnDate = w.Date.AddDays(28),
                        isActive = true,
                    };

            foreach (var club in w.Clubs.Values)
                Assert.Equal(StrengthBySorting(w, club.id), w.Strength(club.id));
        }

        [Theory]
        [InlineData(31415)]
        [InlineData(777)]
        public void 벤치는_정렬_기준_구현과_같다(int seed)
        {
            var w = WorldGenerator.Create(seed);
            int compared = 0;

            foreach (var club in w.Clubs.Values)
            {
                var squad = w.Squad(club.id).ToList();

                foreach (var preset in FormationDatabase.All)
                {
                    var tac = FormationDatabase.CreateTactics(preset.id, "벤치 비교");
                    LineupBuilder.Assign(tac, squad);

                    Assert.Equal(BenchBySorting(tac, squad), LineupBuilder.Bench(tac, squad));
                    compared++;
                }
            }

            Assert.True(compared > 2000, $"견준 벤치가 너무 적다: {compared}");
        }

        [Theory]
        [InlineData(31415)]
        public void 자리별_최고_CA_표는_명단을_센_것과_같다(int seed)
        {
            var w = WorldGenerator.Create(seed);
            var peaks = new SquadPeaks(w);
            var masks = Masks();

            foreach (var club in w.Clubs.Values)
            foreach (var mask in masks)
                Assert.Equal(w.BestCaAt(club.id, mask),
                             peaks.BestCaAt(club.id, mask));

            // 명단이 바뀌면 그 구단만 다시 잰다. 이게 어긋나면 임대가 사실과 다른 표를 본다.
            var donor = w.Clubs.Values.First();
            var taker = w.Clubs.Values.Last();
            var moved = w.Squad(donor.id).OrderByDescending(GameWorld.Ca).First();

            donor.playerIds.Remove(moved.id);
            taker.playerIds.Add(moved.id);
            moved.clubId = taker.id;
            peaks.Refresh(donor.id);
            peaks.Refresh(taker.id);

            foreach (var mask in masks)
            {
                Assert.Equal(w.BestCaAt(donor.id, mask), peaks.BestCaAt(donor.id, mask));
                Assert.Equal(w.BestCaAt(taker.id, mask), peaks.BestCaAt(taker.id, mask));
            }
        }

        /// <summary>
        /// 적합도는 등급과 '본 포지션인지'만 보고 CA 에 정해진 값을 얹는다.
        /// 2026-10-31, Tier 를 거치지 않고 한 함수에서 분기하도록 고치면서
        /// 분기 순서가 바뀌면 조용히 달라질 수 있는 자리가 됐다.
        /// 보너스 값을 베껴 적지 않고 '묶음 안에서 늘 같은가'로 확인한다 —
        /// 값이 바뀌어도 이 테스트는 살아 있어야 한다.
        /// </summary>
        [Theory]
        [InlineData(31415)]
        public void 적합도는_자리_소화_등급만으로_정해진다(int seed)
        {
            var w = WorldGenerator.Create(seed);
            var bonus = new Dictionary<(LineupBuilder.FitTier, bool), float>();

            foreach (var p in w.Players.Values)
            foreach (var pos in Masks())
            {
                var tier = LineupBuilder.Tier(p, pos);
                float fit = LineupBuilder.Fit(p, pos);

                if (tier == LineupBuilder.FitTier.Impossible)
                {
                    Assert.Equal(-1000f, fit);
                    continue;
                }

                var key = (tier, (p.positionFlags & pos) != 0);
                float extra = fit - BalanceConfig.PowerToCa(p.AbilityScore);

                // 큰 CA 에서 더했다 빼면 마지막 자리가 흔들린다(60 이 60.0000076 로 나온다).
                // 그건 이 테스트가 값을 되짚는 방식의 한계지, 적합도의 문제가 아니다.
                if (bonus.TryGetValue(key, out float known)) Assert.Equal(known, extra, 3);
                else bonus[key] = extra;
            }

            float natural = bonus[(LineupBuilder.FitTier.Natural, true)];
            float related = bonus[(LineupBuilder.FitTier.Related, false)];
            float unfamiliar = bonus[(LineupBuilder.FitTier.Unfamiliar, false)];

            Assert.Equal(0f, unfamiliar);
            Assert.True(natural > related, "본 포지션이 인접 포지션보다 나아야 한다");
            Assert.True(related > unfamiliar, "인접 포지션이 낯선 자리보다 나아야 한다");
        }

        /// <summary>
        /// 역할 적합도가 쓰는 '본인 평균'이 <see cref="PlayerData.AbilityScore"/> 와 같은 값인지.
        /// 2026-10-31, 같은 식이라는 이유로 캐시된 쪽을 쓰도록 고쳤다 —
        /// 한쪽 식이 바뀌면 역할 적합도가 조용히 틀어진다.
        /// </summary>
        [Theory]
        [InlineData(31415)]
        [InlineData(20261031)]
        public void 능력치_평균은_AbilityScore_와_같다(int seed)
        {
            var w = WorldGenerator.Create(seed);
            int compared = 0;

            foreach (var p in w.Players.Values)
            {
                float byHand = (p.technical.Average() + p.physical.Average() + p.mental.Average()) / 3f;
                Assert.Equal(byHand, p.AbilityScore);
                compared++;
            }

            Assert.True(compared > 5000, $"견준 선수가 너무 적다: {compared}");
        }

        [Theory]
        [InlineData(31415)]
        public void 역할_균형과_지시_합계는_어느_입구로_불러도_같다(int seed)
        {
            var w = WorldGenerator.Create(seed);
            int compared = 0;

            foreach (var club in w.Clubs.Values)
            foreach (var tac in club.savedTactics)
            {
                Assert.Equal(RoleFit.BalanceMultiplier(tac.slots.Select(s => s.role)),
                             RoleFit.BalanceMultiplier(tac.slots));

                Assert.Equal(
                    PlayerInstructionEffect.TeamTotals(tac.slots.Select(s => s.customInstructions)),
                    PlayerInstructionEffect.TeamTotals(tac.slots));
                compared++;
            }

            Assert.True(compared >= 240, $"견준 전술이 너무 적다: {compared}");
        }

        // ── 기준 구현 ─────────────────────────────────────────────────────

        /// <summary>단일 자리 전부와 흔히 쓰는 조합 몇 개.</summary>
        static List<Position> Masks()
        {
            var list = Enum.GetValues<Position>().Where(p => p != Position.None).ToList();
            list.Add(Position.ST | Position.CF);
            list.Add(Position.LW | Position.RW | Position.CAM);
            list.Add(Position.CB | Position.CDM);
            return list;
        }

        /// <summary>2026-10-31 이전의 <see cref="GameWorld.Strength"/>.</summary>
        static float StrengthBySorting(GameWorld w, int clubId)
        {
            var top = w.Squad(clubId)
                .Where(p => !p.IsInjured)
                .OrderByDescending(GameWorld.Ca)
                .Take(11)
                .ToList();
            return top.Count == 0 ? 0f : top.Average(GameWorld.Ca);
        }

        /// <summary>2026-10-31 이전의 <see cref="LineupBuilder.Bench"/>.</summary>
        static List<int> BenchBySorting(TacticsData tactics, List<PlayerData> squad, int size = 7)
        {
            var starters = tactics.slots.Select(s => s.assignedPlayerId).ToHashSet();
            return squad
                .Where(p => p != null && !p.IsInjured && !starters.Contains(p.id))
                .OrderByDescending(p => p.AbilityScore)
                .Take(size)
                .Select(p => p.id)
                .ToList();
        }

        // ── 기준 구현 ─────────────────────────────────────────────────────

        /// <summary>
        /// 2026-10-31 이전의 <see cref="LineupBuilder.Assign"/>. 자리마다 명단을 정렬해 첫 번째를
        /// 집고, 맞바꿔 개선할 때마다 사전을 새로 만들고 적합도를 네 번씩 다시 구한다.
        /// 느리지만 뜻이 또렷해서 기준으로 쓴다.
        /// </summary>
        static void AssignBySorting(TacticsData tactics, IEnumerable<PlayerData> squad)
        {
            if (tactics == null) return;

            var pool = squad.Where(p => p != null && !p.IsInjured).ToList();
            var slots = tactics.slots.OrderBy(s => s.slotIndex).ToList();
            if (pool.Count == 0 || slots.Count == 0) return;

            var order = slots
                .OrderBy(s => pool.Count(p => (p.positionFlags & s.position) != 0))
                .ThenByDescending(s => (s.position & Position.GK) != 0 ? 1 : 0)
                .ToList();

            var used = new HashSet<int>();
            foreach (var slot in order)
            {
                var pick = pool
                    .Where(p => !used.Contains(p.id))
                    .OrderByDescending(p => LineupBuilder.Fit(p, slot.position))
                    .ThenByDescending(p => p.AbilityScore)
                    .FirstOrDefault();
                slot.assignedPlayerId = pick?.id ?? -1;
                if (pick != null) used.Add(pick.id);
            }

            var byId = pool.ToDictionary(p => p.id);

            for (int pass = 0; pass < 4; pass++)
            {
                bool changed = false;

                for (int i = 0; i < slots.Count; i++)
                    for (int j = i + 1; j < slots.Count; j++)
                    {
                        if (!byId.TryGetValue(slots[i].assignedPlayerId, out var a)) continue;
                        if (!byId.TryGetValue(slots[j].assignedPlayerId, out var b)) continue;

                        float now = LineupBuilder.Fit(a, slots[i].position)
                                  + LineupBuilder.Fit(b, slots[j].position);
                        float swapped = LineupBuilder.Fit(b, slots[i].position)
                                      + LineupBuilder.Fit(a, slots[j].position);

                        if (swapped > now + 0.01f)
                        {
                            (slots[i].assignedPlayerId, slots[j].assignedPlayerId) =
                                (slots[j].assignedPlayerId, slots[i].assignedPlayerId);
                            changed = true;
                        }
                    }

                if (!changed) break;
            }
        }
    }
}
