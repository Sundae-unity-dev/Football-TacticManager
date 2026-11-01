using System;
using System.Collections.Generic;
using System.Linq;
using FootballTactician.Balance;
using FootballTactician.Data;
using FootballTactician.Simulation;
using FootballTactician.World;

namespace FT.Sim
{
    /// <summary>
    /// 밸런싱 하네스. UI 없이 실제 MatchEngine 을 돌려 분포를 측정한다.
    /// 사용법: dotnet run -- [match|league|growth|all]
    /// </summary>
    internal static class Program
    {
        static readonly GameDate Today = new GameDate { year = 2026, month = 7, day = 1 };

        static int Main(string[] args)
        {
            string mode = args.Length > 0 ? args[0] : "all";
            Console.OutputEncoding = System.Text.Encoding.UTF8;

            if (mode is "match" or "all") MeasureMatch();
            if (mode is "league" or "all") MeasureLeague();
            if (mode is "growth" or "all") MeasureGrowth();
            if (mode is "sweep") Sweep();
            if (mode is "sens") Sensitivity();
            if (mode is "diag") Diag();
            if (mode is "qref") QRef();
            if (mode is "world") WorldHarness.Run();
            if (mode is "season") WorldHarness.Season(args.Length > 1 ? int.Parse(args[1]) : 1);
            if (mode is "audit") WorldHarness.Audit(args.Length > 1 ? int.Parse(args[1]) : 8);
            if (mode is "qe") WorldHarness.ExponentSweep(args.Length > 1 ? int.Parse(args[1]) : 3);
            if (mode is "market") WorldHarness.Market(args.Length > 1 ? int.Parse(args[1]) : 5);
            if (mode is "cup") WorldHarness.Cup(args.Length > 1 ? int.Parse(args[1]) : 5);
            if (mode is "training") WorldHarness.Training();
            if (mode is "youth") WorldHarness.Youth(args.Length > 1 ? int.Parse(args[1]) : 5);
            if (mode is "savesize") WorldHarness.SaveBreakdown();
            if (mode is "room") WorldHarness.Room(args.Length > 1 ? int.Parse(args[1]) : 3);
            if (mode is "venue") WorldHarness.Venue(args.Length > 1 ? int.Parse(args[1]) : 20);
            if (mode is "tactics") MeasureTactics(args.Length > 1 ? int.Parse(args[1]) : 400);
            if (mode is "roles") WorldHarness.Roles(args.Length > 1 ? int.Parse(args[1]) : 400);
            if (mode is "setpiece") WorldHarness.SetPiece(args.Length > 1 ? int.Parse(args[1]) : 400);
            if (mode is "squad") WorldHarness.Squad(args.Length > 1 ? int.Parse(args[1]) : 3);
            if (mode is "recruit") WorldHarness.Recruit(args.Length > 1 ? int.Parse(args[1]) : 4);
            if (mode is "scout") WorldHarness.Scout(args.Length > 1 ? int.Parse(args[1]) : 4);
            if (mode is "staff") WorldHarness.Staff(args.Length > 1 ? int.Parse(args[1]) : 4);
            if (mode is "wages") WorldHarness.Wages(args.Length > 1 ? int.Parse(args[1]) : 0);
            if (mode is "ages") WorldHarness.Ages(args.Length > 1 ? int.Parse(args[1]) : 12);
            if (mode is "honours") WorldHarness.Honours(args.Length > 1 ? int.Parse(args[1]) : 8);
            if (mode is "biz") WorldHarness.Business(args.Length > 1 ? int.Parse(args[1]) : 8);
            if (mode is "hg") WorldHarness.HomegrownRule(args.Length > 1 ? int.Parse(args[1]) : 6);
            if (mode is "perf") WorldHarness.Perf(args.Length > 1 ? int.Parse(args[1]) : 3);
            if (mode is "perfround") WorldHarness.PerfRound(args.Length > 1 ? int.Parse(args[1]) : 38);
            if (mode is "perfgen") WorldHarness.PerfGen(args.Length > 1 ? int.Parse(args[1]) : 0);
            if (mode is "fingerprint") WorldHarness.Fingerprint(args.Length > 1 ? int.Parse(args[1]) : 38);
            return 0;
        }

