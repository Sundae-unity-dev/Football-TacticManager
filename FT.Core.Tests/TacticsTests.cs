using System;
using System.Collections.Generic;
using System.Linq;
using FootballTactician.Balance;
using FootballTactician.Data;
using FootballTactician.Simulation;
using FootballTactician.World;
using Xunit;

namespace FT.Core.Tests
{
    /// <summary>
    /// 전술 지시. 모든 설정은 거래여야 한다 — 공짜로 좋아지는 설정이 있으면
    /// 그것만 고르면 되고 나머지 지시는 화면 장식이 된다.
    /// </summary>
    public class TacticsTests
    {
        static TeamInstructions Ins(Action<TeamInstructions> set = null)
        {
            var i = new TeamInstructions();
            set?.Invoke(i);
            return i;
        }

        static TacticalProfile Resolved(TeamInstructions self, TeamInstructions foe, float foeTrap = 13f)
        {
            var p = TacticalProfile.FromInstructions(self);
            TacticalProfile.Resolve(ref p, self, foe, foeTrap);
            return p;
        }

        [Fact]
        public void 기본_지시는_아무것도_바꾸지_않는다()
        {
            // 기본값에서 보정이 붙으면 지금까지 맞춰 둔 득점·슈팅 수가 통째로 틀어진다.
            var p = Resolved(Ins(), Ins());

            Assert.Equal(0f, p.attackRateBonus, 4);
            Assert.Equal(1f, p.qualityMul, 4);
            Assert.Equal(1f, p.bigChanceMul, 4);
            Assert.Equal(0f, p.possessionBonus, 4);
            Assert.Equal(0f, p.fatiguePerBlock, 4);
        }

        [Theory]
        [InlineData(TeamMentality.VeryAttacking)]
        [InlineData(TeamMentality.Attacking)]
        [InlineData(TeamMentality.Defensive)]
        [InlineData(TeamMentality.VeryDefensive)]
        public void 멘탈리티_배수는_서로_역수다(TeamMentality m)
        {
            // 공격 1.15 · 수비 0.92 처럼 곱이 1 을 넘으면 공격 멘탈리티가 공짜 상향이 되어
            // 상대가 누구든 공격적으로 두는 것이 정답이 된다.
            var (attack, defense) = TacticsHarness.MentalityMultipliers(m);
            Assert.Equal(1f, attack * defense, 2);
        }

        // ── 거래인지 ──────────────────────────────────────────────────────

        [Fact]
        public void 높은_라인은_공을_쥐는_대신_뒷공간을_내준다()
        {
            var high = Resolved(Ins(i => i.defensiveLine = DefensiveLine.VeryHigh), Ins());
            var low = Resolved(Ins(i => i.defensiveLine = DefensiveLine.VeryLow), Ins());

            Assert.True(high.possessionBonus > low.possessionBonus);

            // 상대 입장에서 본 빅찬스 — 높은 라인을 상대하면 늘어야 한다.
            var vsHigh = Resolved(Ins(), Ins(i => i.defensiveLine = DefensiveLine.VeryHigh));
            var vsLow = Resolved(Ins(), Ins(i => i.defensiveLine = DefensiveLine.VeryLow));
            Assert.True(vsHigh.bigChanceMul > vsLow.bigChanceMul);
        }

        [Fact]
        public void 빠른_템포는_많이_때리고_적게_넣는다()
        {
            var fast = Resolved(Ins(i => i.tempo = 5), Ins());
            var slow = Resolved(Ins(i => i.tempo = 1), Ins());

            Assert.True(fast.attackRateBonus > slow.attackRateBonus);
            Assert.True(fast.qualityMul < slow.qualityMul);
            Assert.True(fast.fatiguePerBlock > slow.fatiguePerBlock);
        }

        [Fact]
        public void 압박은_체력을_쓴다()
        {
            var hard = Resolved(Ins(i => { i.pressingIntensity = 5; i.counterPress = true; }), Ins());
            var easy = Resolved(Ins(i => i.pressingIntensity = 1), Ins());

            Assert.True(hard.fatiguePerBlock > easy.fatiguePerBlock);
            Assert.Equal(0f, easy.fatiguePerBlock, 4);
        }

        [Fact]
        public void 경기_중_피로가_경기력을_깎는다()
        {
            // 피로가 경기력에 닿지 않으면 압박의 대가가 사라진다.
            Assert.Equal(1f, TacticalProfile.FatigueFactor(0f), 4);
            Assert.Equal(1f - BalanceConfig.InMatchFatigueSwing, TacticalProfile.FatigueFactor(1f), 4);
            Assert.True(TacticalProfile.FatigueFactor(0.5f) < 1f);
        }

