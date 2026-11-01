using System;
using System.Diagnostics;
using System.Linq;
using System.Collections.Generic;
using FootballTactician.Diagnostics;
using FootballTactician.Simulation;
using FootballTactician.Data;
using FootballTactician.World;
using FootballTactician.Balance;

namespace FT.Sim
{
    /// <summary>
    /// World 계층 점검. 화면을 붙이기 전에 생성·일정·진행·세이브가 맞는지 콘솔로 확인한다.
    /// </summary>
    internal static class WorldHarness
    {
        /// <summary>연속 홈·원정 분포. 일정 생성이 얼마나 고른지 본다.</summary>
        public static void Venue(int seeds)
        {
            Console.WriteLine($"=== 연속 장소 분포 ({seeds}개 시드) ===");
            var hist = new SortedDictionary<int, int>();
            int worst = 0;
            string worstWho = "";

            for (int s = 0; s < seeds; s++)
            {
                var w = WorldGenerator.Create(1000 + s * 7);
                foreach (var league in w.Leagues.Values)
                foreach (int id in league.clubIds)
                {
                    var venues = league.currentSeason.fixtures
                        .Where(f => f.homeClubId == id || f.awayClubId == id)
                        .OrderBy(f => f.matchday)
                        .Select(f => f.homeClubId == id)
                        .ToList();

                    int run = 1, max = 1;
                    for (int i = 1; i < venues.Count; i++)
                    {
                        run = venues[i] == venues[i - 1] ? run + 1 : 1;
                        max = Math.Max(max, run);
                    }
                    hist[max] = hist.GetValueOrDefault(max) + 1;
                    if (max > worst) { worst = max; worstWho = $"{w.Club(id)!.name} (시드 {1000 + s * 7})"; }
                }
            }

            int total = hist.Values.Sum();
            foreach (var (len, count) in hist)
                Console.WriteLine($"  최대 {len}연속: {count,5}구단 ({count * 100.0 / total,5:F1}%)");
            Console.WriteLine($"  최악: {worst}연속 — {worstWho}");
        }


        /// <summary>
        /// 역할이 의미가 있는지 본다.
        ///
        /// (1) 선수에 맞춰 역할을 고르면 관례값보다 나아야 한다 — 아니면 손댈 이유가 없다.
        /// (2) 아무 역할이나 꽂아도 비슷하면 안 된다 — 틀린 선택에 대가가 있어야 한다.
        /// (3) 어떤 역할도 모든 선수에게 최선이면 안 된다 — 그러면 그것만 쓰면 된다.
        /// </summary>
        public static void Roles(int matches)
        {
            Console.WriteLine($"=== 역할 ({matches}경기) ===");

            var (w, ca, cb) = RolePair();
            Console.WriteLine($"  {ca.name} vs {cb.name}");

            // 적합도 분포
            var fits = new List<int>();
            foreach (var club in w.Clubs.Values)
            {
                var tac = club.savedTactics.FirstOrDefault(t => t.id == club.activeTacticsId);
                if (tac == null) continue;
                foreach (var s in tac.slots)
                {
                    var p = w.Player(s.assignedPlayerId);
                    if (p == null || s.role == null) continue;
                    if (!RoleDatabase.All.TryGetValue(s.role.role, out var def)) continue;
                    fits.Add(RoleFit.Percent(p, def));
                }
            }
            Console.WriteLine($"  관례 역할 적합도 — 평균 {fits.Average():F0} · 최저 {fits.Min()} · 최고 {fits.Max()}");

            // 자리마다 역할 보정이 고르게 걸리는지. 한쪽만 이득을 보면 득점이 통째로 움직인다.
            Console.WriteLine();
            Console.WriteLine("  자리별 역할 배수 (1.00 이 중립)");
            foreach (var (label, mask) in new (string, Position)[]
                     {
                         ("골키퍼", Position.GK),
                         ("수비", Position.CB | Position.LB | Position.RB | Position.LWB | Position.RWB),
                         ("미드필더", Position.CDM | Position.CM | Position.CAM | Position.LM | Position.RM),
                         ("공격", Position.ST | Position.CF | Position.LW | Position.RW),
                     })
            {
                var muls = new List<float>();
                foreach (var club in w.Clubs.Values)
                {
                    var tac = club.savedTactics.FirstOrDefault(t => t.id == club.activeTacticsId);
                    if (tac == null) continue;
                    foreach (var s in tac.slots)
                    {
                        if ((s.position & mask) == 0) continue;
                        var p = w.Player(s.assignedPlayerId);
                        if (p == null || s.role == null) continue;
                        if (!RoleDatabase.All.TryGetValue(s.role.role, out var d)) continue;
                        muls.Add(RoleFit.Multiplier(p, d));
                    }
                }
                if (muls.Count > 0)
                    Console.WriteLine($"    {label,-6} {muls.Average():F3}");
            }

            {
                var bals = w.Clubs.Values
                    .Select(c => c.savedTactics.FirstOrDefault(t => t.id == c.activeTacticsId))
                    .Where(t => t != null)
                    .Select(t => RoleFit.BalanceMultiplier(t!.slots.Select(s => s.role)))
                    .ToList();
                Console.WriteLine();
                Console.WriteLine($"  역할 균형 배수 — 평균 {bals.Average():F3} · 최저 {bals.Min():F3} · " +
                                  $"깎인 구단 {bals.Count(b => b < 0.999f)}/{bals.Count}");
            }

            // 역할 선택별 승점
            Console.WriteLine();
            Console.WriteLine("  역할 선택                 적합도평균  경기당승점");
            foreach (var (label, mode) in new (string, RoleMode)[]
                     {
                         ("관례값 그대로", RoleMode.Convention),
                         ("선수에 맞춰 고름", RoleMode.BestFit),
                         ("일부러 안 맞게", RoleMode.WorstFit),
                         ("전원 만드는 역할", RoleMode.AllCreators),
                         ("전원 넣는 역할", RoleMode.AllFinishers),
                     })
            {
                var (ppg, avgFit) = RoleDuel(mode, matches);
                Console.WriteLine($"  {label,-22} {avgFit,9:F0}  {ppg,10:F2}");
            }

            // 지배적인 역할이 있는지 — 자리별로 모든 선수에게 최선인 역할
            Console.WriteLine();
            Console.WriteLine("  자리별 최적 역할 쏠림 (한 역할이 전부면 고를 이유가 없다)");
            foreach (var pos in new[] { Position.CB, Position.CM, Position.ST, Position.LW })
            {
                var picks = w.Players.Values
                    .Where(p => p.clubId > 0 && (p.positionFlags & pos) != 0)
                    .Select(p => RoleFit.Best(p, pos))
                    .Where(r => r.HasValue)
                    .GroupBy(r => r!.Value)
                    .OrderByDescending(g => g.Count())
                    .ToList();

                int total = picks.Sum(g => g.Count());
                if (total == 0) continue;

                string top = string.Join(", ", picks.Take(3)
                    .Select(g => $"{RoleDatabase.Get(g.Key).displayName} {g.Count() * 100 / total}%"));
                Console.WriteLine($"    {pos,-4} {picks.Count,2}가지 — {top}");
            }
        }

        enum RoleMode { Convention, BestFit, WorstFit, AllCreators, AllFinishers }

        static (GameWorld w, ClubData a, ClubData b) RolePair()
        {
            var w = WorldGenerator.Create(31415);
            var ranked = w.Clubs.Values
                .Where(c => c.playerIds.Count >= 16)
                .OrderByDescending(c => w.Strength(c.id))
                .ToList();

            var best = (a: ranked[0], b: ranked[1], gap: float.MaxValue);
            for (int i = 0; i + 1 < ranked.Count; i++)
            {
                float gap = Math.Abs(w.Strength(ranked[i].id) - w.Strength(ranked[i + 1].id));
                if (gap < best.gap) best = (ranked[i], ranked[i + 1], gap);
            }
            return (w, best.a, best.b);
        }