        /// <summary>동급 팀의 공격력/수비력 기준 비율을 잰다. 지수 정규화에 쓴다.</summary>
        static void Diag()
        {
            int nextId = 1;
            var a = MakeClub(new Random(777), 1, 1.0f, ref nextId);
            Console.WriteLine("=== 전력 구성 (평판 1.00, 주전 11) ===");
            var tac = FormationDatabase.CreateTactics("4-2-3-1", "t");
            var best = a.Fit.OrderByDescending(Ca).Take(11).ToList();
            Console.WriteLine($"  top11 CA 평균 {best.Average(Ca):F1}  (능력치 환산 {best.Average(p => (float)p.CurrentAbility):F2})");
            foreach (var rep in new[] { 1.35f, 1.15f, 1.0f, 0.9f, 0.8f })
            {
                int nid = 5000;
                var c = MakeClub(new Random(31), 9, rep, ref nid);
                var t11 = c.Fit.OrderByDescending(Ca).Take(11).ToList();
                Console.WriteLine($"  평판 {rep:F2}  CA {t11.Average(Ca):F1}  능력치평균 {t11.Average(p => (float)p.CurrentAbility):F2}");
            }
            // 동급 두 팀을 붙여 ratio 를 역산한다
            {
                int nid = 9000;
                var x = MakeClub(new Random(777), 1, 1.0f, ref nid);
                nid = 9500;
                var y = MakeClub(new Random(777), 2, 1.0f, ref nid);
                foreach (var p in y.Squad) p.clubId = 2;
                var get2 = Lookup(x, y);
                BalanceConfig.QualityExponent = 1.0f;
                BalanceConfig.QualityReference = 1.0f;
                BalanceConfig.HomeAdvantage = 1.0f;
                double gs = 0; int n = 400;
                for (int i = 0; i < n; i++)
                {
                    var ctx = new MatchSimContext { fixtureId = i, home = Lineup(x), away = Lineup(y), seed = i * 101 + 3 };
                    var r = MatchEngine.Simulate(ctx, get2);
                    gs += r.homeGoals + r.awayGoals;
                }
                Console.WriteLine($"  [홈보정 off, QE=1] 동급 경기당 득점 {gs / n:F2}");

                // 지수 정규화의 기준이 될 실제 비율. QualityReference 는 이 값이어야 한다.
                var (atk, def, mid, gk) = MatchEngine.TeamPower(Lineup(x), get2);
                Console.WriteLine($"  동급 팀 전력: 공격 {atk:F2} · 수비 {def:F2} · 중원 {mid:F2} · GK {gk:F2}");
                Console.WriteLine($"  공격/수비 비율 = {atk / MathF.Max(1f, def):F4}  ← QualityReference 기준값");
            }

            // 평판별 비율 — 지수가 증폭해야 할 실제 격차
            {
                BalanceConfig.QualityExponent = 1.0f;
                BalanceConfig.QualityReference = 1.0f;
                BalanceConfig.HomeAdvantage = 1.0f;
                Console.WriteLine("  평판별 공격/수비 비율:");
                foreach (var rep in new[] { 1.35f, 1.0f, 0.8f })
                {
                    int nid = 20000 + (int)(rep * 1000);
                    var c = MakeClub(new Random(31), 9, rep, ref nid);
                    var (a2, d2, _, _) = MatchEngine.TeamPower(Lineup(c), Lookup(c));
                    Console.WriteLine($"    평판 {rep:F2}  공격 {a2:F2} / 수비 {d2:F2} = {a2 / MathF.Max(1f, d2):F4}");
                }
            }
        }

        /// <summary>QE=3 에서 홈 어드밴티지와 기준 비율을 함께 맞춘다.</summary>
        static void QRef()
        {
            BalanceConfig.QualityExponent = 3.0f;
            BalanceConfig.PossessionExponent = 5.0f;
            Console.WriteLine("=== QE=3 · PE=5 에서 HA × QRef 탐색 ===");
            Console.WriteLine("   HA  QRef   홈골  원정골   합계   슈팅   홈승률   판정");
            foreach (float ha in new[] { 1.05f, 1.06f, 1.07f })
                foreach (float qref in new[] { 0.68f, 0.70f, 0.72f, 0.74f, 0.76f })
                {
                    BalanceConfig.HomeAdvantage = ha;
                    BalanceConfig.QualityReference = qref;
                    var (hg, ag, sh, hw) = EqualTeams(900);
                    double tot = hg + ag;
                    bool ok = tot >= 2.4 && tot <= 2.8 && hw >= 0.42 && hw <= 0.48 && sh >= 16 && sh <= 24;
                    Console.WriteLine($"  {ha:F2}  {qref:F2}  {hg,5:F2}  {ag,6:F2}  {tot,6:F2}  {sh,5:F1}  {hw * 100,6:F1}%   {(ok ? "통과" : "")}");
                }
        }