        // ── 궁합 ──────────────────────────────────────────────────────────

        [Fact]
        public void 롱볼은_압박을_넘기고_짧은_패스는_당한다()
        {
            var press = Ins(i => { i.pressingIntensity = 5; i.counterPress = true; });

            var longBall = Resolved(Ins(i => i.buildUpStyle = BuildUpStyle.LongBall), press);
            var shortPass = Resolved(Ins(i => i.buildUpStyle = BuildUpStyle.ShortPassing), press);

            // 압박이 깎는 양만 비교해야 한다. 빌드업 자체의 질 차이는 빼고 본다.
            var longAlone = Resolved(Ins(i => i.buildUpStyle = BuildUpStyle.LongBall), Ins());
            var shortAlone = Resolved(Ins(i => i.buildUpStyle = BuildUpStyle.ShortPassing), Ins());

            float longLoss = 1f - longBall.qualityMul / longAlone.qualityMul;
            float shortLoss = 1f - shortPass.qualityMul / shortAlone.qualityMul;

            Assert.True(shortLoss > longLoss,
                $"짧은 패스가 압박에 덜 당한다 (짧게 {shortLoss:P1}, 길게 {longLoss:P1})");
        }

        [Fact]
        public void 역습은_높은_라인에만_값어치가_있다()
        {
            var vsHigh = Resolved(Ins(i => i.directCounterAttack = true),
                                  Ins(i => i.defensiveLine = DefensiveLine.VeryHigh));
            var vsLow = Resolved(Ins(i => i.directCounterAttack = true),
                                 Ins(i => i.defensiveLine = DefensiveLine.VeryLow));

            // 같은 상대를 역습 없이 상대했을 때와 비교한다.
            var plainHigh = Resolved(Ins(), Ins(i => i.defensiveLine = DefensiveLine.VeryHigh));
            var plainLow = Resolved(Ins(), Ins(i => i.defensiveLine = DefensiveLine.VeryLow));

            Assert.True(vsHigh.bigChanceMul > plainHigh.bigChanceMul, "높은 라인에 역습이 안 통한다");
            Assert.True(vsLow.bigChanceMul < plainLow.bigChanceMul, "뒷공간이 없는데 역습이 공짜다");
        }

        [Fact]
        public void 내려선_상대는_내_슈팅을_줄인다()
        {
            // 이게 없으면 수비적 설정은 내 슈팅만 깎고 상대는 그대로라 순수한 손해가 된다.
            var vsDeep = Resolved(Ins(), Ins(i =>
            {
                i.mentality = TeamMentality.VeryDefensive;
                i.defensiveLine = DefensiveLine.VeryLow;
            }));

            Assert.True(vsDeep.attackRateBonus < 0);
        }

        [Fact]
        public void 넓은_폭은_내려선_상대에게_좁은_폭은_올라온_상대에게()
        {
            var wideVsDeep = Resolved(Ins(i => i.width = 5), Ins(i => i.defensiveLine = DefensiveLine.VeryLow));
            var narrowVsDeep = Resolved(Ins(i => i.width = 1), Ins(i => i.defensiveLine = DefensiveLine.VeryLow));
            Assert.True(wideVsDeep.qualityMul > narrowVsDeep.qualityMul);

            var wideVsHigh = Resolved(Ins(i => i.width = 5), Ins(i => i.defensiveLine = DefensiveLine.VeryHigh));
            var narrowVsHigh = Resolved(Ins(i => i.width = 1), Ins(i => i.defensiveLine = DefensiveLine.VeryHigh));
            Assert.True(narrowVsHigh.qualityMul > wideVsHigh.qualityMul);
        }

        [Fact]
        public void 오프사이드_트랩은_수비수가_좋아야_이득이다()
        {
            var trap = Ins(i => i.offsideTrap = true);

            var vsGood = Resolved(Ins(), trap, foeTrap: 18f);
            var vsPoor = Resolved(Ins(), trap, foeTrap: 7f);
            var vsNone = Resolved(Ins(), Ins());

            Assert.True(vsGood.bigChanceMul < vsNone.bigChanceMul, "좋은 수비진의 트랩이 안 먹는다");
            Assert.True(vsPoor.bigChanceMul > vsNone.bigChanceMul, "못 맞추는 트랩이 공짜다");
        }

