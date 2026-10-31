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
    /// 선수 개인 지시. 지정하지 않은 선수는 중립이어야 하고,
    /// 모든 항목은 거래여야 한다.
    /// </summary>
    public class PlayerInstructionTests
    {
        static PlayerInstructions Ins(Action<PlayerInstructions> set = null)
        {
            var i = new PlayerInstructions();
            set?.Invoke(i);
            return i;
        }

        [Fact]
        public void 지시가_없으면_아무것도_바뀌지_않는다()
        {
            // 쓰지 않는 쪽이 불리해지면 안 된다.
            foreach (var e in new[] { PlayerInstructionEffect.From(null), PlayerInstructionEffect.From(Ins()) })
            {
                Assert.Equal(1f, e.shotShare, 4);
                Assert.Equal(1f, e.shotQuality, 4);
                Assert.Equal(1f, e.creatorShare, 4);
                Assert.Equal(1f, e.teamQuality, 4);
                Assert.Equal(1f, e.cardRisk, 4);
                Assert.Equal(1f, e.defence, 4);
                Assert.Equal(0f, e.possession, 4);
                Assert.Equal(0f, e.fatigue, 4);
            }
        }

        [Fact]
        public void 기본값은_모든_항목에서_중립이다()
        {
            // 한 항목이라도 Default 가 중립이 아니면, 아무것도 건드리지 않은 선수가 보정을 받는다.
            var fresh = new PlayerInstructions();
            foreach (var f in typeof(PlayerInstructions).GetFields())
            {
                var probe = new PlayerInstructions();
                object initial = f.GetValue(fresh);
                f.SetValue(probe, initial);

                var e = PlayerInstructionEffect.From(probe);
                Assert.Equal(1f, e.shotShare, 4);
                Assert.Equal(1f, e.defence, 4);
                Assert.Equal(0f, e.possession, 4);
            }
        }

        [Fact]
        public void 많이_때리면_기회는_늘고_질은_떨어진다()
        {
            var often = PlayerInstructionEffect.From(Ins(i => i.shootingFrequency = ShootingFrequency.ShootOften));
            var rarely = PlayerInstructionEffect.From(Ins(i => i.shootingFrequency = ShootingFrequency.RarelyShoot));

            Assert.True(often.shotShare > 1f);
            Assert.True(often.shotQuality < 1f);
            Assert.True(rarely.shotShare < 1f);
            Assert.True(rarely.shotQuality > 1f);
        }

        [Fact]
        public void 중거리_슈팅은_질을_더_크게_깎는다()
        {
            var often = PlayerInstructionEffect.From(Ins(i => i.shootingFrequency = ShootingFrequency.ShootOften));
            var distance = PlayerInstructionEffect.From(Ins(i => i.longShotTendency = LongShotTendency.ShootFromDistance));

            Assert.True(distance.shotQuality < often.shotQuality);
        }

        [Fact]
        public void 드리블은_기회를_만들지만_공을_잃는다()
        {
            var more = PlayerInstructionEffect.From(Ins(i => i.dribblingTendency = DribblingTendency.DribbleMore));

            Assert.True(more.creatorShare > 1f);
            Assert.True(more.possession < 0f);
        }

        [Fact]
        public void 과감한_패스는_질을_올리고_점유를_내준다()
        {
            var risky = PlayerInstructionEffect.From(Ins(i => i.passingStyle = PassingStyle.Risky));
            var safe = PlayerInstructionEffect.From(Ins(i => i.passingStyle = PassingStyle.Safe));

            Assert.True(risky.teamQuality > 1f);
            Assert.True(risky.possession < 0f);
            Assert.True(safe.teamQuality < 1f);
            Assert.True(safe.possession > 0f);
        }

        [Fact]
        public void 강한_태클은_수비를_올리고_카드를_부른다()
        {
            var hard = PlayerInstructionEffect.From(Ins(i => i.tacklingStyle = TacklingStyle.TackleHarder));
            var easy = PlayerInstructionEffect.From(Ins(i => i.tacklingStyle = TacklingStyle.EasyTackles));

            Assert.True(hard.defence > 1f);
            Assert.True(hard.cardRisk > 1f);
            Assert.True(easy.defence < 1f);
            Assert.True(easy.cardRisk < 1f);
        }

        [Fact]
        public void 많이_침투하면_뒤가_빈다()
        {
            var runs = PlayerInstructionEffect.From(Ins(i => i.offTheBallRuns = OffTheBallRuns.MakeMoreRuns));

            Assert.True(runs.shotShare > 1f);
            Assert.True(runs.defence < 1f);
            Assert.True(runs.fatigue > 0f);
        }

        [Fact]
        public void 개인_압박은_체력과_카드를_쓴다()
        {
            var press = PlayerInstructionEffect.From(
                Ins(i => i.pressingIntensity = FootballTactician.Data.PressingIntensity.PressMore));

            Assert.True(press.possession > 0f);
            Assert.True(press.fatigue > 0f);
            Assert.True(press.cardRisk > 1f);
        }

        [Fact]
        public void 전원_기본값이면_팀_합계도_중립이다()
        {
            var squad = Enumerable.Range(0, 11).Select(_ => new PlayerInstructions()).ToList();
            var (possession, quality, fatigue) = PlayerInstructionEffect.TeamTotals(squad);

            Assert.Equal(0f, possession, 4);
            Assert.Equal(1f, quality, 4);
            Assert.Equal(0f, fatigue, 4);
        }

        [Fact]
        public void 지시가_없는_슬롯이_섞여도_중립이다()
        {
            // 일부만 지정하는 것이 보통이다. null 이 섞여도 터지거나 치우치면 안 된다.
            var squad = new List<PlayerInstructions> { null, new(), null };
            var (possession, quality, fatigue) = PlayerInstructionEffect.TeamTotals(squad);

            Assert.Equal(0f, possession, 4);
            Assert.Equal(1f, quality, 4);
            Assert.Equal(0f, fatigue, 4);
        }

        [Fact]
        public void 개인_지시가_실제로_경기에_닿는다()
        {
            // 화면에서 움직여도 결과가 같으면 장식이다.
            var w = WorldGenerator.Create(31415);
            var clubs = w.Clubs.Values.Where(c => c.playerIds.Count >= 16).Take(2).ToList();

            MatchLineup Make(ClubData c, Action<FormationSlot> tweak = null)
            {
                var tac = FormationDatabase.CreateTactics("4-2-3-1", "검사");
                var squad = w.Squad(c.id).ToList();
                LineupBuilder.Assign(tac, squad);
                RoleDefaults.Fill(tac);
                if (tweak != null) foreach (var s in tac.slots) tweak(s);
                return new MatchLineup { clubId = c.id, tactics = tac, benchIds = LineupBuilder.Bench(tac, squad) };
            }

            double Goals(MatchLineup a, MatchLineup b)
            {
                int g = 0;
                for (int i = 0; i < 600; i++)
                {
                    var r = MatchEngine.Simulate(new MatchSimContext
                    {
                        home = a, away = b, seed = i * 7919 + 13,
                    }, w.Lookup);
                    g += r.homeGoals;
                }
                return g / 600.0;
            }

            var plain = Make(clubs[0]);
            var reckless = Make(clubs[0], s =>
            {
                s.customInstructions = new PlayerInstructions
                {
                    shootingFrequency = ShootingFrequency.ShootOften,
                    longShotTendency = LongShotTendency.ShootFromDistance,
                };
            });
            var foe = Make(clubs[1]);

            Assert.NotEqual(Goals(plain, foe), Goals(reckless, foe), 2);
        }

        [Fact]
        public void 세이브되는_모델에서_초기값이_기본값과_다른_필드는_모두_표시돼_있다()
        {
            // 세이브는 기본값인 항목을 빼고 쓴다(WhenWritingDefault). 초기값이 default(T) 와
            // 다른 필드는 그 default 를 고른 순간 저장에서 빠지고 초기값으로 되살아난다.
            // 전술·개인 지시에서 세 번 겪었으므로, 저장되는 모델 전체를 한 번에 훑는다.
            var types = new[]
            {
                typeof(TeamInstructions),
                typeof(PlayerInstructions),
                typeof(SetPieceRoutine),
                typeof(RoleAssignment),
            };

            var missing = new List<string>();

            foreach (var type in types)
            {
                object fresh = Activator.CreateInstance(type)!;

                foreach (var f in type.GetFields(
                             System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance))
                {
                    // 값 타입만 본다. 참조 타입은 비어 있는 목록이 null 로 바뀌지 않으므로
                    // 초기값이 null 이 아니어도 빠지지 않는다.
                    if (!f.FieldType.IsValueType) continue;

                    object initial = f.GetValue(fresh);
                    object fallback = Activator.CreateInstance(f.FieldType);
                    if (Equals(initial, fallback)) continue;

                    var attr = f.GetCustomAttributes(typeof(System.Text.Json.Serialization.JsonIgnoreAttribute), false)
                        .Cast<System.Text.Json.Serialization.JsonIgnoreAttribute>()
                        .FirstOrDefault();

                    if (attr?.Condition != System.Text.Json.Serialization.JsonIgnoreCondition.Never)
                        missing.Add($"{type.Name}.{f.Name} (초기값 {initial}, 기본값 {fallback})");
                }
            }

            Assert.True(missing.Count == 0,
                "세이브에서 빠질 필드:\n  " + string.Join("\n  ", missing) +
                "\n[JsonIgnore(Condition = JsonIgnoreCondition.Never)] 를 붙이세요.");
        }

        [Fact]
        public void 개인_지시가_세이브를_왕복한다()
        {
            // ShootOften 과 PressMore 는 0 이라 기본값으로 취급돼 빠질 수 있다.
            var w = WorldGenerator.Create(31415);
            var club = w.Clubs.Values.First();
            ManagerCareer.TakeCharge(w, club.id, "테스트");

            var tac = w.MyClub!.savedTactics.First(t => t.id == w.MyClub.activeTacticsId);
            tac.slots[7].customInstructions = new PlayerInstructions
            {
                shootingFrequency = ShootingFrequency.ShootOften,
                pressingIntensity = FootballTactician.Data.PressingIntensity.PressMore,
                tacklingStyle = TacklingStyle.TackleHarder,
                offTheBallRuns = OffTheBallRuns.MakeMoreRuns,
                takePenalties = true,
            };

            var back = SaveGame.Deserialize(SaveGame.Serialize(w, "개인지시")).world!;
            var loaded = back.MyClub!.savedTactics
                .First(t => t.id == back.MyClub.activeTacticsId).slots[7].customInstructions;

            Assert.NotNull(loaded);
            Assert.Equal(ShootingFrequency.ShootOften, loaded!.shootingFrequency);
            Assert.Equal(FootballTactician.Data.PressingIntensity.PressMore, loaded.pressingIntensity);
            Assert.Equal(TacklingStyle.TackleHarder, loaded.tacklingStyle);
            Assert.Equal(OffTheBallRuns.MakeMoreRuns, loaded.offTheBallRuns);
            Assert.True(loaded.takePenalties);
        }
    }
}
