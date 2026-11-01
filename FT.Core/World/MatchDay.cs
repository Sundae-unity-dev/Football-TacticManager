using System;
using System.Collections.Generic;
using System.Linq;
using FootballTactician.Data;
using FootballTactician.Diagnostics;
using FootballTactician.Simulation;

namespace FootballTactician.World
{
    /// <summary>
    /// 한 경기를 치르고 세계에 반영한다 — 라인업 짜기 · 엔진 호출 · 기록 · 체력·사기.
    ///
    /// 진행 엔진(<see cref="GameEngine"/>) 안에 있던 부분이다. 리그 라운드만 이 길을 쓰는 게 아니라
    /// 컵과 대륙 대회도 같은 길을 써야 하는데, 엔진 안에 두니 컵이 엔진을 되참조했다 —
    /// 엔진은 컵 라운드를 부르고, 컵은 엔진의 경기 함수를 부르는 양방향이 됐다.
    /// 치르는 일을 여기로 떼어 내면 양쪽이 모두 이 파일만 보면 된다.
    ///
    /// 경로가 둘이면 결과가 어긋난다. 자동 진행과 사람이 치르는 경기가 모두
    /// 여기의 뒷처리(<see cref="ApplyMatchFatigue"/> · <see cref="ApplyMatchMorale"/>)를 쓴다.
    /// </summary>
    public static class MatchDay
    {
        /// <summary>한 경기를 시뮬레이션하고 결과를 일정에 기록한다.</summary>
        public static MatchResultData SimulateFixture(GameWorld w, FixtureData f, int seedSalt = 0)
        {
            MatchLineup home, away;
            using (SimProfile.Measure("    · 라인업 짜기"))
            {
                home = BuildLineup(w, f.homeClubId);
                away = BuildLineup(w, f.awayClubId);
            }
            if (home == null || away == null) return null;

            var ctx = new MatchSimContext
            {
                fixtureId = f.id,
                home = home,
                away = away,
                seed = SeedMix.Of(w.Seed, f.id, seedSalt),
            };

            MatchResultData result;
            using (SimProfile.Measure("    · 경기 시뮬레이션"))
                result = MatchEngine.Simulate(ctx, w.Lookup);
            using (SimProfile.Measure("    · 기록 반영"))
                MatchEngine.ApplyResult(result, ctx, w.Lookup);

            f.result = result;
            f.isPlayed = true;

            using (SimProfile.Measure("    · 체력·사기"))
            {
                ApplyMatchFatigue(w, ctx, result);
                ApplyMatchMorale(w, f, result);
            }

            // 계약서에 적은 출전·골 보너스가 장부에서 빠진다.
            // 보너스가 없는 계약(AI 구단 대부분)은 null 검사로 끝난다.
            ContractBonuses.PayMatch(w, home, away, result);
            return result;
        }