        /// <summary>a 가 역할을 mode 대로 고르고, b 는 늘 관례값을 쓴다.</summary>
        static (double ppg, double avgFit) RoleDuel(RoleMode mode, int matches)
        {
            var (w, ca, cb) = RolePair();
            var la = RoleLineup(w, ca, mode);
            var lb = RoleLineup(w, cb, RoleMode.Convention);

            double fitSum = 0; int fitN = 0;
            foreach (var s in la.tactics.slots)
            {
                var p = w.Player(s.assignedPlayerId);
                if (p == null || s.role == null) continue;
                if (!RoleDatabase.All.TryGetValue(s.role.role, out var def)) continue;
                fitSum += RoleFit.Percent(p, def); fitN++;
            }

            var rng = new Random(5150);
            int pts = 0;
            for (int m = 0; m < matches; m++)
            {
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

            return ((double)pts / matches, fitN == 0 ? 0 : fitSum / fitN);
        }

        static MatchLineup RoleLineup(GameWorld w, ClubData c, RoleMode mode)
        {
            var tac = FormationDatabase.CreateTactics("4-2-3-1", "역할 검사");
            var squad = w.Squad(c.id).ToList();
            LineupBuilder.Assign(tac, squad);
            RoleDefaults.Fill(tac);

            if (mode != RoleMode.Convention)
            {
                foreach (var slot in tac.slots)
                {
                    var p = w.Player(slot.assignedPlayerId);
                    if (p == null) continue;

                    var options = RoleFit.For(slot.position);
                    if (options.Count == 0) continue;

                    PlayerRole pick;
                    if (mode == RoleMode.BestFit)
                        pick = options.OrderByDescending(r => RoleFit.Score(p, RoleDatabase.Get(r))).First();
                    else if (mode == RoleMode.WorstFit)
                        pick = options.OrderBy(r => RoleFit.Score(p, RoleDatabase.Get(r))).First();
                    else
                    {
                        var want = mode == RoleMode.AllCreators
                            ? RoleFit.Archetype.Creator : RoleFit.Archetype.Finisher;
                        var typed = options.Where(r => RoleFit.TypeOf(RoleDatabase.Get(r)) == want).ToList();
                        pick = (typed.Count > 0 ? typed : options)
                            .OrderByDescending(r => RoleFit.Score(p, RoleDatabase.Get(r))).First();
                    }

                    slot.role = new RoleAssignment
                    {
                        role = pick,
                        duty = RoleFit.DefaultDuty(RoleDatabase.Get(pick)),
                    };
                }
            }

            return new MatchLineup
            {
                clubId = c.id,
                tactics = tac,
                benchIds = LineupBuilder.Bench(tac, squad),
            };
        }


        /// <summary>
        /// 세트피스가 새로운 축인지 본다.
        ///
        /// (1) 제공권이 좋은 팀이 세트피스로 더 넣어야 한다 — 아니면 키 큰 선수를 모을 이유가 없다.
        /// (2) 키커가 좋으면 차이가 나야 한다.
        /// (3) 배달 방식은 거래여야 한다 — 어느 하나가 늘 최선이면 고를 이유가 없다.
        /// </summary>
        public static void SetPiece(int matches)
        {
            Console.WriteLine($"=== 세트피스 ({matches}경기) ===");

            var w = WorldGenerator.Create(31415);
            var clubs = w.Clubs.Values.Where(c => c.playerIds.Count >= 16).ToList();

            float Aerial(ClubData c)
            {
                var tac = c.savedTactics.First(t => t.id == c.activeTacticsId);
                return SetPieces.AerialThreat(tac.slots.Select(s => w.Player(s.assignedPlayerId)).ToArray());
            }

            var tall = clubs.OrderByDescending(Aerial).First();
            var small = clubs.OrderBy(Aerial).First();
            Console.WriteLine($"  제공권 최고 {tall.name} {Aerial(tall):F1} · 최저 {small.name} {Aerial(small):F1}");

            // 전력이 비슷한 두 팀을 제공권으로만 갈라 본다
            var byStrength = clubs.OrderByDescending(c => w.Strength(c.id)).ToList();
            var mid = byStrength.Skip(byStrength.Count / 2 - 12).Take(24).ToList();
            var tallMid = mid.OrderByDescending(Aerial).First();
            var smallMid = mid.OrderBy(Aerial).First();

            Console.WriteLine();
            Console.WriteLine("  전력이 비슷한 두 팀, 제공권만 다름");
            Console.WriteLine($"    {tallMid.name,-26} 제공권 {Aerial(tallMid):F1} · 전력 {w.Strength(tallMid.id):F0}");
            Console.WriteLine($"    {smallMid.name,-26} 제공권 {Aerial(smallMid):F1} · 전력 {w.Strength(smallMid.id):F0}");

            var (aGoals, aSet) = SetPieceDuel(w, tallMid, smallMid, matches);
            Console.WriteLine($"    큰 팀 경기당 {aGoals:F2}골 (세트피스 {aSet:F2})");
            var (bGoals, bSet) = SetPieceDuel(w, smallMid, tallMid, matches);
            Console.WriteLine($"    작은 팀 경기당 {bGoals:F2}골 (세트피스 {bSet:F2})");

            // 배달 방식별
            Console.WriteLine();
            // 상대에 따라 최선이 바뀌어야 고를 이유가 생긴다.
            Console.WriteLine("  배달 방식 × 제공권 상황 (코너 하나의 기대 득점)");
            Console.Write("    상황                      ");
            foreach (var d in Enum.GetValues<SetPieceDelivery>()) Console.Write($"{DeliveryName(d),12}");
            Console.WriteLine();

            var cases = new (string name, float threat, float defence, float gk, float move, float taker)[]
            {
                ("장신 · 평범한 GK",      17f, 12f, 15f, 1.0f, 1.0f),
                ("비슷 · 평범한 GK",      13f, 13f, 15f, 1.0f, 1.0f),
                ("단신 · 평범한 GK",      12f, 17f, 15f, 1.0f, 1.0f),
                ("단신 · 잘 움직임",      12f, 17f, 15f, 1.25f, 1.0f),
                ("비슷 · 박스 장악 GK",   13f, 13f, 19f, 1.0f, 1.0f),
                ("비슷 · 최고의 키커",    13f, 13f, 15f, 1.0f, 1.25f),
                ("장신 · 박스 장악 GK",   16f, 12f, 19f, 1.0f, 1.0f),
            };

            var bestPer = new Dictionary<SetPieceDelivery, int>();
            foreach (var (name, threat, defence, gk, move, taker) in cases)
            {
                Console.Write($"    {name,-22}");
                SetPieceDelivery best = SetPieceDelivery.Inswinger;
                float bestXg = -1f;

                foreach (var d in Enum.GetValues<SetPieceDelivery>())
                {
                    float xg = SetPieces.Xg(SetPieces.Kind.Corner, taker, threat, defence, d, gk, move);
                    Console.Write($"{xg,12:F4}");
                    if (xg > bestXg) { bestXg = xg; best = d; }
                }

                bestPer[best] = bestPer.GetValueOrDefault(best) + 1;
                Console.WriteLine($"   → {DeliveryName(best)}");
            }

            Console.WriteLine("    (짧게 는 기대 득점이 낮은 대신 공을 지켜 열린 찬스로 이어진다 — 이 표에 안 나온다)");
            Console.WriteLine($"    최선이 되는 방식 {bestPer.Count}가지 " +
                              $"({string.Join(", ", bestPer.Select(k => DeliveryName(k.Key)))})");

            // 키커 품질
            Console.WriteLine();
            Console.WriteLine("  키커 능력치 → 배달 품질");
            foreach (int v in new[] { 6, 10, 13, 16, 19 })
            {
                var t = new PlayerData
                {
                    technical = new TechnicalAttributes { setPieces = v, crossing = v, finishing = v },
                    mental = new MentalAttributes { composure = v },
                };
                Console.WriteLine($"    {v,2} → 코너·프리킥 {SetPieces.TakerQuality(t, SetPieces.Kind.Corner):F2} · " +
                                  $"페널티 {SetPieces.TakerQuality(t, SetPieces.Kind.Penalty):F2}");
            }
        }

        static string DeliveryName(SetPieceDelivery d) => d switch
        {
            SetPieceDelivery.Inswinger => "인스윙",
            SetPieceDelivery.Outswinger => "아웃스윙",
            SetPieceDelivery.ShortPass => "짧게",
            SetPieceDelivery.DrivenLow => "낮고 빠르게",
            _ => "높이 띄워",
        };

        static (double goals, double setGoals) SetPieceDuel(
            GameWorld w, ClubData a, ClubData b, int matches)
        {
            MatchLineup Make(ClubData c)
            {
                var tac = FormationDatabase.CreateTactics("4-2-3-1", "세트피스 검사");
                var squad = w.Squad(c.id).ToList();
                LineupBuilder.Assign(tac, squad);
                RoleDefaults.Fill(tac);
                return new MatchLineup { clubId = c.id, tactics = tac, benchIds = LineupBuilder.Bench(tac, squad) };
            }

            var la = Make(a);
            var lb = Make(b);
            var rng = new Random(2468);
            int goals = 0, setGoals = 0;

            for (int m = 0; m < matches; m++)
            {
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

                foreach (var e in r.events)
                {
                    if (e.clubId != a.id) continue;
                    switch (e.eventType)
                    {
                        case MatchEventType.Goal: goals++; break;
                        case MatchEventType.CornerKickGoal:
                        case MatchEventType.FreeKickGoal:
                        case MatchEventType.PenaltyScored: goals++; setGoals++; break;
                    }
                }
            }

            return ((double)goals / matches, (double)setGoals / matches);
        }


        /// <summary>
        /// 선수 개인 목표 · 이적 요청 · 국가대표.
        ///
        /// (1) 목표는 처지에 따라 달라야 한다 — 전원이 같은 것을 바라면 의미가 없다.
        /// (2) 요청은 방치한 선수에게서만 나와야 한다 — 전원이 떠나려 하면 게임이 안 된다.
        /// (3) 대표 소집은 좋은 선수에게 집중되고 체력을 깎아야 한다.
        /// </summary>
        public static void Squad(int seasons)
        {
            Console.WriteLine($"=== 선수 목표 · 이적 요청 · 국가대표 ({seasons}시즌) ===");

            var w = WorldGenerator.Create(31415);
            var club = w.Clubs.Values.First(c => c.reputation == 4);
            ManagerCareer.TakeCharge(w, club.id, "측정");
            Console.WriteLine($"  구단: {club.name}");

            // 목표 분포
            var kinds = new Dictionary<PlayerAmbition.Kind, int>();
            foreach (var p in w.Squad(club.id))
                foreach (var g in PlayerAmbition.For(w, p))
                    kinds[g.Kind] = kinds.GetValueOrDefault(g.Kind) + 1;

            Console.WriteLine();
            Console.WriteLine("  스쿼드가 바라는 것");
            foreach (var (kind, n) in kinds.OrderByDescending(x => x.Value))
                Console.WriteLine($"    {kind,-13} {n,2}명");

            // 대표 발탁선
            Console.WriteLine();
            Console.WriteLine("  나라별 대표 발탁선 (CA)");
            foreach (var nation in w.Nations)
            {
                var code = NameForge.ByCode(nation.Code).Nationality;
                Console.WriteLine($"    {nation.Country,-8} {NationalTeam.Threshold(w, code):F0} · " +
                                  $"명단 {NationalTeam.Squad(w, code).Count}명");
            }

            // 시즌 진행
            Console.WriteLine();
            Console.WriteLine("  시즌  요청  거절누적  평균사기  화합도  대표소집  평균출전");

            int totalRequests = 0, rejected = 0;
            for (int s = 0; s < seasons; s++)
            {
                int guard = 0;
                while (!w.SeasonFinished && guard++ < 60)
                {
                    if (GameEngine.PlayNextRound(w) == null) break;

                    // 요청이 오면 절반은 거절한다 — 양쪽 결과를 다 본다.
                    foreach (var id in w.TransferRequests.Keys.ToList())
                    {
                        var p = w.Player(id);
                        if (p == null) continue;
                        totalRequests++;
                        if (totalRequests % 2 == 0) { TransferRequests.Reject(w, p); rejected++; }
                        else TransferRequests.Accept(w, p);
                    }
                }

                var squad = w.Squad(club.id).ToList();
                int called = squad.Count(p => p.caps > 0);

                Console.WriteLine($"  {w.SeasonYear,4}  {totalRequests,4}  {rejected,8}  " +
                                  $"{squad.Average(p => p.morale),8:F1}  " +
                                  $"{club.dressingRoom.overallHarmony,6:F1}  " +
                                  $"{called,8}  " +
                                  $"{squad.Average(p => p.seasonStats?.appearances ?? 0),8:F1}");

                if (s < seasons - 1) SeasonRollover.StartNextSeason(w);
            }

            // 임계를 데이터에서 정해야 한다. 지금 스쿼드의 압력이 얼마나 되는지 본다.
            Console.WriteLine();
            Console.WriteLine("  압력 분포 (임계 " + BalanceConfig.RequestThreshold + ")");
            foreach (var p in w.Squad(club.id)
                         .OrderByDescending(p => TransferRequests.Pressure(w, p))
                         .Take(8))
            {
                var goals = PlayerAmbition.For(w, p);
                Console.WriteLine($"    {p.FullName,-22} 압력 {TransferRequests.Pressure(w, p),5:F1} · " +
                                  $"출전 {p.seasonStats?.appearances ?? 0,2} · 사기 {p.morale,4:F0} · " +
                                  $"목표 {goals.Count(g => !g.Met)}/{goals.Count} 미달");
            }

            // 요청한 선수들이 정말 방치된 선수인가
            Console.WriteLine();
            Console.WriteLine("  지금 요청 중인 선수");
            foreach (var (id, req) in w.TransferRequests.Take(6))
            {
                var p = w.Player(id);
                if (p == null) continue;
                Console.WriteLine($"    {p.FullName,-24} 출전 {p.seasonStats?.appearances ?? 0,2} · " +
                                  $"사기 {p.morale,4:F0} · 압력 {TransferRequests.Pressure(w, p):F1} · \"{req.reason}\"");
            }

            // 대표 경력 분포
            Console.WriteLine();
            var capped = w.Players.Values.Where(p => p.clubId > 0 && p.caps > 0).ToList();
            var all = w.Players.Values.Where(p => p.clubId > 0).ToList();
            Console.WriteLine($"  대표 경력 보유 {capped.Count}/{all.Count}명 " +
                              $"({capped.Count * 100.0 / all.Count:F0}%)");
            if (capped.Count > 0)
            {
                Console.WriteLine($"    보유자 평균 CA {capped.Average(GameWorld.Ca):F0} · " +
                                  $"미보유 평균 CA {all.Where(p => p.caps == 0).Average(GameWorld.Ca):F0}");
                Console.WriteLine($"    최다 출전 {capped.Max(p => p.caps)}회 · " +
                                  $"대표 득점 합계 {capped.Sum(p => p.internationalGoals)}");
            }

            // 소집이 체력을 깎는가
            Console.WriteLine();
            var probe = WorldGenerator.Create(31415);
            var before = probe.Players.Values.Where(p => p.clubId > 0).Average(p => p.condition);
            NationalTeam.RunBreak(probe, new Random(7));
            var after = probe.Players.Values.Where(p => p.clubId > 0).Average(p => p.condition);
            var pickedNow = probe.Players.Values.Count(p => p.caps > 0);
            Console.WriteLine($"  A매치 한 번 — 소집 {pickedNow}명 · 전체 평균 컨디션 {before:F1} → {after:F1}");
        }


        /// <summary>
        /// AI 영입 안목.
        ///
        /// (1) 시즌이 지날수록 스쿼드가 자기 시스템에 맞아야 한다.
        /// (2) 큰 구단이 작은 구단보다 잘 맞춰야 한다 — 똑같으면 안목이 의미가 없다.
        /// (3) 리그가 쏠리면 안 된다. 잘 뽑는 구단이 다 가져가면 경쟁이 사라진다.
        /// </summary>
        public static void Recruit(int seasons)
        {
            Console.WriteLine($"=== AI 영입 안목 ({seasons}시즌) ===");

            var w = WorldGenerator.Create(31415);
            var club = w.Clubs.Values.First(c => c.reputation == 3);
            ManagerCareer.TakeCharge(w, club.id, "측정");

            // 색깔 분포
            var styles = w.Clubs.Values
                .Select(c => AiRecruiting.StyleOf(
                    c.savedTactics?.FirstOrDefault(t => t.id == c.activeTacticsId)?.instructions))
                .GroupBy(s => s)
                .OrderByDescending(g => g.Count())
                .ToList();

            Console.WriteLine("  전술 색깔 분포");
            foreach (var g in styles)
                Console.WriteLine($"    {g.Key,-16} {g.Count(),3}구단");

            float SquadFit(ClubData c)
            {
                var tac = c.savedTactics?.FirstOrDefault(t => t.id == c.activeTacticsId);
                if (tac == null) return 1f;

                var style = AiRecruiting.StyleOf(tac.instructions);
                var squad = w.Squad(c.id).ToList();
                return squad.Count == 0 ? 1f : squad.Average(p => AiRecruiting.StyleFit(p, style));
            }

            double Spread()
            {
                var top = w.Leagues.Values.First(l => l.tier == LeagueTier.First);
                var powers = top.clubIds.Select(id => w.Strength(id)).OrderByDescending(x => x).ToList();
                return powers.Take(5).Sum() / powers.Sum();   // 상위 5팀의 전력 점유
            }

            Console.WriteLine();
            Console.WriteLine("  시즌  색깔적합도(별5)  (별3)  (별1)  상위5팀 전력점유");

            for (int s = 0; s <= seasons; s++)
            {
                double Fit(int stars) => w.Clubs.Values
                    .Where(c => c.reputation == stars)
                    .Average(SquadFit);

                Console.WriteLine($"  {w.SeasonYear,4}  {Fit(5),14:F3}  {Fit(3),5:F3}  {Fit(1),5:F3}  " +
                                  $"{Spread() * 100,14:F1}%");

                if (s == seasons) break;

                int guard = 0;
                while (!w.SeasonFinished && guard++ < 60)
                    if (GameEngine.PlayNextRound(w) == null) break;
                SeasonRollover.StartNextSeason(w);
            }

            // 적합도 입력이 선수마다 갈리는지부터 본다. 안 갈리면 점수도 안 갈린다.
            Console.WriteLine();
            Console.WriteLine("  색깔 적합도 분포 (1부 CM)");
            var cms = w.Players.Values
                .Where(p => p.clubId > 0 && (p.positionFlags & Position.CM) != 0
                         && w.TierOf(p.clubId) == LeagueTier.First)
                .ToList();
            foreach (var st in Enum.GetValues<AiTactics.Style>())
            {
                var fits = cms.Select(p => AiRecruiting.StyleFit(p, st)).OrderBy(x => x).ToList();
                if (fits.Count == 0) continue;
                Console.WriteLine($"    {st,-16} 최저 {fits[0]:F3} · 중앙 {fits[fits.Count / 2]:F3} · 최고 {fits[^1]:F3}");
            }

            // 안목이 실제 후보군에서 선택을 바꾸는 빈도. 한 줄짜리 예시로는 알 수 없다.
            Console.WriteLine();
            int changed = 0, checkedPools = 0;
            foreach (var c in w.Clubs.Values.Where(x => x.reputation >= 3).Take(80))
            {
                foreach (var pos in new[] { Position.CB, Position.CM, Position.ST, Position.LW })
                {
                    var cands = w.Players.Values
                        .Where(p => p.clubId > 0 && p.clubId != c.id && (p.positionFlags & pos) != 0)
                        .OrderByDescending(GameWorld.Ca)
                        .Take(10)
                        .ToList();
                    if (cands.Count < 2) continue;

                    var plain = cands.OrderByDescending(GameWorld.Ca).First();
                    var smart = cands.OrderByDescending(p => AiRecruiting.Score(w, c, p, pos, AiRecruiting.SkillOf(c))).First();
                    checkedPools++;
                    if (plain != smart) changed++;
                }
            }
            {
                var gaps = new List<double>();
                var leans = new List<double>();
                foreach (var c in w.Clubs.Values.Where(x => x.reputation >= 3).Take(80))
                foreach (var pos in new[] { Position.CB, Position.CM, Position.ST, Position.LW })
                {
                    var pl = w.Players.Values
                        .Where(p => p.clubId > 0 && p.clubId != c.id && (p.positionFlags & pos) != 0)
                        .OrderByDescending(GameWorld.Ca).Take(10).ToList();
                    if (pl.Count < 2) continue;
                    gaps.Add((GameWorld.Ca(pl[0]) - GameWorld.Ca(pl[^1])) / GameWorld.Ca(pl[0]));
                    var st = AiRecruiting.StyleOf(c.savedTactics!.First(t => t.id == c.activeTacticsId).instructions);
                    leans.Add(pl.Max(p => AiRecruiting.StyleFit(p, st)) - pl.Min(p => AiRecruiting.StyleFit(p, st)));
                }
                Console.WriteLine($"  후보 10명의 CA 격차 평균 {gaps.Average() * 100:F1}% · 적합도 폭 평균 {leans.Average() * 100:F1}%");
            }

            Console.WriteLine($"  안목이 선택을 바꾼 비율 {changed} / {checkedPools} ({changed * 100.0 / Math.Max(1, checkedPools):F0}%)");

            Console.WriteLine();
            Console.WriteLine("  같은 후보군을 두고 안목만 바꿨을 때 고르는 선수");
            var buyer = w.Clubs.Values.First(c => c.reputation == 5);
            var style2 = AiRecruiting.StyleOf(
                buyer.savedTactics!.First(t => t.id == buyer.activeTacticsId).instructions);
            Console.WriteLine($"    {buyer.name} · 색깔 {style2}");

            var pool = w.Players.Values
                .Where(p => p.clubId > 0 && p.clubId != buyer.id)
                .Where(p => (p.positionFlags & Position.CM) != 0)
                .OrderByDescending(GameWorld.Ca)
                .Take(40)
                .ToList();

            foreach (float skill in new[] { 0f, 0.5f, 1f })
            {
                var pick = pool.OrderByDescending(p => AiRecruiting.Score(w, buyer, p, Position.CM, skill)).First();
                Console.WriteLine($"    안목 {skill:F1} → {pick.FullName,-24} CA {GameWorld.Ca(pick),3:F0} · " +
                                  $"색깔적합 {AiRecruiting.StyleFit(pick, style2):F2}");
            }
        }

        public static void Run()
        {
            Console.WriteLine("=== 세계 생성 ===");
            var sw = Stopwatch.StartNew();
            var w = WorldGenerator.Create(20261017);
            sw.Stop();

            Console.WriteLine($"  생성 {sw.ElapsedMilliseconds} ms · " +
                              $"리그 {w.Leagues.Count} · 구단 {w.Clubs.Count} · 선수 {w.Players.Count}");

            foreach (var n in w.Nations)
            {
                var l = w.League(n.LeagueId);
                Console.WriteLine($"  [{n.Country}] {l.name} — {l.clubIds.Count}팀, " +
                                  $"{l.currentSeason.fixtures.Count}경기 / {l.currentSeason.totalMatchdays}라운드");
            }

            CheckSchedule(w);
            CheckStrengthLadder(w);
            CheckSave(w);
        }

        static void CheckSchedule(GameWorld w)
        {
            Console.WriteLine();
            Console.WriteLine("=== 일정 검증 ===");
            bool ok = true;

            foreach (var league in w.Leagues.Values)
            {
                var fx = league.currentSeason.fixtures;
                int expected = league.clubIds.Count * (league.clubIds.Count - 1);
                if (fx.Count != expected)
                {
                    Console.WriteLine($"  [실패] {league.shortName} 경기 수 {fx.Count}, 기대 {expected}");
                    ok = false;
                }

                // 모든 조합이 홈·원정 한 번씩
                var pairs = fx.Select(f => (f.homeClubId, f.awayClubId)).ToHashSet();
                if (pairs.Count != fx.Count)
                {
                    Console.WriteLine($"  [실패] {league.shortName} 중복 대진 {fx.Count - pairs.Count}건");
                    ok = false;
                }
                foreach (var (h, a) in pairs)
                    if (!pairs.Contains((a, h)))
                    {
                        Console.WriteLine($"  [실패] {league.shortName} 역경기 없음: {h} vs {a}");
                        ok = false;
                        break;
                    }

                // 한 라운드에 한 팀이 두 번 나오지 않는다
                foreach (var g in fx.GroupBy(f => f.matchday))
                {
                    var seen = g.SelectMany(f => new[] { f.homeClubId, f.awayClubId }).ToList();
                    if (seen.Count != seen.Distinct().Count())
                    {
                        Console.WriteLine($"  [실패] {league.shortName} {g.Key}라운드 중복 출전");
                        ok = false;
                        break;
                    }
                    if (g.Count() != league.clubIds.Count / 2)
                    {
                        Console.WriteLine($"  [실패] {league.shortName} {g.Key}라운드 경기 수 {g.Count()}");
                        ok = false;
                        break;
                    }
                }

                // 홈 경기 수 균형
                var homeCounts = league.clubIds.ToDictionary(
                    id => id, id => fx.Count(f => f.homeClubId == id));
                int min = homeCounts.Values.Min(), max = homeCounts.Values.Max();
                Console.WriteLine($"  {league.shortName}: {fx.Count}경기 · 홈 경기 {min}~{max}회");
                if (max - min > 2)
                {
                    Console.WriteLine($"  [실패] {league.shortName} 홈 경기 편차 {max - min}");
                    ok = false;
                }
            }
            Console.WriteLine($"  판정: {(ok ? "통과" : "실패")}");
        }

        static void CheckStrengthLadder(GameWorld w)
        {
            Console.WriteLine();
            Console.WriteLine("=== 평판별 전력 ===");
            foreach (var g in w.Clubs.Values.GroupBy(c => c.reputation).OrderByDescending(g => g.Key))
            {
                float str = g.Average(c => w.Strength(c.id));
                float wage = g.Average(c => (float)c.finances.currentWageSpend);
                float ratio = g.Average(c => c.finances.currentWageSpend * 52f / c.finances.annualRevenue);
                Console.WriteLine($"  별 {g.Key}: 전력 {str,6:F1} CA · 주급 합계 {wage / 1000f,8:F0}천€ · 급여비중 {ratio * 100f,5:F1}%");
            }

            Console.WriteLine();
            Console.WriteLine("  리그 등급별:");
            foreach (var league in w.Leagues.Values.OrderBy(l => l.regionName).ThenBy(l => (int)l.tier))
            {
                var strengths = league.clubIds.Select(id => w.Strength(id)).ToList();
                float rev = league.clubIds.Average(id => w.Club(id)!.finances.annualRevenue / 1_000_000f);
                Console.WriteLine($"    {league.shortName,-4} {(int)league.tier}부 · 전력 {strengths.Min(),5:F0}~{strengths.Max():F0} " +
                                  $"(평균 {strengths.Average(),5:F1}) · 평균 수입 {rev,6:F1}M€");
            }

            var sample = w.Clubs.Values.First();
            Console.WriteLine($"  예시 구단: {sample.name} ({sample.stadium.name}, {sample.stadium.capacity:N0}석)");
            var top = w.Squad(sample.id).OrderByDescending(GameWorld.Ca).Take(3);
            foreach (var p in top)
                Console.WriteLine($"    {p.FullName,-22} {p.preferredPosition,-4} {w.Age(p)}세 " +
                                  $"CA {GameWorld.Ca(p),5:F1} / PA {PlayerDevelopment.Pa(p),5:F0} · {p.nationality}");
        }

        /// <summary>세이브에서 무엇이 자리를 차지하는지 쪼개 본다.</summary>
        public static void SaveBreakdown()
        {
            Console.WriteLine("=== 세이브 구성 ===");
            var w = WorldGenerator.Create(20261017);
            var club = w.Clubs.Values.First(c => c.reputation == 3);
            ManagerCareer.TakeCharge(w, club.id, "감사");

            void Report(string label)
            {
                var (raw, packed) = SaveGame.Measure(w);
                var sw = System.Diagnostics.Stopwatch.StartNew();
                SaveGame.Serialize(w, "");
                sw.Stop();
                Console.WriteLine($"  {label,-22} JSON {raw / 1024f / 1024f,5:F2} MB → gzip {packed / 1024f,5:F0} KB · {sw.ElapsedMilliseconds,4} ms");
            }

            Report("시즌 시작");

            // 무엇이 비용인지 떼어 본다. JSON 시간은 바이트가 아니라 객체 수를 따르므로
            // 어떤 묶음을 없애면 얼마나 빨라지는지 직접 재는 쪽이 정확하다.
            Console.WriteLine();
            Console.WriteLine("  부분을 떼어 봤을 때 (시즌 시작 기준)");

            var players = w.Players.Values.ToList();
            var contracts = players.ToDictionary(p => p.id, p => p.contract);
            var stats = players.ToDictionary(p => p.id, p => p.seasonStats);
            var histories = w.Clubs.Values.ToDictionary(c => c.id, c => c.history);
            var tactics = w.Clubs.Values.ToDictionary(c => c.id, c => c.savedTactics);

            void Strip(string label, Action off, Action on)
            {
                off();
                var (raw, _) = SaveGame.Measure(w);
                var sw = System.Diagnostics.Stopwatch.StartNew();
                SaveGame.Serialize(w, "");
                sw.Stop();
                on();
                Console.WriteLine($"    {label,-18} JSON {raw / 1024f / 1024f,5:F2} MB · {sw.ElapsedMilliseconds,4} ms");
            }

            Strip("계약 제거",
                () => { foreach (var p in players) p.contract = null; },
                () => { foreach (var p in players) p.contract = contracts[p.id]; });

            Strip("시즌기록 제거",
                () => { foreach (var p in players) p.seasonStats = null; },
                () => { foreach (var p in players) p.seasonStats = stats[p.id]; });

            Strip("구단 이력 제거",
                () => { foreach (var c in w.Clubs.Values) c.history = null; },
                () => { foreach (var c in w.Clubs.Values) c.history = histories[c.id]; });

            Strip("저장 전술 제거",
                () => { foreach (var c in w.Clubs.Values) c.savedTactics = null; },
                () => { foreach (var c in w.Clubs.Values) c.savedTactics = tactics[c.id]; });

            Strip("선수 전부 제거",
                () => { foreach (var p in players) { p.contract = null; p.seasonStats = null; } },
                () => { foreach (var p in players) { p.contract = contracts[p.id]; p.seasonStats = stats[p.id]; } });

            Console.WriteLine();

            int guard = 0;
            while (!w.SeasonFinished && guard++ < 60)
                if (GameEngine.PlayNextRound(w) == null) break;

            Report("한 시즌 뒤");

            int events = w.Leagues.Values
                .SelectMany(l => l.currentSeason.fixtures)
                .Where(f => f.result != null)
                .Sum(f => f.result.events?.Count ?? 0);
            Console.WriteLine($"  저장된 경기 이벤트 {events:N0}건 · 치른 경기 " +
                              $"{w.Leagues.Values.Sum(l => l.currentSeason.fixtures.Count(f => f.isPlayed)):N0}");

            // 이벤트를 버리면 얼마나 줄어드는지
            foreach (var f in w.Leagues.Values.SelectMany(l => l.currentSeason.fixtures))
                if (f.result != null) { f.result.events = null; }
            Report("이벤트 제거 후");
        }

        static void CheckSave(GameWorld w)
        {
            Console.WriteLine();
            Console.WriteLine("=== 세이브 ===");
            var sw = Stopwatch.StartNew();
            var (raw, packed) = SaveGame.Measure(w);
            byte[] bytes = SaveGame.Serialize(w, "테스트");
            sw.Stop();

            var back = SaveGame.Deserialize(bytes).world;
            Console.WriteLine($"  JSON {raw / 1024f / 1024f:F2} MB → gzip {packed / 1024f:F0} KB " +
                              $"({(1 - packed / (float)raw) * 100f:F1}% 감소, {sw.ElapsedMilliseconds} ms)");

            bool ok = back.Clubs.Count == w.Clubs.Count
                   && back.Players.Count == w.Players.Count
                   && back.Leagues.Count == w.Leagues.Count;

            var a = w.Players.Values.First();
            var b = back.Player(a.id);
            ok &= b != null && b.FullName == a.FullName
               && Math.Abs(b.AbilityScore - a.AbilityScore) < 0.001f;

            var la = w.Leagues.Values.First();
            var lb = back.League(la.id);
            ok &= lb.currentSeason.fixtures.Count == la.currentSeason.fixtures.Count;

            Console.WriteLine($"  왕복 검증: {(ok ? "통과" : "실패")}");
            if (packed > 4 * 1024 * 1024)
                Console.WriteLine("  [경고] 5MB 브라우저 저장소 한도에 근접합니다.");
        }

        /// <summary>
        /// 전력 순위와 실제 순위가 맞는지, 그리고 플레이어 구단이 불리하지 않은지 본다.
        /// 한 시즌 한 구단만 보고는 분산과 구조적 문제를 구분할 수 없다.
        /// </summary>
        /// <summary>
        /// 이적 시장을 여러 시즌 돌려 돈과 전력이 한쪽으로 쏠리지 않는지 본다.
        /// 쏠리면 몇 시즌 만에 상위 구단이 리그 전체를 사들인다.
        /// </summary>
        public static void Market(int seasons)
        {
            Console.WriteLine($"=== 이적 시장 {seasons}시즌 ===");
            var w = WorldGenerator.Create(31415);
            var league = w.League(w.Nations[0].LeagueId)!;

            var club = league.clubIds.Select(id => w.Club(id)!).First(c => c.reputation == 3);
            ManagerCareer.TakeCharge(w, club.id, "감사");

            float StrengthSpread()
            {
                var s = league.clubIds.Select(id => w.Strength(id)).ToList();
                return s.Max() - s.Min();
            }
            float TopFiveShare()
            {
                var ranked = league.clubIds.OrderByDescending(id => w.Strength(id)).ToList();
                float top = ranked.Take(5).Sum(id => w.Strength(id));
                float all = ranked.Sum(id => w.Strength(id));
                return top / all;
            }

            Console.WriteLine($"  시작 — 전력 폭 {StrengthSpread():F1} · 상위5 비중 {TopFiveShare() * 100:F1}%");

            int totalMoves = 0;
            int lastOpenWeeks = 0;
            TransferMarket.Diagnostics = new Dictionary<string, int>();
            for (int s = 0; s < seasons; s++)
            {
                int before = w.TotalTransfers;

                int openWeeks = 0;
                int guard = 0;
                while (!w.SeasonFinished && guard++ < 60)
                {
                    if (GameEngine.PlayNextRound(w) == null) break;
                    if (TransferMarket.IsWindowOpen(w)) openWeeks++;
                }

                // 다음 시즌 창구까지 넘긴다
                for (int week = 0; week < 14; week++)
                {
                    WeekCycle.Advance(w, w.Date.AddDays(7));
                    if (TransferMarket.IsWindowOpen(w)) openWeeks++;
                }
                lastOpenWeeks = openWeeks;

                int moves = w.TotalTransfers - before;
                totalMoves += moves;

                var pos = Standings.PositionOf(league, club.id);
                var squads = w.Clubs.Values.Select(c => c.playerIds.Count).ToList();
                float avgRatio = w.Clubs.Values.Average(c =>
                    c.finances.currentWageSpend * 52f / Math.Max(1, c.finances.annualRevenue));
                int canBuy = w.Clubs.Values.Count(c => c.finances.transferBudget > 1_000_000);

                Console.WriteLine($"  시즌 {s + 1}: 이적 {moves,3}건 · 전력 폭 {StrengthSpread():F1} · " +
                                  $"상위5 비중 {TopFiveShare() * 100:F1}% · 내 구단 {pos,2}위 · " +
                                  $"스쿼드 {squads.Min()}~{squads.Max()} · 급여비중 {avgRatio * 100:F0}% · " +
                                  $"예산보유 {canBuy}/{w.Clubs.Count} · 창구 {lastOpenWeeks}주");

                SeasonRollover.StartNextSeason(w);
            }

            Console.WriteLine();
            Console.WriteLine("  거절 사유 (전 시즌 누적):");
            foreach (var kv in TransferMarket.Diagnostics.OrderByDescending(k => k.Value).Take(8))
                Console.WriteLine($"    {kv.Key,-28} {kv.Value,6}");
            TransferMarket.Diagnostics = null;

            Console.WriteLine();
            Console.WriteLine($"  총 이적 {w.TotalTransfers}건 (시즌당 {w.TotalTransfers / (double)seasons:F1})");

            var biggest = w.Transfers.OrderByDescending(t => t.fee).Take(5);
            Console.WriteLine("  최고 이적료:");
            foreach (var t in biggest)
                Console.WriteLine($"    {t.playerName,-24} {w.Club(t.fromClubId)?.shortName} → " +
                                  $"{w.Club(t.toClubId)?.shortName}  {t.fee / 1_000_000d,6:F1}M€");

            Console.WriteLine("  구단 재정:");
            foreach (var c in league.clubIds.Select(id => w.Club(id)!)
                         .OrderByDescending(c => c.finances.balance).Take(3)
                         .Concat(league.clubIds.Select(id => w.Club(id)!)
                             .OrderBy(c => c.finances.balance).Take(3)))
                Console.WriteLine($"    별{c.reputation} {c.name,-26} 자금 {c.finances.balance / 1_000_000d,8:F1}M€ · " +
                                  $"급여비중 {c.finances.currentWageSpend * 52f / c.finances.annualRevenue * 100,5:F1}%");
        }

        /// <summary>컵이 끝까지 진행되고 우승 팀이 나오는지 본다.</summary>
        public static void Cup(int seasons)
        {
            Console.WriteLine($"=== 국내 컵 {seasons}시즌 ===");
            var w = WorldGenerator.Create(20261017);
            var club = w.Clubs.Values.First(c => c.reputation == 4);
            ManagerCareer.TakeCharge(w, club.id, "감사");

            foreach (var c in w.Cups.Values.OrderBy(c => c.continental))
            {
                if (c.HasGroups)
                {
                    Console.WriteLine($"  {c.name} — 참가 {c.entrants.Count}팀 · " +
                                      $"{c.groups.Count}개조 × {c.groups[0].clubIds.Count}팀 · " +
                                      $"조별 {c.groups.Sum(g => g.fixtures.Count)}경기 · " +
                                      $"주차 {string.Join(",", CupCompetition.GroupMatchdays)} → " +
                                      $"{string.Join(",", c.roundMatchdays)}");

                    foreach (var g in c.groups)
                        Console.WriteLine($"      {g.name}: " + string.Join(", ",
                            g.clubIds.Select(id => $"{w.Club(id)?.shortName}({w.NationOf(id)?.Code})")));
                    continue;
                }

                Console.WriteLine($"  {c.name} — 참가 {c.entrants.Count}팀 · 1라운드 {c.rounds[0].ties.Count}경기 · " +
                                  $"부전승 {c.PendingByes.Count}팀 · 주차 {string.Join(",", c.roundMatchdays)}");
            }

            var tierWins = new Dictionary<LeagueTier, int>();

            for (int s = 0; s < seasons; s++)
            {
                int guard = 0;
                while (!w.SeasonFinished && guard++ < 60)
                    if (GameEngine.PlayNextRound(w) == null) break;

                foreach (var cup in w.Cups.Values)
                {
                    if (!cup.Finished)
                    {
                        Console.WriteLine($"  [실패] {cup.name} 이 끝나지 않았다 " +
                                          $"(라운드 {cup.currentRound}/{cup.rounds.Count})");
                        continue;
                    }
                    var tier = w.TierOf(cup.winnerClubId);
                    tierWins[tier] = tierWins.GetValueOrDefault(tier) + 1;
                }

                var mine = CupCompetition.UserCup(w);
                string mineResult = mine == null ? "—"
                    : mine.winnerClubId == club.id ? "우승"
                    : RoundReached(mine, club.id);

                Console.WriteLine($"  시즌 {s + 1}: " +
                                  string.Join(" · ", w.Cups.Values.Select(c =>
                                      $"{c.shortName} {w.Club(c.winnerClubId)?.shortName ?? "없음"}")) +
                                  $" · 내 구단 {mineResult}");

                for (int week = 0; week < 14; week++)
                    WeekCycle.Advance(w, w.Date.AddDays(7));
                SeasonRollover.StartNextSeason(w);
            }

            Console.WriteLine();
            Console.WriteLine("  우승 구단 등급:");
            foreach (var kv in tierWins.OrderBy(k => (int)k.Key))
                Console.WriteLine($"    {(int)kv.Key}부 {kv.Value}회");
        }

        static string RoundReached(CupState cup, int clubId)
        {
            for (int i = cup.rounds.Count - 1; i >= 0; i--)
            {
                var tie = cup.rounds[i].ties.FirstOrDefault(
                    t => t.homeClubId == clubId || t.awayClubId == clubId);
                if (tie != null) return $"{cup.rounds[i].name} 탈락";
            }
            return "부전승 후 탈락";
        }

        /// <summary>
        /// 훈련 강도가 실제로 거래 관계를 만드는지 본다.
        /// 강하게 굴리면 빨리 크되 컨디션이 떨어지고 부상이 늘어야 한다.
        /// </summary>
        public static void Training()
        {
            Console.WriteLine("=== 훈련 강도 (한 시즌) ===");
            Console.WriteLine("  강도        성장 CA   시즌말 컨디션   부상자수   최종순위");

            foreach (int intensity in new[] { 1, 2, 3, 4, 5 })
            {
                var w = WorldGenerator.Create(515151);
                var club = w.Clubs.Values.First(c => c.reputation == 3);
                ManagerCareer.TakeCharge(w, club.id, "감사");
                w.Training.Intensity = intensity;

                float caBefore = w.Squad(club.id).Average(GameWorld.Ca);
                int injuries = 0;

                int guard = 0;
                while (!w.SeasonFinished && guard++ < 60)
                {
                    if (GameEngine.PlayNextRound(w) == null) break;
                    injuries += w.Squad(club.id).Count(p => p.IsInjured);
                }

                float caAfter = w.Squad(club.id).Average(GameWorld.Ca);
                float cond = w.Squad(club.id).Average(p => p.condition);
                int pos = Standings.PositionOf(w.MyLeague, club.id);

                Console.WriteLine($"  {TrainingPlan.IntensityLabel(intensity),-10} " +
                                  $"{caAfter - caBefore,+7:F1}   {cond,10:F1}   " +
                                  $"{injuries / 38.0,8:F1}   {pos,6}위");
            }

            Console.WriteLine();
            Console.WriteLine("=== 훈련 중점 (유스 육성) ===");
            foreach (var focus in new[] { TrainingFocus.Balanced, TrainingFocus.Youth })
            {
                var w = WorldGenerator.Create(626262);
                var club = w.Clubs.Values.First(c => c.reputation == 3);
                ManagerCareer.TakeCharge(w, club.id, "감사");
                w.Training.Focus = focus;

                var young = w.Squad(club.id).Where(p => w.Age(p) <= 21).Select(p => p.id).ToList();
                var before = young.ToDictionary(id => id, id => GameWorld.Ca(w.Player(id)!));

                int guard = 0;
                while (!w.SeasonFinished && guard++ < 60)
                    if (GameEngine.PlayNextRound(w) == null) break;

                float gain = young.Count == 0 ? 0
                    : young.Average(id => GameWorld.Ca(w.Player(id)!) - before[id]);
                Console.WriteLine($"  {TrainingPlan.FocusLabel(focus),-10} U21 {young.Count}명 평균 성장 {gain:+0.0}");
            }
        }

        /// <summary>
        /// 유스와 임대. 임대가 성장으로 이어지지 않으면 보낼 이유가 없다.
        /// </summary>
        public static void Youth(int seasons)
        {
            Console.WriteLine($"=== 유스와 임대 {seasons}시즌 ===");
            var w = WorldGenerator.Create(70707);
            var club = w.Clubs.Values.First(c => c.reputation == 3);
            ManagerCareer.TakeCharge(w, club.id, "감사");

            Console.WriteLine($"  구단 {club.name} · 아카데미 {new string('★', club.academyRating)}");
            Console.WriteLine();
            Console.WriteLine("  시즌  유스승격  임대중  스쿼드  U21수  U21평균CA  최고잠재력");

            for (int s = 0; s < seasons; s++)
            {
                int guard = 0;
                while (!w.SeasonFinished && guard++ < 60)
                    if (GameEngine.PlayNextRound(w) == null) break;

                int before = w.Players.Count;
                SeasonRollover.StartNextSeason(w);
                int produced = w.Players.Count - before;

                var squad = w.Squad(club.id).ToList();
                var u21 = squad.Where(p => w.Age(p) <= 21).ToList();
                float bestPa = squad.Count == 0 ? 0 : squad.Max(PlayerDevelopment.Pa);

                Console.WriteLine($"  {s + 1,3}   {produced,7}   {w.Loans.Count,5}   " +
                                  $"{squad.Count,5}   {u21.Count,4}   " +
                                  $"{(u21.Count == 0 ? 0 : u21.Average(GameWorld.Ca)),8:F0}   {bestPa,9:F0}");
            }

            Console.WriteLine();
            Console.WriteLine("  임대 효과 — 같은 선수를 보냈을 때와 데리고 있을 때");
            CompareLoanGrowth();
        }

        static void CompareLoanGrowth()
        {
            // 1군에서 정말로 자리가 없는 선수를 골라야 비교가 성립한다.
            // 주전급을 고르면 "큰 구단에 남는 쪽이 낫다"는 당연한 결과만 나온다.
            (float gain, int apps, string where) Run(bool loanOut)
            {
                var w = WorldGenerator.Create(515253);
                var club = w.Clubs.Values.First(c => c.reputation == 5);
                ManagerCareer.TakeCharge(w, club.id, "감사");

                var ranked = w.Squad(club.id).OrderByDescending(GameWorld.Ca).ToList();
                var target = LoanMarket.Loanable(w, club.id)
                    .Where(p => ranked.IndexOf(p) >= 14)              // 확실한 후보 자원
                    .Where(p => LoanMarket.Suitors(w, p, 1).Count > 0) // 갈 곳이 있어야 비교가 된다
                    .OrderByDescending(p => PlayerDevelopment.Pa(p) - GameWorld.Ca(p))
                    .FirstOrDefault();
                if (target == null) return (0f, 0, "대상 없음");

                float before = GameWorld.Ca(target);
                string where = "원소속";

                if (loanOut)
                {
                    var host = LoanMarket.Suitors(w, target).FirstOrDefault();
                    if (host != null) { LoanMarket.Send(w, target, host); where = host.name; }
                }

                int guard = 0;
                while (!w.SeasonFinished && guard++ < 60)
                    if (GameEngine.PlayNextRound(w) == null) break;

                return (GameWorld.Ca(target) - before, target.seasonStats.appearances, where);
            }

            // 임대처가 얼마나 남아 있는지 — 한 명만 보면 "대상 없음"이 조건이 센 건지
            // 그 선수가 특이한 건지 구분되지 않는다.
            var probe = WorldGenerator.Create(515253);
            var pool = probe.Clubs.Values
                .SelectMany(c => LoanMarket.Loanable(probe, c.id))
                .ToList();
            int withSuitor = pool.Count(x => LoanMarket.Suitors(probe, x, 1).Count > 0);
            Console.WriteLine($"    임대 후보 {pool.Count}명 중 갈 곳이 있는 선수 {withSuitor}명 " +
                              $"({(pool.Count == 0 ? 0 : withSuitor * 100.0 / pool.Count):F0}%)");

            var kept = Run(false);
            var sent = Run(true);
            Console.WriteLine($"    데리고 있을 때 {kept.gain:+0.0} ({kept.apps}경기)");
            Console.WriteLine($"    임대 보냈을 때 {sent.gain:+0.0} ({sent.apps}경기, {sent.where})");
            Console.WriteLine($"    판정: {(sent.gain > kept.gain + 0.5f ? "임대가 성장에 도움이 된다" : "임대할 이유가 없다")}");
        }

        internal readonly record struct SeasonMetrics(
            double rankGap, double goalsPerMatch, int topPoints, int bottomPoints,
            int managedGap, double condition, double morale,
            double aiCondition, double aiMorale, int controlGap,
            double managedDrift, double aiDrift,
            string managedShort, int managedStrengthRank,
            int managedPosition);

        /// <summary>
        /// 한 시즌을 실제 게임 루프로 끝까지 돌리고 지표를 돌려준다.
        /// PlayNextRound 는 주간 회복·부상·성장까지 포함하므로, 측정도 반드시 이 경로를 써야 한다.
        /// 라운드만 돌리면 컨디션이 회복되지 않아 골키퍼 전력이 떨어지고 득점이 부풀려진다.
        /// </summary>
        internal static SeasonMetrics RunSeason(int seed, int clubPick)
        {
            var w = WorldGenerator.Create(seed);
            var league = w.League(w.Nations[0].LeagueId);

            // 구단 순서는 평판 사다리 순이므로 성큼성큼 건너뛰어 상·하위를 고루 맡는다.
            int managedId = league.clubIds[(clubPick * 7) % league.clubIds.Count];
            int controlId = league.clubIds[(clubPick * 7 + 9) % league.clubIds.Count];
            ManagerCareer.TakeCharge(w, managedId, "감사");

            var rank = league.clubIds.OrderByDescending(id => w.Strength(id))
                .Select((id, i) => (id, r: i + 1)).ToDictionary(t => t.id, t => t.r);

            // 시즌 중 전력이 얼마나 움직이는지. 시작 전력으로 순위를 예측하므로
            // AI 구단이 이적으로 전력을 바꾸면 예측이 빗나간다.
            var startStrength = league.clubIds.ToDictionary(id => id, id => w.Strength(id));

            while (!w.SeasonFinished)
                if (GameEngine.PlayNextRound(w) == null) break;

            var st = league.currentSeason.standings;
            int played = league.currentSeason.fixtures.Count(f => f.isPlayed);
            var me = st.First(e => e.clubId == managedId);

            return new SeasonMetrics(
                rankGap: st.Average(e => Math.Abs(e.position - rank[e.clubId])),
                goalsPerMatch: league.currentSeason.fixtures.Where(f => f.isPlayed)
                    .Sum(f => f.result.homeGoals + f.result.awayGoals) / (double)Math.Max(1, played),
                topPoints: st.First().points,
                bottomPoints: st.Last().points,
                managedGap: me.position - rank[managedId],
                // 통제 구단 — 같은 리그의 다른 팀. 감독을 맡은 것이 원인인지 가린다.
                controlGap: st.First(e => e.clubId == controlId).position - rank[controlId],
                condition: w.Squad(managedId).Average(p => p.condition),
                morale: w.Squad(managedId).Average(p => p.morale),
                aiCondition: league.clubIds.Where(id => id != managedId)
                    .Average(id => w.Squad(id).Average(p => p.condition)),
                aiMorale: league.clubIds.Where(id => id != managedId)
                    .Average(id => w.Squad(id).Average(p => p.morale)),
                managedDrift: w.Strength(managedId) - startStrength[managedId],
                aiDrift: league.clubIds.Where(id => id != managedId)
                    .Average(id => w.Strength(id) - startStrength[id]),
                managedShort: w.Club(managedId).shortName,
                managedStrengthRank: rank[managedId],
                managedPosition: me.position);
        }

        /// <summary>품질 지수를 훑는다. 지수가 클수록 전력이 순위에 잘 반영되지만 대승이 늘어 득점이 뜬다.</summary>
        public static void ExponentSweep(int seasons)
        {
            Console.WriteLine($"=== 품질 지수 탐색 (리그당 {seasons}시즌) ===");
            Console.WriteLine("   QE   순위오차   경기당득점   우승승점   최하위승점   판정");
            float keep = FootballTactician.Balance.BalanceConfig.QualityExponent;

            foreach (float qe in new[] { 1.6f, 1.8f, 2.0f, 2.2f, 2.4f })
            {
                FootballTactician.Balance.BalanceConfig.QualityExponent = qe;
                double gap = 0, goals = 0, top = 0, bottom = 0;

                for (int s = 0; s < seasons; s++)
                {
                    var m = RunSeason(880000 + s * 29, s);
                    gap += m.rankGap; goals += m.goalsPerMatch;
                    top += m.topPoints; bottom += m.bottomPoints;
                }

                double g = goals / seasons;
                bool ok = g >= 2.60 && g <= 2.90 && gap / seasons <= 2.6;
                Console.WriteLine($"  {qe:F1}   {gap / seasons,7:F2}   {g,9:F2}   " +
                                  $"{top / seasons,7:F0}   {bottom / seasons,9:F0}   {(ok ? "통과" : "")}");
            }
            FootballTactician.Balance.BalanceConfig.QualityExponent = keep;
        }

        public static void Audit(int seasons)
        {
            Console.WriteLine($"=== 전력 대비 순위 감사 ({seasons}회) ===");
            double sumGap = 0, sumGoals = 0, sumManagedGap = 0;
            double sumCond = 0, sumMorale = 0, sumMyDrift = 0, sumAiDrift = 0;
            double sumAiCond = 0, sumAiMorale = 0, sumControl = 0;
            int n = 0;

            for (int s = 0; s < seasons; s++)
            {
                var m = RunSeason(770000 + s * 13, s);

                sumGap += m.rankGap; sumGoals += m.goalsPerMatch; sumManagedGap += m.managedGap;
                sumCond += m.condition; sumMorale += m.morale;
                sumMyDrift += m.managedDrift; sumAiDrift += m.aiDrift;
                sumAiCond += m.aiCondition; sumAiMorale += m.aiMorale; sumControl += m.controlGap;
                n++;

                Console.WriteLine($"  #{s + 1} {m.managedShort}: " +
                                  $"전력 {m.managedStrengthRank,2}위 → 최종 {m.managedPosition,2}위 " +
                                  $"({m.managedGap,+3}) · 리그 평균 오차 {m.rankGap:F1} · 득점 {m.goalsPerMatch:F2}");
            }

            Console.WriteLine();
            Console.WriteLine($"  리그 평균 |전력순위 - 최종순위| = {sumGap / n:F2}  (무작위면 약 6.7)");
            Console.WriteLine($"  플레이어 구단 평균 편차 = {sumManagedGap / n:+0.00;-0.00}  (0 에 가까워야 한다)");
            Console.WriteLine($"  통제 구단(감독 없음) 평균 편차 = {sumControl / n:+0.00;-0.00}");
            Console.WriteLine($"  시즌 중 전력 변화 — 내 구단 {sumMyDrift / n:+0.0;-0.0} · AI 평균 {sumAiDrift / n:+0.0;-0.0}");
            Console.WriteLine($"  경기당 득점 {sumGoals / n:F2}");
            Console.WriteLine($"  시즌 말 — 내 구단 컨디션 {sumCond / n:F1} 사기 {sumMorale / n:F1} · " +
                              $"AI 컨디션 {sumAiCond / n:F1} 사기 {sumAiMorale / n:F1}");
        }

        // ── 한 시즌 전체 진행 ─────────────────────────────────────────────

        public static void Season(int seasons)
        {
            Console.WriteLine($"=== {seasons}시즌 진행 ===");
            var w = WorldGenerator.Create(20261017);

            // 중위권(별 3) 구단을 맡는다.
            var myClub = w.Clubs.Values.First(c => c.reputation == 3);
            ManagerCareer.TakeCharge(w, myClub.id, "테스트 감독");
            var league = w.MyLeague;

            Console.WriteLine($"  구단 {myClub.name} (별 {myClub.reputation}) · 리그 {league.name}");
            Console.WriteLine($"  목표: {string.Join(" / ", w.Objectives.Select(o => o.description))}");

            float caBefore = w.Squad(myClub.id).Average(GameWorld.Ca);
            var sw = Stopwatch.StartNew();

            int rounds = 0;
            while (!w.SeasonFinished && rounds < league.currentSeason.totalMatchdays + 2)
            {
                if (GameEngine.PlayNextRound(w) == null) break;
                rounds++;
            }
            sw.Stop();

            Console.WriteLine($"  {rounds}라운드 진행 · {sw.ElapsedMilliseconds} ms " +
                              $"({sw.ElapsedMilliseconds / Math.Max(1, rounds)} ms/라운드)");

            var season = league.currentSeason;
            int goals = season.fixtures.Where(f => f.isPlayed)
                                       .Sum(f => f.result.homeGoals + f.result.awayGoals);
            int played = season.fixtures.Count(f => f.isPlayed);
            Console.WriteLine($"  경기당 득점 {goals / (float)played:F2} ({played}경기)");

            Console.WriteLine();
            Console.WriteLine("  순위 | 구단                       | 별 | 전력 | 경기 | 승점 | 득실");
            foreach (var e in season.standings.Take(6).Concat(season.standings.TakeLast(3)))
            {
                var c = w.Club(e.clubId);
                string mark = e.clubId == myClub.id ? "◀" : " ";
                Console.WriteLine($"   {e.position,2}  | {c.name,-26} | {c.reputation}  | " +
                                  $"{w.Strength(c.id),4:F0} | {e.played,3}  | {e.points,3}  | {e.GoalDifference,+4} {mark}");
            }

            var me = season.standings.First(s => s.clubId == myClub.id);
            Console.WriteLine();
            Console.WriteLine($"  내 구단 {me.position}위 · {me.won}승 {me.drawn}무 {me.lost}패 · 승점 {me.points}");
            Console.WriteLine($"  이사회 신뢰도 {w.Manager.boardConfidence} · 경질 {(w.Sacked ? "예 — " + w.SackReason : "아니오")}");

            float caAfter = w.Squad(myClub.id).Average(GameWorld.Ca);
            Console.WriteLine($"  스쿼드 평균 CA {caBefore:F1} → {caAfter:F1} ({caAfter - caBefore:+0.0;-0.0})");

            var scorers = w.Players.Values
                .Where(p => league.clubIds.Contains(p.clubId))
                .OrderByDescending(p => p.seasonStats.goals)
                .Take(3);
            Console.WriteLine("  득점 선두:");
            foreach (var p in scorers)
                Console.WriteLine($"    {p.FullName,-22} {w.Club(p.clubId).shortName} " +
                                  $"{p.seasonStats.goals}골 {p.seasonStats.assists}도움 " +
                                  $"({p.seasonStats.appearances}경기, 평점 {p.seasonStats.averageRating:F2})");

            int injured = w.Squad(myClub.id).Count(p => p.IsInjured);
            Console.WriteLine($"  현재 부상자 {injured}명 · 자금 {w.MyClub.finances.balance / 1_000_000f:F1}M€ " +
                              $"({w.MyClub.finances.status})");

            foreach (var o in w.Objectives)
                Console.WriteLine($"  목표 [{o.result}] {o.description}");
        }
  
        /// <summary>
        /// 탈의실. 불만이 사기를 통해 성적까지 닿는지 본다.
        /// 분위기가 숫자로만 남고 경기에 영향이 없으면 화면을 만들 이유가 없다.
        /// </summary>
        public static void Room(int seasons)
        {
            Console.WriteLine($"=== 탈의실 {seasons}시즌 ===");

            var w = WorldGenerator.Create(31415);
            var club = w.Clubs.Values.First(c => c.reputation == 4);
            ManagerCareer.TakeCharge(w, club.id, "측정");

            Console.WriteLine($"  구단: {club.name}");
            var room = club.dressingRoom;
            Console.WriteLine($"  주장: {w.Player(room.captainPlayerId)?.FullName} " +
                              $"(영향력 {DressingRoom.Influence(w, w.Player(room.captainPlayerId)):F1})");

            var sample = w.Squad(club.id).OrderByDescending(p => GameWorld.Ca(p)).First();
            Console.WriteLine($"  {sample.FullName} 의 관계:");
            foreach (var rel in DressingRoom.RelationsOf(w, sample).Take(5))
                Console.WriteLine($"    {rel.type,-13} {w.Player(rel.targetPlayerId)?.FullName,-24} 강도 {rel.strength}");

            Console.WriteLine();
            Console.WriteLine("  시즌  화합도  불만  리더십  평균사기  승점  순위");

            for (int s = 0; s < seasons; s++)
            {
                int guard = 0;
                while (!w.SeasonFinished && guard++ < 60)
                    if (GameEngine.PlayNextRound(w) == null) break;

                room = club.dressingRoom;
                float morale = w.Squad(club.id).Average(p => p.morale);
                var entry = w.MyLeague.currentSeason.standings.First(e => e.clubId == club.id);

                Console.WriteLine($"  {w.SeasonYear,4}  {room.overallHarmony,6:F1}  " +
                                  $"{room.discontent,4:F1}  {room.leadershipStrength,6:F1}  " +
                                  $"{morale,8:F1}  {entry.points,4}  {entry.position,4}");

                if (s < seasons - 1) SeasonRollover.StartNextSeason(w);
            }

            Console.WriteLine();
            Console.WriteLine("  전 세계 구단 (AI 포함):");
            var all = w.Clubs.Values.Where(c => c.dressingRoom != null).ToList();
            Console.WriteLine($"    화합도 평균 {all.Average(c => c.dressingRoom.overallHarmony):F1}" +
                              $" · 최저 {all.Min(c => c.dressingRoom.overallHarmony):F1}" +
                              $" · 최고 {all.Max(c => c.dressingRoom.overallHarmony):F1}");
            Console.WriteLine($"    불만   평균 {all.Average(c => c.dressingRoom.discontent):F1}" +
                              $" · 최고 {all.Max(c => c.dressingRoom.discontent):F1}");
            Console.WriteLine($"    사기   평균 {w.Players.Values.Where(p => p.clubId > 0).Average(p => p.morale):F1}");

            Console.WriteLine();
            Console.WriteLine("  현재 불만:");
            foreach (var g in DressingRoom.Grievances(w, club).Take(6))
                Console.WriteLine($"    {w.Player(g.PlayerId)?.FullName,-24} {g.Reason} ({g.weight:F0})");

            // 화합도가 성적까지 닿는지. 같은 세계에서 분위기만 바꿔 한 시즌씩 돌린다.
            // 숫자만 움직이고 승점이 그대로면 화면을 만들 이유가 없다.
            Console.WriteLine();
            Console.WriteLine("  분위기 → 성적 (같은 세계, 같은 스쿼드)");
            Console.WriteLine("    화합도  기준선  평균사기  승점  순위");

            foreach (float h in new[] { 10f, 50f, 90f })
            {
                var t = WorldGenerator.Create(31415);
                var tc = t.Clubs.Values.First(c => c.reputation == 4);
                ManagerCareer.TakeCharge(t, tc.id, "측정");

                // 매주 다시 재므로 기준선을 직접 고정해 분위기만 남긴다.
                WeekCycle.MoraleAnchorOverride = _ => DressingRoom.MoraleAnchor
                    + (h - 50f) / 50f * DressingRoom.AnchorSwing;

                int guard = 0;
                while (!t.SeasonFinished && guard++ < 60)
                    if (GameEngine.PlayNextRound(t) == null) break;

                var e = t.MyLeague.currentSeason.standings.First(x => x.clubId == tc.id);
                Console.WriteLine($"    {h,6:F0}  {WeekCycle.MoraleAnchorOverride(tc),6:F1}  " +
                                  $"{t.Squad(tc.id).Average(p => p.morale),8:F1}  {e.points,4}  {e.position,4}");
            }

            WeekCycle.MoraleAnchorOverride = null;
        }

        /// <summary>
        /// 스카우트 — 추정 구간이 얼마나 넓고 얼마나 빗나가는지.
        /// 구간이 늘 참값을 한가운데 품으면 넓이가 아무 의미가 없다.
        /// </summary>
        public static void Scout(int seasons)
        {
            var w = WorldGenerator.Create(31415);
            var club = w.Clubs.Values.First(c => c.reputation == 4);
            ManagerCareer.TakeCharge(w, club.id, "하네스");
            Scouting.StartWeek(w);

            Console.WriteLine($"\n== 스카우트 ==  {club.name}  주당 관찰 {w.ScoutingLeft}회\n");

            var others = w.Players.Values.Where(p => p.clubId > 0 && p.clubId != w.ManagedClubId).ToList();

            void Report(string tag, IEnumerable<PlayerData> pool)
            {
                var list = pool.ToList();
                if (list.Count == 0) { Console.WriteLine($"  {tag,-10} (없음)"); return; }

                double width = 0, miss = 0;
                int off = 0;
                foreach (var p in list)
                {
                    float real = GameWorld.Ca(p);
                    var (low, high, shown) = Scouting.Estimate(w, p, real);
                    width += high - low;
                    miss += Math.Abs(shown - real);
                    if (Math.Abs(shown - real) > 1f) off++;
                }

                Console.WriteLine($"  {tag,-10} 구간 폭 {width / list.Count,5:F1} · 빗나감 {miss / list.Count,4:F1}"
                                + $" · 중심이 틀린 비율 {off * 100.0 / list.Count,3:F0}%   ({list.Count}명)");
            }

            Report("같은 리그", others.Where(p => w.LeagueOf(p.clubId)?.id == w.MyLeague?.id));
            Report("국가대표", others.Where(p => p.caps > 0 && w.LeagueOf(p.clubId)?.id != w.MyLeague?.id));
            Report("그 밖", others.Where(p => p.caps == 0 && w.LeagueOf(p.clubId)?.id != w.MyLeague?.id));

            // 관찰을 거듭하면 좁아지는가.
            var target = others.First(p => Scouting.Knowledge(w, p) < 0.1f);
            Console.WriteLine($"\n  {target.FullName} 을 관찰하며  (참값 {GameWorld.Ca(target):F0})");
            for (int i = 0; i <= BalanceConfig.ScoutReportsForFull; i++)
            {
                var (low, high, _) = Scouting.Estimate(w, target, GameWorld.Ca(target));
                Console.WriteLine($"    보고서 {i}회 — {low,5:F0} ~ {high,5:F0}");
                Scouting.StartWeek(w);
                Scouting.SendScout(w, target);
            }

            // 2군이 실제로 출전을 만들어 내는가.
            Console.WriteLine();
            for (int s = 0; s < seasons; s++)
            {
                int guard = 0;
                while (!w.SeasonFinished && guard++ < 60)
                    if (GameEngine.PlayNextRound(w) == null) break;

                var squad = w.Squad(w.ManagedClubId).ToList();
                var played = squad.Where(p => p.reserveAppearances > 0).ToList();
                int idle = squad.Count(p => (p.seasonStats?.appearances ?? 0) == 0 && p.reserveAppearances == 0);

                Console.WriteLine($"  {w.Date.year}  2군 출전 {played.Count,2}명 · 평균 {(played.Count == 0 ? 0 : played.Average(p => p.reserveAppearances)),3:F1}회"
                                + $" · 한 경기도 못 뛴 선수 {idle,2}명");

                SeasonRollover.StartNextSeason(w);
            }
        }

        /// <summary>
        /// 스태프 — 네 갈래가 저마다 다른 것을 바꾸는지, 그리고 대가가 있는지.
        /// 하나가 다 하면 "좋은 스태프"를 모으는 것 말고 할 일이 없다.
        /// </summary>
        public static void Staff(int seasons)
        {
            var w = WorldGenerator.Create(31415);
            Console.WriteLine($"\n== 스태프 ==  {w.Staff.Count}명 (구단 소속 {w.Staff.Values.Count(s => s.clubId > 0)} · 자유 {w.Staff.Values.Count(s => s.clubId <= 0)})\n");

            // 평판대별로 네 갈래의 힘이 어떻게 갈리는지.
            Console.WriteLine("  평판   코칭   스카우팅   의료   분석    성장배수  부상배수  보고서");
            foreach (int stars in new[] { 5, 4, 3, 2, 1 })
            {
                var clubs = w.Clubs.Values.Where(c => c.reputation == stars).ToList();
                if (clubs.Count == 0) continue;

                Console.WriteLine($"   {stars}성  {clubs.Average(c => StaffRoom.Coaching(w, c.id)),5:F2}  "
                    + $"{clubs.Average(c => StaffRoom.ScoutingPower(w, c.id)),8:F2}  "
                    + $"{clubs.Average(c => StaffRoom.Medical(w, c.id)),5:F2}  "
                    + $"{clubs.Average(c => StaffRoom.Analytics(w, c.id)),5:F2}    "
                    + $"{clubs.Average(c => StaffRoom.CoachingFactor(w, c.id)),7:F3}  "
                    + $"{clubs.Average(c => StaffRoom.InjuryFactor(w, c.id)),7:F3}  "
                    + $"{clubs.Average(c => StaffRoom.ReportValue(w, c.id)),6:F2}");
            }

            // 갈래끼리 독립인가. 하나가 좋으면 다 좋다면 갈래를 나눈 뜻이 없다.
            var all = w.Clubs.Values.ToList();
            double Corr(Func<ClubData, double> a, Func<ClubData, double> b)
            {
                double ma = all.Average(a), mb = all.Average(b);
                double cov = all.Sum(c => (a(c) - ma) * (b(c) - mb));
                double sa = Math.Sqrt(all.Sum(c => Math.Pow(a(c) - ma, 2)));
                double sb = Math.Sqrt(all.Sum(c => Math.Pow(b(c) - mb, 2)));
                return sa * sb == 0 ? 0 : cov / (sa * sb);
            }

            Console.WriteLine($"\n  코칭↔의료 상관 {Corr(c => StaffRoom.Coaching(w, c.id), c => StaffRoom.Medical(w, c.id)),5:F2}"
                + $" · 코칭↔스카우팅 {Corr(c => StaffRoom.Coaching(w, c.id), c => StaffRoom.ScoutingPower(w, c.id)),5:F2}"
                + "   (1 에 가까우면 갈래를 나눈 뜻이 없다)");

            // 대가가 있는가 — 주급이 선수 지갑을 얼마나 먹는가.
            double share = all.Average(c => c.finances.currentWageSpend == 0
                ? 0 : StaffRoom.WageBill(w, c.id) * 100.0 / c.finances.currentWageSpend);
            Console.WriteLine($"  스태프가 먹는 주급 비중 평균 {share:F1}%");

            // 아무도 없으면 벌을 받는가.
            var victim = w.Clubs.Values.First(c => c.reputation == 4);
            float before = StaffRoom.CoachingFactor(w, victim.id);
            int saved = StaffRoom.WageBill(w, victim.id);   // 내보낸 뒤에 재면 0 이 나온다
            var fired = victim.staffIds.ToList();
            foreach (int id in fired) StaffRoom.Release(w, victim, w.Staff[id]);
            Console.WriteLine($"  스태프를 모두 내보내면 성장배수 {before:F3} → {StaffRoom.CoachingFactor(w, victim.id):F3}"
                + $" · 주급 {saved:N0}€ 절약");
        }

        /// <summary>세계가 늙는가 — 은퇴가 없으면 몇 시즌 만에 노인 리그가 된다.</summary>
        public static void Ages(int seasons)
        {
            var w = WorldGenerator.Create(31415);
            var club = w.Clubs.Values.First(c => c.reputation == 4);
            ManagerCareer.TakeCharge(w, club.id, "하네스");

            Console.WriteLine("\n== 나이 ==\n");
            Console.WriteLine("  시즌   선수수   평균나이  35세이상  40세이상  최고령   18~21세");

            void Row()
            {
                var ps = w.Players.Values.Where(p => p.clubId > 0).ToList();
                Console.WriteLine($"  {w.Date.year}   {ps.Count,6}   {ps.Average(p => w.Age(p)),7:F1}  "
                    + $"{ps.Count(p => w.Age(p) >= 35),7}  {ps.Count(p => w.Age(p) >= 40),8}  "
                    + $"{ps.Max(p => w.Age(p)),6}   {ps.Count(p => w.Age(p) <= 21),7}");
            }

            Row();
            for (int s = 0; s < seasons; s++)
            {
                int guard = 0;
                while (!w.SeasonFinished && guard++ < 60)
                    if (GameEngine.PlayNextRound(w) == null) break;
                SeasonRollover.StartNextSeason(w);
                if (s % 2 == 1 || s == seasons - 1) Row();
            }
        }

        /// <summary>시상과 명예 — 우승이 기록에 남는가, 상이 엉뚱한 사람에게 가지 않는가.</summary>
        public static void Honours(int seasons)
        {
            var w = WorldGenerator.Create(31415);
            var club = w.Clubs.Values.First(c => c.reputation == 4);
            ManagerCareer.TakeCharge(w, club.id, "하네스");

            int before = w.Clubs.Values.Sum(c => c.history.leagueTitles + c.history.cupTitles
                                               + c.history.continentalTitles);
            Console.WriteLine($"\n== 시상·명예 ==  시작 시점 통산 우승 합계 {before}\n");

            for (int s = 0; s < seasons; s++)
            {
                int guard = 0;
                while (!w.SeasonFinished && guard++ < 60)
                    if (GameEngine.PlayNextRound(w) == null) break;
                SeasonRollover.StartNextSeason(w);
            }

            int after = w.Clubs.Values.Sum(c => c.history.leagueTitles + c.history.cupTitles
                                              + c.history.continentalTitles);
            Console.WriteLine($"  {seasons}시즌 뒤 합계 {after}  (늘어난 {after - before} · 시즌당 {(after - before) / (float)seasons:F1})");
            Console.WriteLine($"    기대: 1부 4곳 + 국내컵 4곳 + 대륙 1곳 = 시즌당 9\n");

            var mine = w.MyLeague!;
            var sets = w.Awards.Where(a => a.leagueId == mine.id).OrderBy(a => a.year).ToList();

            Console.WriteLine($"  {mine.name} 수상 기록 {sets.Count}시즌");
            foreach (var a in sets.TakeLast(5))
            {
                Console.WriteLine($"    {a.year}  MVP {a.bestPlayerName,-22} 평점 {a.bestRating:F2}"
                    + $" · 득점왕 {a.topScorerName,-22} {a.topScorerGoals,2}골"
                    + $" · 감독 {a.managerClubName,-18} +{a.managerPlacesGained}");
            }

            // 상이 두 경기 뛴 선수에게 가지 않는지.
            int thin = sets.Count(a => (w.Player(a.bestPlayerId)?.careerStats?.appearances ?? 99) < 5);
            Console.WriteLine($"\n  출전이 거의 없는 선수가 MVP 를 받은 시즌 {thin} / {sets.Count}");

            // 승격·강등이 섞여도 우승이 제 리그에 기록되는지.
            var topClub = w.Clubs.Values.OrderByDescending(c => c.history.leagueTitles).First();
            Console.WriteLine($"  최다 우승 {topClub.name} — 리그 {topClub.history.leagueTitles}"
                + $" · 컵 {topClub.history.cupTitles} · 대륙 {topClub.history.continentalTitles}"
                + $" (평판 {topClub.reputation}성)");
        }

        /// <summary>경기장과 스폰서 — 감독이 한 일이 수입으로 돌아오는가.</summary>
        public static void Business(int seasons)
        {
            var w = WorldGenerator.Create(31415);
            var club = w.Clubs.Values.First(c => c.reputation == 3);
            ManagerCareer.TakeCharge(w, club.id, "하네스");

            Console.WriteLine($"\n== 경기장·스폰서 ==  {club.name}\n");

            // 기준선이 중립인가 — 아무것도 안 한 구단이 손해를 보면 안 된다.
            var mults = w.Clubs.Values.Select(ClubBusiness.RevenueMultiplier).ToList();
            Console.WriteLine($"  세계 수입 배수  평균 {mults.Average(),5:F3} · 최저 {mults.Min(),5:F3} · 최고 {mults.Max(),5:F3}");
            Console.WriteLine($"    (평균이 1.0 에서 멀면 세계 전체의 돈이 밀린 것이다)\n");

            Console.WriteLine("  평판   좌석평균   수입배수   스폰서비중");
            foreach (int stars in new[] { 5, 4, 3, 2, 1 })
            {
                var cs = w.Clubs.Values.Where(c => c.reputation == stars).ToList();
                if (cs.Count == 0) continue;
                Console.WriteLine($"   {stars}성  {cs.Average(c => c.stadium.capacity),8:N0}   "
                    + $"{cs.Average(ClubBusiness.RevenueMultiplier),8:F3}   "
                    + $"{cs.Average(ClubBusiness.SponsorFactor),10:F3}");
            }

            // 증축이 실제로 수입을 올리는가.
            long revBefore = club.finances.annualRevenue;
            int seatsBefore = club.stadium.capacity;
            club.finances.balance = ClubBusiness.ExpansionCost(club) * 3;

            Console.WriteLine($"\n  증축 전  {seatsBefore:N0}석 · 연 수입 {revBefore / 1_000_000.0:F1}M€");
            ClubBusiness.Expand(w, club);
            SeasonRollover.RebudgetClub(w, club);
            Console.WriteLine($"  증축 후  {club.stadium.capacity:N0}석 · 연 수입 {club.finances.annualRevenue / 1_000_000.0:F1}M€"
                + $"  (+{(club.finances.annualRevenue - revBefore) / 1_000_000.0:F1}M€)");

            // 성적이 스폰서 금액을 바꾸는가.
            var good = new ClubData
            {
                id = club.id, reputation = club.reputation, finances = club.finances,
                history = new ClubHistory
                {
                    recentSeasons = new List<SeasonRecord>
                    { new() { finalPosition = 1 }, new() { finalPosition = 2 }, new() { finalPosition = 1 } },
                },
            };
            var bad = new ClubData
            {
                id = club.id, reputation = club.reputation, finances = club.finances,
                history = new ClubHistory
                {
                    recentSeasons = new List<SeasonRecord>
                    { new() { finalPosition = 17 }, new() { finalPosition = 19 }, new() { finalPosition = 18 } },
                },
            };

            var rng = new Random(5);
            long high = ClubBusiness.Negotiate(w, good, rng).annualFee;
            long low = ClubBusiness.Negotiate(w, bad, new Random(5)).annualFee;
            Console.WriteLine($"\n  스폰서  상위권 {high / 1_000_000.0:F1}M€  vs  하위권 {low / 1_000_000.0:F1}M€"
                + $"  ({(high / (double)Math.Max(1, low) - 1) * 100:F0}% 차이)");

            // 시즌을 돌려 수입이 폭주하지 않는지.
            Console.WriteLine();
            for (int s = 0; s < seasons; s++)
            {
                int guard = 0;
                while (!w.SeasonFinished && guard++ < 60)
                    if (GameEngine.PlayNextRound(w) == null) break;
                SeasonRollover.StartNextSeason(w);

                if (s % 2 == 1 || s == seasons - 1)
                {
                    var all = w.Clubs.Values.ToList();
                    Console.WriteLine($"  {w.Date.year}  세계 연 수입 합계 {all.Sum(c => c.finances.annualRevenue) / 1_000_000_000.0,6:F2}B€"
                        + $" · 수입 배수 평균 {all.Average(ClubBusiness.RevenueMultiplier),5:F3}"
                        + $" · 자금 합계 {all.Sum(c => c.finances.balance) / 1_000_000_000.0,6:F2}B€");
                    Console.WriteLine($"          경기장 {all.Average(ClubBusiness.StadiumFactor),5:F3}"
                        + $" · 스폰서 {all.Average(ClubBusiness.SponsorFactor),5:F3}"
                        + $" · 좌석비 {all.Average(c => c.stadium.capacity / (float)ClubBusiness.ExpectedCapacity(c.reputation)),5:F3}"
                        + $" · 분위기차 {all.Average(c => c.stadium.atmosphereRating - ClubBusiness.ExpectedAtmosphere(c.reputation)),5:F1}");
                }
            }
        }

        /// <summary>홈그로운 — 규정이 물리는가, 그러나 시장을 얼려 버리지는 않는가.</summary>
        public static void HomegrownRule(int seasons)
        {
            var w = WorldGenerator.Create(31415);
            var club = w.Clubs.Values.First(c => c.reputation == 4);
            ManagerCareer.TakeCharge(w, club.id, "하네스");

            Console.WriteLine($"\n== 홈그로운 ==  필요 {Homegrown.RequiredFor(w, club.id)}명\n");
            Console.WriteLine("  시즌   평균 보유   규정 미달 구단   이적 건수   유스 출신 비중");

            int transfersLast = w.Transfers.Count;

            void Row()
            {
                var all = w.Clubs.Values.ToList();
                int now = w.Transfers.Count;

                var squads = w.Players.Values.Where(p => p.clubId > 0).ToList();
                float academy = squads.Count == 0
                    ? 0f
                    : squads.Count(p => p.homegrown?.isClubAcademy == true) * 100f / squads.Count;

                Console.WriteLine($"  {w.Date.year}  {all.Average(c => Homegrown.CountIn(w, c.id)),9:F1}"
                    + $"  {all.Count(c => !Homegrown.Meets(w, c.id)),13}"
                    + $"  {now - transfersLast,10}"
                    + $"  {academy,14:F1}%");

                transfersLast = now;
            }

            Row();

            // 분포를 본다. 평균만 보면 치우침이 보이지 않는다.
            var counts = w.Clubs.Values.Select(c => Homegrown.CountIn(w, c.id)).OrderBy(x => x).ToList();
            Console.WriteLine($"  보유 분포  최저 {counts.First()} · 25% {counts[counts.Count / 4]}"
                + $" · 중앙 {counts[counts.Count / 2]} · 75% {counts[counts.Count * 3 / 4]} · 최고 {counts.Last()}");

            var worst = w.Clubs.Values.OrderBy(c => Homegrown.CountIn(w, c.id)).First();
            var wn = w.NationOf(worst.id);
            int localCount = w.Squad(worst.id)
                .Count(p => p.nationality == NameForge.ByCode(wn?.Code ?? "NSL").Nationality);
            Console.WriteLine($"  최저 구단 {worst.name} ({wn?.Country}) — 자국 선수 "
                + $"{localCount} / {w.Squad(worst.id).Count()}명");
            Console.WriteLine();

            for (int s = 0; s < seasons; s++)
            {
                int guard = 0;
                while (!w.SeasonFinished && guard++ < 60)
                    if (GameEngine.PlayNextRound(w) == null) break;
                SeasonRollover.StartNextSeason(w);
                Row();
            }

            // 규정이 실제로 영입을 막는가.
            var victim = w.Clubs.Values.First();
            foreach (var p in w.Squad(victim.id).Where(Homegrown.IsHomegrown).ToList())
                p.homegrown.isHomegrown = false;

            var outsider = w.Players.Values.First(p => p.clubId > 0 && p.clubId != victim.id
                                                    && !Homegrown.IsHomegrown(p));
            var insider = w.Players.Values.FirstOrDefault(p => p.clubId > 0 && p.clubId != victim.id
                                                            && Homegrown.IsHomegrown(p));

            Console.WriteLine($"\n  홈그로운 0명인 구단이");
            Console.WriteLine($"    외국 선수를 데려올 수 있는가 — {Homegrown.CanSign(w, victim.id, outsider)}");
            Console.WriteLine($"    홈그로운을 데려올 수 있는가 — "
                + (insider == null ? "(후보 없음)" : Homegrown.CanSign(w, victim.id, insider).ToString()));
            Console.WriteLine($"  {Homegrown.Shortfall(w, victim.id)}");
        }

        /// <summary>어디에 시간이 가는가. 짐작으로 고치지 않기 위해 먼저 잰다.</summary>
        public static void Perf(int seasons)
        {
            var sw = System.Diagnostics.Stopwatch.StartNew();
            var w = WorldGenerator.Create(31415);
            long gen = sw.ElapsedMilliseconds;

            var club = w.Clubs.Values.First(c => c.reputation == 4);
            ManagerCareer.TakeCharge(w, club.id, "하네스");

            Console.WriteLine($"\n== 성능 (차갑게 — 켜고 처음 하는 한 판) ==\n");
            Console.WriteLine("  계층 컴파일이 올라오기 전의 값이 섞여 있다. 플레이어가 첫 실행에서 겪는 시간이 이쪽이고,");
            Console.WriteLine("  데워진 뒤의 정상 비용은 perfround 로 본다 (릴리스에서 세 패스쯤 지나 수렴한다).\n");
            Console.WriteLine($"  세계 생성        {gen,6} ms");

            long rounds = 0, rollovers = 0;
            int roundCount = 0;

            for (int s = 0; s < seasons; s++)
            {
                sw.Restart();
                int guard = 0;
                while (!w.SeasonFinished && guard++ < 60)
                {
                    if (GameEngine.PlayNextRound(w) == null) break;
                    roundCount++;
                }
                rounds += sw.ElapsedMilliseconds;

                sw.Restart();
                SeasonRollover.StartNextSeason(w);
                rollovers += sw.ElapsedMilliseconds;
            }

            Console.WriteLine($"  라운드 {roundCount}회      {rounds,6} ms  (회당 {rounds / (float)Math.Max(1, roundCount),5:F1} ms)");
            Console.WriteLine($"  시즌 넘김 {seasons}회     {rollovers,6} ms  (회당 {rollovers / (float)seasons,5:F0} ms)");

            // 세이브
            sw.Restart();
            var packed = SaveGame.Serialize(w, "성능");
            long save = sw.ElapsedMilliseconds;

            sw.Restart();
            SaveGame.Deserialize(packed);
            long load = sw.ElapsedMilliseconds;

            Console.WriteLine($"  저장             {save,6} ms  ({packed.Length / 1024.0:F0} KB)");
            Console.WriteLine($"  불러오기         {load,6} ms");

            // 시즌 넘김 안쪽을 쪼갠다. 합쳐서 보면 어디가 느린지 모른다.
            sw.Restart(); Retirement.RunAll(w, new Random(1)); long t1 = sw.ElapsedMilliseconds;
            sw.Restart(); ClubBusiness.RenewExpired(w, new Random(1)); long t2 = sw.ElapsedMilliseconds;
            sw.Restart(); Homegrown.AdvanceYear(w); long t3 = sw.ElapsedMilliseconds;
            sw.Restart(); SeasonAwards.RunAll(w, w.SeasonYear); long t4 = sw.ElapsedMilliseconds;
            sw.Restart(); YouthAcademy.RunAll(w, new Random(1)); long t5 = sw.ElapsedMilliseconds;
            sw.Restart(); DressingRoom.UpdateAll(w); long t6 = sw.ElapsedMilliseconds;

            Console.WriteLine($"\n  시즌 넘김 안쪽 (1회분)");
            Console.WriteLine($"    은퇴 {t1,5} ms · 스폰서 {t2,5} ms · 홈그로운 {t3,5} ms");
            Console.WriteLine($"    시상 {t4,5} ms · 유스 {t5,5} ms · 탈의실 {t6,5} ms");
        }

        /// <summary>
        /// 라운드 한 회를 구간별로 쪼갠다. 합친 47ms 로는 경기 시뮬레이션인지
        /// 240구단 주간 처리인지 알 수 없다 — 쪼개기 전에 고치지 않는다.
        ///
        /// 구간 표시는 <see cref="GameEngine.PlayNextRound"/> 안에 있으므로
        /// 여기서 재는 것은 게임이 실제로 지나는 경로다.
        /// </summary>
        public static void PerfRound(int rounds)
        {
            // 같은 시드로 여러 번 돈다. 세계가 결정적이므로 매번 일거리가 같고,
            // 차이는 구간 표시 비용뿐이다. 표시 비용을 모르면 쪼갠 수치를 믿을 수 없다.
            //
            // 데우기는 "한 번"이 아니라 "수렴할 때까지"다. 릴리스 빌드에서는 계층 컴파일(tiered JIT)이
            // 두 번째 패스까지도 계속 올라와, 한 번만 버리면 표시를 켠 세 번째 패스가 두 배 빨라
            // "구간 표시 비용 -9.67ms" 같은 수치가 나온다. 디버그 빌드는 최적화가 꺼져 있어
            // 한 번만 버려도 안정됐고, 그래서 이 함정이 릴리스로 재기 전까지 보이지 않았다.
            Console.WriteLine("\n== 데우기 (연속 두 회가 5% 안에 들면 수렴) ==");
            double prev = double.MaxValue;
            for (int i = 0; i < 6; i++)
            {
                double ms = RoundPass(rounds, profile: false, out int wn, out _);
                Console.WriteLine($"  {i + 1}회차 {ms / wn,7:F2} ms/회");
                if (Math.Abs(ms - prev) <= prev * 0.05) break;
                prev = ms;
            }

            double plain = RoundPass(rounds, profile: false, out int n1, out _);
            double marked = RoundPass(rounds, profile: true, out int played, out double rollover);

            Console.WriteLine($"\n== 라운드 쪼개기 ({played}회) ==\n");
            Console.WriteLine($"  표시 없이          {plain / n1,7:F2} ms/회   ← 실제 비용");
            Console.WriteLine($"  표시 켜고          {marked / played,7:F2} ms/회");
            Console.WriteLine($"  구간 표시 비용      {(marked / played) - (plain / n1),7:F2} ms/회\n");

            double total = marked;
            Console.WriteLine($"  {"구간",-24}{"ms/회",9}{"비중",8}{"호출",8}{"ms/호출",10}");

            // 호출 수를 라운드로 나누지 않고 그대로 찍는다. 이적 창구처럼 몇 주만 도는 구간은
            // 평균으로 뭉개면 "회당 10ms" 로 보이지만 실제로는 그 몇 주에 몰려 터진다.
            foreach (var (label, ms, count) in SimProfile.Results())
                Console.WriteLine($"  {label,-24}{ms / played,9:F2}{ms / total * 100,7:F1}%{count,8}{ms / Math.Max(1, count),10:F2}");

            double top = SimProfile.MsOf("리그 경기") + SimProfile.MsOf("컵")
                       + SimProfile.MsOf("A매치") + SimProfile.MsOf("2군")
                       + SimProfile.MsOf("경기 뒷처리") + SimProfile.MsOf("주간 진행");
            Console.WriteLine($"\n  상위 구간 합        {top / played,7:F2} ms/회  ({top / total * 100:F1}%)");
            Console.WriteLine($"  시즌 넘김(라운드 밖) {rollover / played,7:F2} ms/회");
            Console.WriteLine($"  나머지              {(total - top - rollover) / played,7:F2} ms/회");
        }

        /// <summary>
        /// 세계 생성과 저장을 쪼갠다.
        ///
        /// 플레이어가 기다리는 시간은 라운드가 아니라 이 둘이다 — 새 게임 한 번과 저장 한 번.
        /// 생성은 켜고 처음 하는 일이라 <b>차가운 값이 실제로 겪는 시간</b>이고,
        /// 쪼갠 비중은 데워진 뒤에야 안정된다.
        ///
        /// 한 번이 50ms 짜리 일은 한 번만 재면 두 배까지 흔들린다(이 기계에서 44~93ms를 봤다).
        /// 그래서 여러 번 돌려 <b>중앙값</b>을 쓰고, 쪼갠 값도 여러 번의 합을 나눈다.
        /// </summary>
        public static void PerfGen(int repeats)
        {
            if (repeats <= 0) repeats = 12;
            var sw = new Stopwatch();

            // 차갑게 — 켜고 처음 만드는 한 번. 이 값만은 다시 잴 수 없다.
            sw.Restart();
            WorldGenerator.Create(31415);
            sw.Stop();
            double cold = sw.Elapsed.TotalMilliseconds;

            var warm = new List<double>();
            for (int i = 0; i < repeats; i++)
            {
                sw.Restart();
                WorldGenerator.Create(31415 + i + 1);
                sw.Stop();
                warm.Add(sw.Elapsed.TotalMilliseconds);
            }
            warm.Sort();

            Console.WriteLine($"\n== 세계 생성 ==\n");
            Console.WriteLine($"  차갑게(첫 번째)   {cold,7:F1} ms   ← 새 게임에서 실제로 겪는 값");
            Console.WriteLine($"  데워진 뒤 중앙값   {warm[warm.Count / 2],7:F1} ms"
                              + $"   (최저 {warm[0]:F1} · 최고 {warm[^1]:F1}, {repeats}회)");

            // 쪼개기 — 같은 횟수만큼 돌려 합을 나눈다.
            SimProfile.Reset();
            SimProfile.Enabled = true;
            sw.Restart();
            GameWorld w = WorldGenerator.Create(31415);               // 첫 번째도 횟수에 든다
            for (int i = 1; i < repeats; i++) w = WorldGenerator.Create(31415);
            sw.Stop();
            SimProfile.Enabled = false;
            double marked = sw.Elapsed.TotalMilliseconds / repeats;

            Console.WriteLine($"  표시 켜고         {marked,7:F1} ms\n");
            Console.WriteLine($"  {"구간",-18}{"ms",8}{"비중",8}{"호출",8}{"ms/호출",10}");
            foreach (var (label, ms, count) in SimProfile.Results())
                Console.WriteLine($"  {label,-18}{ms / repeats,8:F1}{ms / repeats / marked * 100,7:F1}%"
                                  + $"{count / repeats,8}{ms / Math.Max(1, count),10:F3}");

            // ── 저장 ──
            Console.WriteLine($"\n== 저장 ==\n");
            var (jsonMs, gzipMs, b64Ms, raw, packed) = SaveGame.Profile(w);
            Console.WriteLine($"  차갑게           JSON {jsonMs,5} ms · gzip {gzipMs,4} ms · base64 {b64Ms,4} ms"
                              + $"  ({raw / 1024f / 1024f:F2} MB → {packed / 1024f:F0} KB)");

            var json = new List<long>();
            var gzip = new List<long>();
            for (int i = 0; i < 5; i++)
            {
                var p = SaveGame.Profile(w);
                json.Add(p.jsonMs);
                gzip.Add(p.gzipMs);
            }
            json.Sort();
            gzip.Sort();
            Console.WriteLine($"  데워진 뒤 중앙값   JSON {json[json.Count / 2],5} ms · gzip {gzip[gzip.Count / 2],4} ms");

            sw.Restart();
            byte[] bytes = SaveGame.Serialize(w, "측정");
            sw.Stop();
            double writeMs = sw.Elapsed.TotalMilliseconds;

            sw.Restart();
            SaveGame.Deserialize(bytes);
            sw.Stop();
            Console.WriteLine($"\n  쓰기 {writeMs,6:F0} ms · 읽기 {sw.Elapsed.TotalMilliseconds,6:F0} ms"
                              + $" · {bytes.Length / 1024f:F0} KB");
            Console.WriteLine($"  지문(저장 바이트) {SaveGame.Fingerprint(w)}");
        }

        /// <summary>
        /// 세계 한 시즌의 지문. 같은 시드로 돌린 두 빌드가 같은 값을 내면 동작이 같다.
        ///
        /// "동작은 그대로, 더 빠르게" 는 말로 주장할 수 없다. 빠르게 고친 자리는
        /// 대개 정렬을 훑기로 바꾼 자리이고, 그건 동점에서 조용히 갈라진다 —
        /// 라인업 한 자리가 달라지면 그 경기의 난수 흐름부터 시즌 순위까지 전부 밀린다.
        /// 영역별로 따로 찍는다. 하나만 어긋나면 어디를 건드렸는지 바로 좁혀진다.
        /// 실수는 비트로 넣는다. 반올림해서 비교하면 0.3% 틀어진 값이 통과한다.
        /// </summary>
        public static void Fingerprint(int rounds)
        {
            var w = WorldGenerator.Create(31415);
            var club = w.Clubs.Values.First(c => c.reputation == 4);
            ManagerCareer.TakeCharge(w, club.id, "하네스");

            int played = 0;
            while (played < rounds)
            {
                if (w.SeasonFinished) SeasonRollover.StartNextSeason(w);
                if (GameEngine.PlayNextRound(w) == null) break;
                played++;
            }

            var matches = new Fnv();
            var table = new Fnv();
            var players = new Fnv();
            var clubs = new Fnv();
            var deals = new Fnv();

            int goals = 0, playedMatches = 0;

            foreach (int lid in w.Leagues.Keys.OrderBy(x => x))
            {
                var league = w.Leagues[lid];
                var season = league.currentSeason;
                matches.Add(lid);
                matches.Add(season.currentMatchday);

                foreach (var f in season.fixtures.OrderBy(f => f.id))
                {
                    matches.Add(f.id);
                    matches.Add(f.isPlayed);
                    matches.Add(f.result?.homeGoals ?? -1);
                    matches.Add(f.result?.awayGoals ?? -1);
                    if (f.isPlayed && f.result != null)
                    {
                        playedMatches++;
                        goals += f.result.homeGoals + f.result.awayGoals;
                    }
                }

                foreach (var s in season.standings ?? new List<StandingEntry>())
                {
                    table.Add(s.clubId);
                    table.Add(s.position);
                    table.Add(s.played);
                    table.Add(s.won);
                    table.Add(s.drawn);
                    table.Add(s.lost);
                    table.Add(s.goalsFor);
                    table.Add(s.goalsAgainst);
                    table.Add(s.points);
                }
            }

            foreach (int pid in w.Players.Keys.OrderBy(x => x))
            {
                var p = w.Players[pid];
                players.Add(pid);
                players.Add(p.clubId);
                players.Add(p.AbilityScore);
                players.Add(p.condition);
                players.Add(p.morale);
                players.Add(p.marketValue);
                players.Add((int)p.status);
                players.Add(p.caps);
                players.Add(p.internationalGoals);
                players.Add(p.injury != null && p.injury.isActive);
                players.Add(p.contract?.weeklyWage ?? 0);

                var st = p.seasonStats;
                players.Add(st?.appearances ?? -1);
                players.Add(st?.goals ?? -1);
                players.Add(st?.assists ?? -1);
                players.Add(st?.yellowCards ?? -1);
                players.Add(st?.redCards ?? -1);
                players.Add(st?.minutesPlayed ?? -1f);
                players.Add(st?.averageRating ?? -1f);
                players.Add(st?.cleanSheets ?? -1);
            }

            foreach (int cid in w.Clubs.Keys.OrderBy(x => x))
            {
                var c = w.Clubs[cid];
                clubs.Add(cid);
                clubs.Add(c.finances.balance);
                clubs.Add(c.finances.currentWageSpend);
                clubs.Add(c.finances.annualRevenue);
                foreach (int id in c.playerIds) clubs.Add(id);

                var room = c.dressingRoom;
                clubs.Add(room?.captainPlayerId ?? -1);
                clubs.Add(room?.viceCaptainPlayerId ?? -1);
                clubs.Add(room?.discontent ?? -1f);
                clubs.Add(room?.overallHarmony ?? -1f);
                clubs.Add(room?.leadershipStrength ?? -1f);
                foreach (int id in room?.influencerPlayerIds ?? new List<int>()) clubs.Add(id);
            }

            foreach (int pid in w.Loans.Keys.OrderBy(x => x))
            {
                var l = w.Loans[pid];
                deals.Add(pid);
                deals.Add(l.parentClubId);
                deals.Add(l.hostClubId);
                deals.Add(l.endYear);
            }
            foreach (int pid in w.TransferRequests.Keys.OrderBy(x => x)) deals.Add(pid);

            var all = new Fnv();
            all.Add(w.Date.year); all.Add(w.Date.month); all.Add(w.Date.day);
            foreach (var part in new[] { matches, table, players, clubs, deals }) all.Add(part.Value);

            Console.WriteLine($"\n== 지문 (시드 31415 · {played}라운드) ==\n");
            Console.WriteLine($"  경기 결과   {matches.Hex}");
            Console.WriteLine($"  순위표     {table.Hex}");
            Console.WriteLine($"  선수      {players.Hex}");
            Console.WriteLine($"  구단      {clubs.Hex}");
            Console.WriteLine($"  이적·임대  {deals.Hex}");
            Console.WriteLine($"  전체      {all.Hex}");
            Console.WriteLine();
            Console.WriteLine($"  치른 경기 {playedMatches}회 · 총 득점 {goals} ({goals / (float)Math.Max(1, playedMatches):F3}/경기)");
            Console.WriteLine($"  임대 {w.Loans.Count}건 · 이적 요청 {w.TransferRequests.Count}건 · {w.Date.year}-{w.Date.month:00}-{w.Date.day:00}");
        }

        /// <summary>지문용 FNV-1a 64비트. 순서까지 포함해 섞는다.</summary>
        sealed class Fnv
        {
            ulong h = 14695981039346656037UL;

            public void Add(int v)
            {
                for (int i = 0; i < 4; i++)
                {
                    h ^= (byte)(v >> (i * 8));
                    h *= 1099511628211UL;
                }
            }

            public void Add(long v) { Add((int)v); Add((int)(v >> 32)); }
            public void Add(ulong v) { Add((long)v); }
            public void Add(bool b) => Add(b ? 1 : 0);

            /// <summary>실수는 비트 그대로. 반올림해서 비교하면 미세한 틀어짐이 통과한다.</summary>
            public void Add(float f) => Add(BitConverter.SingleToInt32Bits(f));

            public ulong Value => h;
            public string Hex => h.ToString("x16");
        }

        /// <summary>새 세계에서 라운드를 돌리고 걸린 시간을 돌려준다. 시즌 넘김은 따로 센다.</summary>
        static double RoundPass(int rounds, bool profile, out int played, out double rollover)
        {
            var w = WorldGenerator.Create(31415);
            var club = w.Clubs.Values.First(c => c.reputation == 4);
            ManagerCareer.TakeCharge(w, club.id, "하네스");

            // 데우기는 버리는 패스가 맡는다. 여기서 몇 회 돌려 버리면 측정 구간이
            // 시즌 1라운드에서 시작하지 않아, perf 의 시즌 평균과 견줄 수 없다.
            SimProfile.Reset();
            SimProfile.Enabled = profile;

            var sw = new System.Diagnostics.Stopwatch();
            var roll = new System.Diagnostics.Stopwatch();
            played = 0;
            while (played < rounds)
            {
                if (w.SeasonFinished)
                {
                    roll.Start();
                    SeasonRollover.StartNextSeason(w);
                    roll.Stop();
                }
                sw.Start();
                var r = GameEngine.PlayNextRound(w);
                sw.Stop();
                if (r == null) break;
                played++;
            }
            SimProfile.Enabled = false;
            rollover = roll.Elapsed.TotalMilliseconds;
            return sw.Elapsed.TotalMilliseconds;
        }
    }
}