        [Fact]
        public void 트랩_숙련은_수비수만_센다()
        {
            var w = WorldGenerator.Create(31415);
            var club = w.Clubs.Values.First();
            var tac = club.savedTactics.First(t => t.id == club.activeTacticsId);
            var starters = tac.slots.Select(s => w.Player(s.assignedPlayerId)).ToArray();

            float skill = TacticalProfile.TrapSkill(starters);
            Assert.InRange(skill, 1f, 20f);
        }

        // ── 지배 전략이 없는지 ─────────────────────────────────────────────

        [Fact]
        public void 어떤_설정도_모든_상대에게_이기지_않는다()
        {
            // 하나라도 모든 상대를 이기면 전술 화면은 그 설정을 찾는 수수께끼가 된다.
            var options = Presets();
            var matrix = new double[options.Count, options.Count];

            for (int a = 0; a < options.Count; a++)
            for (int b = 0; b < options.Count; b++)
            {
                if (a == b) continue;
                matrix[a, b] = TacticsHarness.Duel(options[a].ins, options[b].ins, 120, a * 31 + b);
            }

            for (int a = 0; a < options.Count; a++)
            {
                double worst = 9;
                for (int b = 0; b < options.Count; b++)
                    if (a != b) worst = Math.Min(worst, matrix[a, b]);

                Assert.True(worst < 1.80,
                    $"{options[a].name} 이 모든 상대에게 이긴다 (최악 상대에게도 {worst:F2}점)");
            }
        }

        [Fact]
        public void 전술은_상대에_따라_유불리가_갈린다()
        {
            // 상대를 가리지 않으면 매 경기 고를 이유가 없다.
            var options = Presets();

            foreach (var (name, ins) in options)
            {
                double hi = 0, lo = 9;
                foreach (var (_, foe) in options)
                {
                    if (ReferenceEquals(ins, foe)) continue;
                    // 120 경기로는 표준오차가 0.1 을 넘어 진폭을 못 잰다.
                    double ppg = TacticsHarness.Duel(ins, foe, 400, name.Length * 17 + options.IndexOf((name, ins)));
                    hi = Math.Max(hi, ppg);
                    lo = Math.Min(lo, ppg);
                }

                Assert.True(hi - lo > 0.15,
                    $"{name} 은 상대를 가리지 않는다 (진폭 {hi - lo:F2})");
            }
        }

        // ── 세이브 ────────────────────────────────────────────────────────

        [Fact]
        public void 모든_전술_지시가_세이브를_왕복한다()
        {
            // 세이브는 기본값을 빼고 쓴다. 열거형 0번 값(짧은 패스, 매우 수비적, 매우 낮은 라인)은
            // 빠진 채 저장되고, 불러올 때 필드 초기값으로 되살아나 감독의 선택이 사라진다.
            var w = WorldGenerator.Create(31415);
            var club = w.Clubs.Values.First();
            ManagerCareer.TakeCharge(w, club.id, "테스트");

            var tac = w.MyClub!.savedTactics.First(t => t.id == w.MyClub.activeTacticsId);
            var mine = tac.instructions;
            mine.mentality = TeamMentality.VeryDefensive;       // 0
            mine.buildUpStyle = BuildUpStyle.ShortPassing;      // 0
            mine.defensiveLine = DefensiveLine.VeryLow;         // 0
            mine.pressingIntensity = 1;
            mine.tempo = 1;
            mine.width = 1;
            mine.counterPress = true;
            mine.offsideTrap = true;
            mine.directCounterAttack = true;

            var back = SaveGame.Deserialize(SaveGame.Serialize(w, "전술")).world!;
            var loaded = back.MyClub!.savedTactics
                .First(t => t.id == back.MyClub.activeTacticsId).instructions;

            Assert.Equal(TeamMentality.VeryDefensive, loaded.mentality);
            Assert.Equal(BuildUpStyle.ShortPassing, loaded.buildUpStyle);
            Assert.Equal(DefensiveLine.VeryLow, loaded.defensiveLine);
            Assert.Equal(1, loaded.pressingIntensity);
            Assert.Equal(1, loaded.tempo);
            Assert.Equal(1, loaded.width);
            Assert.True(loaded.counterPress);
            Assert.True(loaded.offsideTrap);
            Assert.True(loaded.directCounterAttack);
        }