        public static MatchLineup BuildLineup(GameWorld w, int clubId)
        {
            var club = w.Club(clubId);
            if (club == null) return null;

            var tac = club.savedTactics.FirstOrDefault(t => t.id == club.activeTacticsId)
                      ?? club.savedTactics.FirstOrDefault();
            if (tac == null) return null;

            // 명단은 한 번만 모은다. 배치와 벤치가 각각 선수 목록을 다시 만들고 있었다.
            // 없는 번호(null)는 미리 뺀다 — 양쪽 다 어차피 건너뛰므로 결과가 같다.
            var squad = new List<PlayerData>(club.playerIds.Count);
            foreach (int id in club.playerIds)
            {
                var p = w.Player(id);
                if (p != null) squad.Add(p);
            }

            // AI 구단과 자동 선발을 켠 플레이어 구단은 매 경기 다시 뽑는다.
            // 빈 슬롯만 메우면 부상에서 돌아온 주전이 영영 벤치에 남아 시즌 내내 전력이 샌다.
            bool auto = clubId != w.ManagedClubId || w.AutoLineup;
            if (auto)
            {
                LineupBuilder.Assign(tac, squad);
            }
            else
            {
                // 감독이 직접 짠 라인업 — 뛸 수 없는 선수만 바꾼다.
                var unavailable = tac.slots.Where(s =>
                {
                    var p = w.Player(s.assignedPlayerId);
                    return p == null || p.IsInjured || p.clubId != clubId;
                }).ToList();

                if (unavailable.Count > 0)
                {
                    var taken = tac.slots.Select(s => s.assignedPlayerId).ToHashSet();
                    foreach (var slot in unavailable)
                    {
                        var pick = squad
                            .Where(p => p != null && !p.IsInjured && !taken.Contains(p.id))
                            .OrderByDescending(p => LineupBuilder.Fit(p, slot.position))
                            .FirstOrDefault();
                        slot.assignedPlayerId = pick?.id ?? -1;
                        if (pick != null) taken.Add(pick.id);
                    }
                }
            }

            List<int> bench;
            using (SimProfile.Measure("      · 벤치"))
                bench = LineupBuilder.Bench(tac, squad);
            return new MatchLineup { clubId = clubId, tactics = tac, benchIds = bench };
        }

        internal static void ApplyMatchFatigue(GameWorld w, MatchSimContext ctx, MatchResultData result)
        {
            foreach (var lineup in new[] { ctx.home, ctx.away })
                foreach (var slot in lineup.tactics.slots)
                {
                    var p = w.Player(slot.assignedPlayerId);
                    if (p == null) continue;
                    // 체력이 좋을수록 덜 빠진다. 주 1경기면 회복과 맞물려 85 언저리에서 안정된다.
                    float drop = 16f - p.physical.stamina * 0.35f;
                    p.condition = Math.Clamp(p.condition - drop, 15f, 100f);
                }
        }

        /// <summary>
        /// 경기 결과를 양 팀 사기에 반영한다. 플레이어 구단만 적용하면 혼자 사기가 흔들려
        /// 전력과 무관하게 순위가 무너진다. 폭은 주간 복원으로 되돌아올 수 있는 크기로 둔다.
        /// </summary>
        internal static void ApplyMatchMorale(GameWorld w, FixtureData f, MatchResultData r)
        {
            int diff = r.homeGoals - r.awayGoals;
            Shift(f.homeClubId, diff);
            Shift(f.awayClubId, -diff);

            void Shift(int clubId, int margin)
            {
                float d = margin > 0 ? 4f : margin == 0 ? 0f : -4f;
                d += Math.Clamp(margin, -2, 2);          // 대패와 대승은 조금 더 움직인다
                foreach (var p in w.Squad(clubId))
                    p.morale = Math.Clamp(p.morale + d, 25f, 95f);
            }
        }

        /// <summary>컵 경기 한 판. 리그 경기와 같은 엔진을 쓰되 일정에는 기록하지 않는다.</summary>
        public static (int home, int away) SimulateCupTie(GameWorld w, CupState cup, CupTie tie)
        {
            var home = BuildLineup(w, tie.homeClubId);
            var away = BuildLineup(w, tie.awayClubId);
            if (home == null || away == null) return (0, 0);

            var ctx = new MatchSimContext
            {
                fixtureId = cup.id * 100 + cup.currentRound * 10 + tie.homeClubId % 10,
                home = home,
                away = away,
                seed = SeedMix.Of(w.Seed, cup.id * 977 + cup.currentRound * 31 + tie.homeClubId, 5501),
                rules = w.LeagueOf(tie.homeClubId)?.matchRules ?? new MatchRules(),
            };

            var result = MatchEngine.Simulate(ctx, w.Lookup);
            MatchEngine.ApplyResult(result, ctx, w.Lookup);
            ApplyMatchFatigue(w, ctx, result);

            return (result.homeGoals, result.awayGoals);
        }
    }
}