        // ── 민감도 탐색 ───────────────────────────────────────────────────
        /// <summary>리그를 돌려 전력이 순위로 얼마나 전달되는지 본다.</summary>
        static (double gpg, int spread, double repGap, double topRepRank) LeagueMetrics(int seed)
        {
            var rng = new Random(seed);
            int nextId = 1;
            float[] reps = { 1.35f, 1.35f, 1.15f, 1.15f, 1.15f, 1.0f, 1.0f, 1.0f, 1.0f, 1.0f,
                             0.9f, 0.9f, 0.9f, 0.9f, 0.8f, 0.8f, 0.8f, 0.8f, 0.8f, 0.8f };
            var clubs = reps.Select((r, i) => MakeClub(rng, i + 1, r, ref nextId)).ToList();
            var get = Lookup(clubs.ToArray());
            var pts = new int[clubs.Count];
            var gd = new int[clubs.Count];
            int goals = 0, games = 0, sd = 1;

            for (int i = 0; i < clubs.Count; i++)
                for (int j = 0; j < clubs.Count; j++)
                {
                    if (i == j) continue;
                    var ctx = new MatchSimContext
                    { fixtureId = sd, home = Lineup(clubs[i]), away = Lineup(clubs[j]), seed = sd++ * 31 + 7 };
                    var r = MatchEngine.Simulate(ctx, get);
                    goals += r.homeGoals + r.awayGoals; games++;
                    gd[i] += r.homeGoals - r.awayGoals; gd[j] += r.awayGoals - r.homeGoals;
                    if (r.homeGoals > r.awayGoals) pts[i] += 3;
                    else if (r.homeGoals == r.awayGoals) { pts[i]++; pts[j]++; }
                    else pts[j] += 3;
                }

            var order = Enumerable.Range(0, clubs.Count)
                                  .OrderByDescending(k => pts[k]).ThenByDescending(k => gd[k]).ToList();
            double topRep = order.Take(5).Average(k => clubs[k].Reputation);
            double botRep = order.TakeLast(5).Average(k => clubs[k].Reputation);
            // 평판 1.35 두 구단의 평균 순위 (1부터)
            double bigRank = Enumerable.Range(0, clubs.Count)
                .Where(k => clubs[k].Reputation >= 1.35f)
                .Average(k => order.IndexOf(k) + 1);
            return (goals / (double)games, pts[order[0]] - pts[order[^1]], topRep - botRep, bigRank);
        }

        static void Sensitivity()
        {
            Console.WriteLine("=== 전력 민감도 탐색 (리그 3회 평균) ===");
            Console.WriteLine("   QE   PE   득점   승점격차  평판분리  최상위2 평균순위  판정");
            foreach (float qe in new[] { 1.0f, 2.0f, 3.0f, 4.0f, 5.0f })
                foreach (float pe in new[] { 1.0f, 3.0f, 5.0f })
                {
                    BalanceConfig.QualityExponent = qe;
                    BalanceConfig.PossessionExponent = pe;
                    double g = 0, sp = 0, rg = 0, br = 0;
                    for (int s = 0; s < 3; s++)
                    {
                        var m = LeagueMetrics(4242 + s * 97);
                        g += m.gpg; sp += m.spread; rg += m.repGap; br += m.topRepRank;
                    }
                    g /= 3; sp /= 3; rg /= 3; br /= 3;
                    bool ok = sp >= 40 && rg >= 0.20 && br <= 6.0 && g >= 2.2 && g <= 3.0;
                    Console.WriteLine($"  {qe,3:F1}  {pe,3:F1}  {g,5:F2}  {sp,8:F0}  {rg,8:F2}  {br,15:F1}   {(ok ? "통과" : "")}");
                }
            Console.WriteLine();
            Console.WriteLine("  목표: 승점격차 40+, 평판분리 0.20+, 최상위2 평균순위 6위 이내, 득점 2.2~3.0");
        }