        [Fact]
        public void 전술_슬롯_좌표가_세이브_뒤에_되살아난다()
        {
            // 좌표는 저장하지 않고 프리셋에서 되살린다. 되살리지 않으면 전부 0 이 되어
            // 전술 보드의 선수 열한 명이 왼쪽 아래 구석에 겹쳐 그려진다.
            var w = WorldGenerator.Create(31415);
            var club = w.Clubs.Values.First();
            var tac = club.savedTactics.First(t => t.id == club.activeTacticsId);
            var before = tac.slots.ToDictionary(s => s.slotIndex,
                s => (s.attackingX, s.attackingY, s.defendingX, s.defendingY));

            Assert.Contains(before.Values, c => c.attackingX != 0f || c.attackingY != 0f);

            var back = SaveGame.Deserialize(SaveGame.Serialize(w, "좌표")).world!;
            var loaded = back.Club(club.id)!.savedTactics.First(t => t.id == club.activeTacticsId);

            foreach (var slot in loaded.slots)
            {
                var (ax, ay, dx, dy) = before[slot.slotIndex];
                Assert.Equal(ax, slot.attackingX, 3);
                Assert.Equal(ay, slot.attackingY, 3);
                Assert.Equal(dx, slot.defendingX, 3);
                Assert.Equal(dy, slot.defendingY, 3);
            }
        }

        [Fact]
        public void 계약과_시즌기록이_배열로_저장돼도_값이_같다()
        {
            // 선수당 객체를 줄이려고 숫자 배열로 바꿨다. 순서가 곧 포맷이라 어긋나면
            // 주급과 만료일이 뒤섞인 채로 읽힌다.
            var w = WorldGenerator.Create(31415);
            var p = w.Players.Values.First(x => x.clubId > 0);
            p.seasonStats = new PlayerStats
            {
                appearances = 31, goals = 12, assists = 7,
                yellowCards = 3, redCards = 1,
                averageRating = 7.24f, minutesPlayed = 2650f,
                xG = 10.5f, xA = 6.25f, cleanSheets = 4,
                progressiveCarries = 121f, pressureSuccessRate = 0.37f,
            };

            var c = p.contract!;
            var back = SaveGame.Deserialize(SaveGame.Serialize(w, "계약")).world!;
            var loaded = back.Player(p.id)!;

            Assert.Equal(c.clubId, loaded.contract!.clubId);
            Assert.Equal(c.weeklyWage, loaded.contract.weeklyWage);
            Assert.Equal(c.startDate, loaded.contract.startDate);
            Assert.Equal(c.expiryDate, loaded.contract.expiryDate);
            Assert.Equal(c.status, loaded.contract.status);

            var s = loaded.seasonStats!;
            Assert.Equal(31, s.appearances);
            Assert.Equal(12, s.goals);
            Assert.Equal(7, s.assists);
            Assert.Equal(7.24f, s.averageRating, 2);
            Assert.Equal(2650f, s.minutesPlayed, 0);
            Assert.Equal(10.5f, s.xG, 2);
            Assert.Equal(6.25f, s.xA, 2);
            Assert.Equal(0.37f, s.pressureSuccessRate, 2);
        }

        [Fact]
        public void 초기값이_기본값과_다른_필드는_항상_저장하도록_표시돼_있다()
        {
            // 앞으로 지시를 추가할 때도 같은 함정에 빠지지 않도록 반사로 검사한다.
            var fresh = new TeamInstructions();

            foreach (var f in typeof(TeamInstructions).GetFields(
                         System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance))
            {
                object initial = f.GetValue(fresh);
                object fallback = f.FieldType.IsValueType ? Activator.CreateInstance(f.FieldType) : null;
                if (Equals(initial, fallback)) continue;    // 초기값 == default 면 안전하다

                var attr = f.GetCustomAttributes(
                    typeof(System.Text.Json.Serialization.JsonIgnoreAttribute), false)
                    .Cast<System.Text.Json.Serialization.JsonIgnoreAttribute>()
                    .FirstOrDefault();

                Assert.True(
                    attr?.Condition == System.Text.Json.Serialization.JsonIgnoreCondition.Never,
                    $"{f.Name} 의 초기값({initial})이 기본값({fallback})과 달라 세이브에서 빠진다. " +
                    "[JsonIgnore(Condition = JsonIgnoreCondition.Never)] 를 붙이세요.");
            }
        }

        // ── AI 구단의 색깔 ────────────────────────────────────────────────