        // ── 상수 탐색 ─────────────────────────────────────────────────────
        static void Sweep()
        {
            Console.WriteLine("=== 공격 기본 확률 × 홈 어드밴티지 탐색 ===");
            Console.WriteLine("  base    HA    홈골  원정골   합계   슈팅   홈승률   판정");
            foreach (float b in new[] { 0.50f, 0.52f, 0.54f, 0.56f })
                foreach (float ha in new[] { 1.12f, 1.16f, 1.20f, 1.25f, 1.30f })
                {
                    BalanceConfig.MatchAttackBase = b;
                    BalanceConfig.HomeAdvantage = ha;
                    var (hg, ag, sh, hw) = EqualTeams(1500);
                    bool ok = hg >= BalanceConfig.GoalsPerTeamMin && hg <= BalanceConfig.GoalsPerTeamMax
                           && ag >= BalanceConfig.GoalsPerTeamMin && ag <= BalanceConfig.GoalsPerTeamMax
                           && hw >= BalanceConfig.HomeWinRateMin && hw <= BalanceConfig.HomeWinRateMax
                           && sh >= BalanceConfig.ShotsPerMatchMin && sh <= BalanceConfig.ShotsPerMatchMax;
                    Console.WriteLine($"  {b:F2}  {ha:F2}  {hg,5:F2}  {ag,6:F2}  {hg + ag,6:F2}  {sh,5:F1}  {hw * 100,6:F1}%   {(ok ? "통과" : "")}");
                }
        }

        static (double hg, double ag, double sh, double hw) EqualTeams(int n)
        {
            int nextId = 1;
            var a = MakeClub(new Random(777), 1, 1.0f, ref nextId);
            nextId = 10001;
            var b = MakeClub(new Random(777), 2, 1.0f, ref nextId);
            foreach (var p in b.Squad) p.clubId = 2;
            var get = Lookup(a, b);
            double hg = 0, ag = 0, sh = 0; int hw = 0;
            for (int i = 0; i < n; i++)
            {
                var ctx = new MatchSimContext { fixtureId = i, home = Lineup(a), away = Lineup(b), seed = i * 7919 + 13 };
                var r = MatchEngine.Simulate(ctx, get);
                hg += r.homeGoals; ag += r.awayGoals;
                sh += r.homeStats.shots + r.awayStats.shots;
                if (r.homeGoals > r.awayGoals) hw++;
            }
            return (hg / n, ag / n, sh / n, hw / (double)n);
        }

        // ── 구단 ──────────────────────────────────────────────────────────
        sealed class Club
        {
            public int Id;
            public string Name = "";
            public float Reputation;
            public List<PlayerData> Squad = new();
            public float Coach, Facility;
            public float Revenue;                       // 만 유로/년
            public TacticsData Tactics = null!;

            public IEnumerable<PlayerData> Fit => Squad.Where(p => !p.IsInjured);
            public float TopElevenCa()
            {
                var t = Fit.OrderByDescending(Ca).Take(11).ToList();
                return t.Count == 0 ? 0 : t.Average(Ca);
            }
            public float WageBill() => Squad.Sum(p =>
                BalanceConfig.WeeklyWage(Ca(p), p.GetAge(Today), Reputation)) * 52f / 10000f;
        }

        /// <summary>능력치 평균(1~20) → CA(1~200).</summary>
        static float Ca(PlayerData p) => BalanceConfig.PowerToCa(p.AbilityScore);

        static readonly Position[] Shape =
        {
            Position.GK,
            Position.LB, Position.CB, Position.CB, Position.RB,
            Position.CDM, Position.CDM,
            Position.LW, Position.CAM, Position.RW,
            Position.ST
        };

        /// <summary>
        /// 전술 지시가 거래인지 검사한다.
        ///
        /// 같은 전력의 두 팀을 붙이되 지시만 바꿔 가며 전 조합을 돌린다.
        /// 어떤 설정이 모든 상대에게 이기면 그것만 고르면 되므로 나머지 지시가 장식이 된다.
        /// </summary>
        static void MeasureTactics(int matches)
        {
            Console.WriteLine($"=== 전술 지시 ({matches}경기/조합) ===");

            var options = TacticOptions();
            Console.WriteLine($"  검사 대상 {options.Count}가지\n");

            var points = new double[options.Count];
            var matrix = new double[options.Count, options.Count];
            var worstAgainst = new double[options.Count];
            for (int i = 0; i < options.Count; i++) worstAgainst[i] = 9;

            for (int a = 0; a < options.Count; a++)
            for (int b = 0; b < options.Count; b++)
            {
                if (a == b) continue;
                double ppg = TacticDuel(options[a].ins, options[b].ins, matches, a * 100 + b);
                matrix[a, b] = ppg;
                points[a] += ppg;
                worstAgainst[a] = Math.Min(worstAgainst[a], ppg);
            }

            Console.WriteLine("  설정                     평균승점  최악상대  판정");
            var ranked = Enumerable.Range(0, options.Count)
                .OrderByDescending(i => points[i])
                .ToList();

            foreach (int i in ranked)
            {
                double avg = points[i] / (options.Count - 1);
                // 모든 상대에게 1.70 점을 넘기면 어떤 상대에게도 지지 않는다는 뜻이다.
                string verdict = worstAgainst[i] > 1.70 ? "★ 지배적"
                               : avg > 1.70 ? "강함"
                               : avg < 1.30 ? "약함"
                               : "";
                Console.WriteLine($"  {options[i].name,-24} {avg,8:F2}  {worstAgainst[i],8:F2}  {verdict}");
            }

            double best = ranked.Max(i => points[i]) / (options.Count - 1);
            double worst = ranked.Min(i => points[i]) / (options.Count - 1);
            int dominant = ranked.Count(i => worstAgainst[i] > 1.70);

            // 평균만 보면 상성이 있는지 알 수 없다. 약한 설정에도 이기는 상대가 있어야
            // 고를 이유가 생긴다.
            Console.WriteLine();
            Console.WriteLine("  상성 (행이 열을 상대할 때 행의 경기당 승점)");
            Console.Write("  ".PadRight(18));
            for (int b = 0; b < options.Count; b++) Console.Write($"{b,6}");
            Console.WriteLine();

            int counters = 0;
            for (int a = 0; a < options.Count; a++)
            {
                Console.Write($"  {a} {Short(options[a].name),-14}");
                for (int b = 0; b < options.Count; b++)
                    Console.Write(a == b ? "     ·" : $"{matrix[a, b],6:F2}");
                Console.WriteLine();
            }

            // 전술이 의미 있으려면 "어떤 설정이 센가"가 아니라 "상대에 따라 달라지는가"다.
            // 같은 설정이 상대에 따라 얼마나 흔들리는지를 본다. 38경기 시즌에서
            // 0.25 점/경기는 9~10 점이라 순위를 가른다.
            Console.WriteLine();
            Console.WriteLine("  설정별 상대에 따른 진폭");
            double minSwing = 9;
            for (int a = 0; a < options.Count; a++)
            {
                double hi = 0, lo = 9;
                int bestFoe = 0, worstFoe = 0;
                for (int b = 0; b < options.Count; b++)
                {
                    if (a == b) continue;
                    if (matrix[a, b] > hi) { hi = matrix[a, b]; bestFoe = b; }
                    if (matrix[a, b] < lo) { lo = matrix[a, b]; worstFoe = b; }
                }
                minSwing = Math.Min(minSwing, hi - lo);
                Console.WriteLine($"  {options[a].name,-16} {hi - lo,5:F2}  " +
                                  $"잘 잡는 상대 {Short(options[bestFoe].name),-8} ({hi:F2}) · " +
                                  $"막히는 상대 {Short(options[worstFoe].name)} ({lo:F2})");
            }

            counters = Enumerable.Range(0, options.Count).Count(a =>
            {
                double hi = 0, lo = 9;
                for (int b = 0; b < options.Count; b++)
                {
                    if (a == b) continue;
                    hi = Math.Max(hi, matrix[a, b]);
                    lo = Math.Min(lo, matrix[a, b]);
                }
                return hi - lo >= 0.25;
            });

            Console.WriteLine();
            Console.WriteLine($"  상대에 따라 0.25 이상 흔들리는 설정 {counters}/{options.Count}개");
            Console.WriteLine($"  최소 진폭 {minSwing:F2} (0 에 가까우면 그 설정은 상대를 안 가린다)");
            Console.WriteLine($"  최고-최저 평균 격차 {best - worst:F2} 점/경기");
            Console.WriteLine($"  지배 전략 {dominant}개");
            Console.WriteLine($"  판정: {(dominant == 0 && best - worst < 0.50 && counters >= options.Count * 7 / 10 ? "통과" : "조정 필요")}");
        }

        static string Short(string s) => s.Length <= 7 ? s : s.Substring(0, 7);