        [Fact]
        public void AI_구단은_저마다_다른_전술을_쓴다()
        {
            // 전부 기본값이면 매 경기 같은 상대라 전술을 고를 이유가 없다.
            var w = WorldGenerator.Create(31415);

            var styles = w.Clubs.Values
                .Select(c => c.savedTactics.First(t => t.id == c.activeTacticsId).instructions)
                .ToList();

            Assert.True(styles.Select(i => i.buildUpStyle).Distinct().Count() >= 3);
            Assert.True(styles.Select(i => i.defensiveLine).Distinct().Count() >= 3);
            Assert.True(styles.Select(i => i.mentality).Distinct().Count() >= 2);

            // 한 가지 색깔이 절반을 넘으면 사실상 단조롭다.
            int most = styles.GroupBy(i => (i.buildUpStyle, i.defensiveLine)).Max(g => g.Count());
            Assert.True(most < styles.Count / 2,
                $"한 조합이 {most}/{styles.Count}구단을 차지한다");
        }

        [Fact]
        public void 약팀은_강팀_흉내를_내지_않는다()
        {
            // 약팀이 고압박 하이라인을 쓰면 전력 차에 더해 전술로도 무너진다.
            var w = WorldGenerator.Create(31415);

            foreach (var club in w.Clubs.Values.Where(c => c.reputation <= 2))
            {
                var ins = club.savedTactics.First(t => t.id == club.activeTacticsId).instructions;
                Assert.NotEqual(DefensiveLine.VeryHigh, ins.defensiveLine);
            }
        }

        [Fact]
        public void 트랩은_수비진이_받쳐_줄_때만_건다()
        {
            var w = WorldGenerator.Create(31415);

            foreach (var club in w.Clubs.Values)
            {
                var tac = club.savedTactics.First(t => t.id == club.activeTacticsId);
                if (!tac.instructions.offsideTrap) continue;

                var starters = tac.slots.Select(s => w.Player(s.assignedPlayerId)).ToArray();
                Assert.True(TacticalProfile.TrapSkill(starters) >= BalanceConfig.TrapBreakEven,
                    $"{club.name} 이 못 맞추는 트랩을 걸고 있다");
            }
        }

        [Fact]
        public void 전술_색깔이_전력_순위를_뒤집지_않는다()
        {
            // 전술이 전력을 압도하면 선수를 모을 이유가 사라진다.
            var w = WorldGenerator.Create(31415);
            var club = w.Clubs.Values.First(c => c.reputation == 3);
            ManagerCareer.TakeCharge(w, club.id, "테스트");

            int guard = 0;
            while (!w.SeasonFinished && guard++ < 60)
                if (GameEngine.PlayNextRound(w) == null) break;

            var league = w.MyLeague!;
            var byStrength = league.clubIds.OrderByDescending(id => w.Strength(id)).ToList();
            double error = league.currentSeason.standings
                .Average(e => Math.Abs(byStrength.IndexOf(e.clubId) + 1 - e.position));

            // 무작위면 약 6.7. 전술이 전력을 덮으면 이 값이 치솟는다.
            Assert.True(error < 4.0, $"전력 순위 대비 오차 {error:F2} — 전술이 전력을 덮는다");
        }

        static List<(string name, TeamInstructions ins)> Presets() => new()
        {
            ("기본", Ins()),
            ("고압박 하이라인", Ins(i => { i.pressingIntensity = 5; i.defensiveLine = DefensiveLine.VeryHigh; i.counterPress = true; })),
            ("저블록 역습", Ins(i => { i.pressingIntensity = 1; i.defensiveLine = DefensiveLine.VeryLow; i.directCounterAttack = true; i.buildUpStyle = BuildUpStyle.Direct; })),
            ("점유 축구", Ins(i => { i.buildUpStyle = BuildUpStyle.ShortPassing; i.tempo = 2; i.defensiveLine = DefensiveLine.High; })),
            ("롱볼", Ins(i => { i.buildUpStyle = BuildUpStyle.LongBall; i.tempo = 4; i.defensiveLine = DefensiveLine.Low; })),
            // 상대가 넷뿐이면 진폭이 작게 잡힌다. 날이 선 조합까지 넣어 실제 폭을 본다.
            ("빠른 템포", Ins(i => { i.tempo = 5; i.width = 5; })),
            ("공격적", Ins(i => { i.mentality = TeamMentality.Attacking; i.defensiveLine = DefensiveLine.High; i.tempo = 4; })),
            ("오프사이드 트랩", Ins(i => { i.defensiveLine = DefensiveLine.High; i.offsideTrap = true; })),
        };
    }
}