        static List<(string name, TeamInstructions ins)> TacticOptions()
        {
            var list = new List<(string, TeamInstructions)>();

            void Add(string name, Action<TeamInstructions> set)
            {
                var ins = new TeamInstructions();
                set(ins);
                list.Add((name, ins));
            }

            Add("기본", _ => { });
            Add("고압박 하이라인", i => { i.pressingIntensity = 5; i.defensiveLine = DefensiveLine.VeryHigh; i.counterPress = true; });
            Add("저블록 역습", i => { i.pressingIntensity = 1; i.defensiveLine = DefensiveLine.VeryLow; i.directCounterAttack = true; i.buildUpStyle = BuildUpStyle.Direct; });
            Add("점유 축구", i => { i.buildUpStyle = BuildUpStyle.ShortPassing; i.tempo = 2; i.defensiveLine = DefensiveLine.High; });
            Add("롱볼", i => { i.buildUpStyle = BuildUpStyle.LongBall; i.tempo = 4; i.defensiveLine = DefensiveLine.Low; });
            Add("빠른 템포", i => { i.tempo = 5; i.width = 5; });
            Add("느린 템포", i => { i.tempo = 1; i.width = 2; });
            Add("오프사이드 트랩", i => { i.defensiveLine = DefensiveLine.High; i.offsideTrap = true; });
            Add("수비적", i => { i.mentality = TeamMentality.Defensive; i.defensiveLine = DefensiveLine.Low; i.tempo = 2; });
            Add("공격적", i => { i.mentality = TeamMentality.Attacking; i.defensiveLine = DefensiveLine.High; i.tempo = 4; });

            return list;
        }

        /// <summary>같은 전력의 두 팀을 붙여 a 의 경기당 승점을 돌려준다.</summary>
        static double TacticDuel(TeamInstructions insA, TeamInstructions insB, int matches, int seed)
        {
            int nextId = 1;
            var a = MakeClub(new Random(4242), 1, 1.0f, ref nextId);
            int nextId2 = 500;
            var b = MakeClub(new Random(4242), 2, 1.0f, ref nextId2);

            var la = Lineup(a);
            var lb = Lineup(b);
            CopyInstructions(insA, la.tactics.instructions);
            CopyInstructions(insB, lb.tactics.instructions);

            var get = Lookup(a, b);
            var rng = new Random(9000 + seed);
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

                var r = MatchEngine.Simulate(ctx, get);
                int ag = aHome ? r.homeGoals : r.awayGoals;
                int bg = aHome ? r.awayGoals : r.homeGoals;
                pts += ag > bg ? 3 : ag == bg ? 1 : 0;
            }

            return (double)pts / matches;
        }

        static void CopyInstructions(TeamInstructions from, TeamInstructions to)
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

        static Club MakeClub(Random rng, int id, float rep, ref int nextPlayerId)
        {
            var c = new Club
            {
                Id = id,
                Name = $"C{id:00}",
                Reputation = rep,
                Coach = 0.5f + (rep - 0.8f) / 0.6f * 1.2f,
                Facility = 0.7f + (rep - 0.8f) / 0.6f * 0.6f,
                Revenue = BalanceConfig.AnnualRevenue(rep),
                Tactics = FormationDatabase.CreateTactics("4-2-3-1", "기본"),
            };

            // 주전 11 은 전성기 나이대로, 백업 14 는 유망주와 노장을 섞는다.
            // 주전에 18세가 배치되면 실현율이 낮아 구단 전력이 평판과 어긋난다.
            for (int i = 0; i < 25; i++)
            {
                var pos = Shape[i % Shape.Length];
                bool starter = i < 11;
                int age = starter ? 23 + rng.Next(8)              // 23~30
                                  : (rng.Next(2) == 0 ? 18 + rng.Next(5)   // 18~22
                                                      : 29 + rng.Next(6)); // 29~34
                c.Squad.Add(PlayerFactory.Create(rng, nextPlayerId++, id, rep, pos, age, starter, Today));
            }
            return c;
        }

        static MatchLineup Lineup(Club c)
        {
            // 게임과 같은 배치 규칙을 쓴다. 전에는 CA 순서대로 슬롯을 채워
            // 스트라이커가 센터백 자리에 서는 라인업으로 수치를 쟀다.
            var tac = FormationDatabase.CreateTactics("4-2-3-1", "기본");
            LineupBuilder.Assign(tac, c.Squad);
            RoleDefaults.Fill(tac);          // 게임과 같은 상태로 맞춘다
            return new MatchLineup
            {
                clubId = c.Id,
                tactics = tac,
                benchIds = LineupBuilder.Bench(tac, c.Squad),
            };
        }

        static Func<int, PlayerData> Lookup(params Club[] clubs)
        {
            var map = clubs.SelectMany(c => c.Squad).ToDictionary(p => p.id);
            return id => map.TryGetValue(id, out var p) ? p : null!;
        }

        // ── 1. 동급 전력 두 팀의 득점 ─────────────────────────────────────
        static void MeasureMatch()
        {
            Console.WriteLine("=== 1. 동급 전력 두 팀 (목표: 팀당 1.20~1.40골) ===");
            const int N = 3000;
            // 전력 차이가 결과를 오염시키지 않도록 같은 시드로 동일 스쿼드를 만든다
            int nextId = 1;
            var a = MakeClub(new Random(777), 1, 1.0f, ref nextId);
            nextId = 10001;
            var b = MakeClub(new Random(777), 2, 1.0f, ref nextId);
            foreach (var p in b.Squad) p.clubId = 2;
            var get = Lookup(a, b);

            double hg = 0, ag = 0, sh = 0, co = 0; int hw = 0, dr = 0, aw = 0;
            int gOpen = 0, gCorner = 0, gFk = 0, gPen = 0, penAwarded = 0;
            for (int i = 0; i < N; i++)
            {
                var ctx = new MatchSimContext
                {
                    fixtureId = i,
                    home = Lineup(a),
                    away = Lineup(b),
                    seed = i * 7919 + 13
                };
                var r = MatchEngine.Simulate(ctx, get);
                hg += r.homeGoals; ag += r.awayGoals;
                sh += r.homeStats.shots + r.awayStats.shots;
                co += r.homeStats.corners + r.awayStats.corners;
                foreach (var e in r.events)
                    switch (e.eventType)
                    {
                        case MatchEventType.Goal: gOpen++; break;
                        case MatchEventType.CornerKickGoal: gCorner++; break;
                        case MatchEventType.FreeKickGoal: gFk++; break;
                        case MatchEventType.PenaltyScored: gPen++; break;
                        case MatchEventType.PenaltyAwarded: penAwarded++; break;
                    }
                if (r.homeGoals > r.awayGoals) hw++;
                else if (r.homeGoals == r.awayGoals) dr++;
                else aw++;
            }
            Console.WriteLine($"  홈 전력 {a.TopElevenCa():F0} / 원정 전력 {b.TopElevenCa():F0}  (CA 환산)");
            Console.WriteLine($"  팀당 득점  홈 {hg / N:F2}  원정 {ag / N:F2}   합계 {(hg + ag) / N:F2}");
            int setGoals = gCorner + gFk + gPen;
            int allGoals = gOpen + setGoals;
            Console.WriteLine($"  득점 출처  열린플레이 {gOpen * 100.0 / allGoals:F0}% · " +
                              $"코너 {gCorner * 100.0 / allGoals:F0}% · " +
                              $"프리킥 {gFk * 100.0 / allGoals:F0}% · " +
                              $"페널티 {gPen * 100.0 / allGoals:F0}%" +
                              $"  (세트피스 합계 {setGoals * 100.0 / allGoals:F0}%)");
            Console.WriteLine($"  경기당 코너 {co / N:F1} · 페널티 {penAwarded / (double)N:F2}");
            Console.WriteLine($"  경기당 슈팅 {sh / N:F1}");
            Console.WriteLine($"  홈 {hw * 100.0 / N:F1}%  무 {dr * 100.0 / N:F1}%  원정 {aw * 100.0 / N:F1}%");
            bool pass = (hg / N) >= BalanceConfig.GoalsPerTeamMin && (hg / N) <= BalanceConfig.GoalsPerTeamMax
                     && (ag / N) >= BalanceConfig.GoalsPerTeamMin && (ag / N) <= BalanceConfig.GoalsPerTeamMax;
            Console.WriteLine($"  판정: {(pass ? "통과" : "실패 — 기준 1.20~1.40")}");
            Console.WriteLine();
        }

        // ── 2. 리그 한 시즌 ───────────────────────────────────────────────
        static void MeasureLeague()
        {
            Console.WriteLine("=== 2. 20팀 리그 (380경기) ===");
            var rng = new Random(424242);
            int nextId = 1;
            float[] reps = { 1.35f, 1.35f, 1.15f, 1.15f, 1.15f, 1.0f, 1.0f, 1.0f, 1.0f, 1.0f,
                             0.9f, 0.9f, 0.9f, 0.9f, 0.8f, 0.8f, 0.8f, 0.8f, 0.8f, 0.8f };
            var clubs = reps.Select((r, i) => MakeClub(rng, i + 1, r, ref nextId)).ToList();
            var get = Lookup(clubs.ToArray());

            var pts = new int[clubs.Count];
            var gf = new int[clubs.Count];
            var ga = new int[clubs.Count];
            int goals = 0, games = 0, seed = 1;

            for (int i = 0; i < clubs.Count; i++)
                for (int j = 0; j < clubs.Count; j++)
                {
                    if (i == j) continue;
                    var ctx = new MatchSimContext
                    {
                        fixtureId = seed,
                        home = Lineup(clubs[i]),
                        away = Lineup(clubs[j]),
                        seed = seed++ * 31 + 7
                    };
                    var r = MatchEngine.Simulate(ctx, get);
                    goals += r.homeGoals + r.awayGoals; games++;
                    gf[i] += r.homeGoals; ga[i] += r.awayGoals;
                    gf[j] += r.awayGoals; ga[j] += r.homeGoals;
                    if (r.homeGoals > r.awayGoals) pts[i] += 3;
                    else if (r.homeGoals == r.awayGoals) { pts[i]++; pts[j]++; }
                    else pts[j] += 3;
                }

            var order = Enumerable.Range(0, clubs.Count)
                                  .OrderByDescending(k => pts[k])
                                  .ThenByDescending(k => gf[k] - ga[k]).ToList();
            Console.WriteLine($"  경기당 총 득점 {goals / (double)games:F2}");
            Console.WriteLine($"  우승 승점 {pts[order[0]]}  최하위 {pts[order[^1]]}  격차 {pts[order[0]] - pts[order[^1]]}");
            Console.WriteLine("  순위 | 구단 | 평판 | 전력 | 승점 | 득실");
            foreach (var k in order.Take(5).Concat(order.TakeLast(3)))
                Console.WriteLine($"   {order.IndexOf(k) + 1,2}  | {clubs[k].Name} | {clubs[k].Reputation:F2} | " +
                                  $"{clubs[k].TopElevenCa():F0} | {pts[k],3} | {gf[k] - ga[k],+4}");
            float topRep = order.Take(5).Average(k => clubs[k].Reputation);
            float botRep = order.TakeLast(5).Average(k => clubs[k].Reputation);
            Console.WriteLine($"  상위5 평판 {topRep:F2} / 하위5 평판 {botRep:F2} — " +
                              $"{(topRep > botRep + 0.15f ? "전력이 순위에 반영됨" : "반영 부족")}");
            Console.WriteLine();
        }

        // ── 3. 성장과 급여 ────────────────────────────────────────────────
        static void MeasureGrowth()
        {
            Console.WriteLine("=== 3. 성장 공식 (평균 환경: 코치 0.9 / 시설 0.9 / 프로의식 1.1) ===");
            (int age, float ca, float pa, string note)[] cases =
            {
                (18, 88, 160, "유망주"), (21, 105, 155, "성장기"), (24, 125, 150, "완성 직전"),
                (27, 140, 150, "전성기"), (31, 145, 155, "하락 시작"), (35, 135, 145, "노장")
            };
            foreach (var (age, ca0, pa, note) in cases)
            {
                float ca = ca0, first = 0;
                for (int w = 0; w < BalanceConfig.WeeksPerSeason; w++)
                {
                    float inc = BalanceConfig.WeeklyGrowth(age, ca, pa, 0.9f, 0.9f, 1.1f, 1.0f);
                    if (w == 0) first = inc;
                    ca = Math.Clamp(ca + inc, 20f, 200f);
                }
                Console.WriteLine($"  {age,2}세 CA{ca0,3}/PA{pa,3} {note,-10} 첫주 {first,+5:F2}  " +
                                  $"시즌 {ca - ca0,+6:F1}  → CA {ca:F0}");
            }

            Console.WriteLine();
            Console.WriteLine("=== 4. 급여 비중 (등급별) ===");
            var rng = new Random(11);
            int nextId = 1;
            foreach (var rep in new[] { 1.35f, 1.15f, 1.0f, 0.9f, 0.8f })
            {
                var c = MakeClub(rng, 99, rep, ref nextId);
                float ratio = c.WageBill() / c.Revenue;
                string verdict = ratio > BalanceConfig.WageSanctionRatio ? "제재"
                               : ratio > BalanceConfig.WageWarnRatio ? "경고" : "정상";
                Console.WriteLine($"  평판 {rep:F2}  전력 {c.TopElevenCa():F0}  수입 {c.Revenue,7:N0}만  " +
                                  $"급여 {c.WageBill(),7:N0}만  비중 {ratio * 100,5:F1}%  {verdict}");
            }
            Console.WriteLine();
        }
    }
}
