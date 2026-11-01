using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text.Json;
using FootballTactician.Data;
using FootballTactician.World;
using Xunit;

namespace FT.Core.Tests
{
    /// <summary>
    /// 선수는 이름 없는 배열 하나로 저장된다(<c>PlayerConverter</c>). 순서가 곧 포맷이라
    /// 자리를 하나 빠뜨리면 그 뒤가 전부 밀리는데, 밀린 값도 숫자라 조용히 읽힌다 —
    /// 다음 판을 불러올 때야 드러난다. 그래서 필드를 하나하나 세지 않고 <b>반사로 전수</b> 센다.
    /// </summary>
    public class PlayerSaveFormatTests
    {
        static GameWorld Make(int seed) => WorldGenerator.Create(seed);

        /// <summary>중첩 묶음은 각자의 변환기가 있고 이미 다른 테스트가 전수로 본다.</summary>
        static readonly string[] NestedByTheirOwnConverter =
        {
            "technical", "physical", "mental", "goalkeeper", "hidden",
            "contract", "homegrown", "workPermit", "seasonStats", "careerStats",
            "roleAssignment", "injury", "recentRatings",
        };

        static IEnumerable<FieldInfo> SavedScalarFields() =>
            typeof(PlayerData)
                .GetFields(BindingFlags.Public | BindingFlags.Instance)
                .Where(f => f.GetCustomAttribute<System.Text.Json.Serialization.JsonIgnoreAttribute>() == null)
                .Where(f => !NestedByTheirOwnConverter.Contains(f.Name));

        [Fact]
        public void 선수의_모든_낱값이_왕복한다()
        {
            var w = Make(24680);
            var club = w.Clubs.Values.First(c => c.reputation == 3);
            ManagerCareer.TakeCharge(w, club.id, "테스트 감독");
            for (int i = 0; i < 6; i++) GameEngine.PlayNextRound(w);

            var back = SaveGame.Deserialize(SaveGame.Serialize(w, "선수")).world;
            var fields = SavedScalarFields().ToList();

            // 셀 것이 실제로 있는지부터 — 반사가 빈 목록을 주면 테스트가 통과만 한다.
            Assert.True(fields.Count >= 20, $"선수의 낱값이 {fields.Count}개뿐입니다");

            foreach (var a in w.Players.Values)
            {
                var b = back.Player(a.id);
                Assert.NotNull(b);

                foreach (var f in fields)
                    Assert.Equal($"{f.Name}={f.GetValue(a)}", $"{f.Name}={f.GetValue(b)}");
            }
        }

        [Fact]
        public void 부상과_역할도_왕복한다()
        {
            // 둘 다 대부분의 선수에게 비어 있어서, 비어 있는 쪽만 보면 통과한다.
            // roleAssignment 는 아직 아무도 채우지 않으므로(모델에만 있는 자리)
            // 여기서 직접 채워 자리가 제대로 왕복하는지 본다.
            var w = Make(13579);
            var club = w.Clubs.Values.First(c => c.reputation == 3);
            ManagerCareer.TakeCharge(w, club.id, "테스트 감독");
            for (int i = 0; i < 12; i++) GameEngine.PlayNextRound(w);

            var injured = w.Players.Values.Where(p => p.injury != null).ToList();
            Assert.NotEmpty(injured);

            var roled = w.Players.Values.Take(3).ToList();
            roled[0].roleAssignment = new RoleAssignment
            {
                role = PlayerRole.BallPlayingDefender, duty = RoleDuty.Defend, suitability = 77,
            };
            roled[1].roleAssignment = new RoleAssignment
            {
                role = PlayerRole.Poacher, duty = RoleDuty.Attack, suitability = 3,
            };
            // roled[2] 는 비워 둔다 — null 자리를 건너뛰면 그 뒤가 밀린다.

            var back = SaveGame.Deserialize(SaveGame.Serialize(w, "부상")).world;

            foreach (var a in injured)
            {
                var b = back.Player(a.id)!.injury;
                Assert.NotNull(b);
                Assert.Equal(a.injury.injuryType, b!.injuryType);
                Assert.Equal(a.injury.severity, b.severity);
                Assert.Equal(a.injury.injuryDate, b.injuryDate);
                Assert.Equal(a.injury.expectedReturnDate, b.expectedReturnDate);
                Assert.Equal(a.injury.isActive, b.isActive);
            }

            foreach (var a in roled.Where(p => p.roleAssignment != null))
            {
                var b = back.Player(a.id)!.roleAssignment;
                Assert.NotNull(b);
                Assert.Equal(a.roleAssignment.role, b!.role);
                Assert.Equal(a.roleAssignment.duty, b.duty);
                Assert.Equal(a.roleAssignment.suitability, b.suitability);
            }

            Assert.Null(back.Player(roled[2].id)!.roleAssignment);

            // 비어 있는 쪽도 비어서 돌아와야 한다.
            foreach (var a in w.Players.Values.Where(p => p.injury == null).Take(50))
                Assert.Null(back.Player(a.id)!.injury);
        }

        [Fact]
        public void 임대하지_않은_선수는_원소속이_비어서_돌아온다()
        {
            // 배열에는 null 을 쓸 수 없어 0 으로 적는다. 0 을 그대로 읽으면
            // 모든 선수가 "0번 구단에서 임대 온 선수"가 된다.
            var w = Make(11223);
            var back = SaveGame.Deserialize(SaveGame.Serialize(w, "임대")).world;

            foreach (var a in w.Players.Values.Take(500))
                Assert.Equal(a.loanedFromClubId, back.Player(a.id)!.loanedFromClubId);

            Assert.All(back.Players.Values.Where(p => !p.isOnLoan),
                       p => Assert.Null(p.loanedFromClubId));
        }

        [Fact]
        public void 선수는_객체가_아니라_배열로_저장된다()
        {
            // 포맷이 조용히 객체로 돌아가면(변환기를 등록에서 빠뜨리면) 세이브가
            // 두 배로 불어나기만 하고 아무 테스트도 깨지지 않는다.
            var w = Make(31415);
            var p = w.Players.Values.First();

            string json = SaveGame.SampleJson(p);

            Assert.StartsWith("[", json);
            Assert.DoesNotContain("\"marketValue\"", json);
        }

        [Fact]
        public void 전술_슬롯도_배열로_저장되고_그대로_돌아온다()
        {
            // 슬롯은 숫자 다섯 개인데 이름이 값보다 길었다(240구단 × 11자리).
            // 개인 지시는 감독이 손댄 자리에만 붙으므로, 붙은 자리와 안 붙은 자리를 함께 본다.
            var w = Make(31415);
            var club = w.Clubs.Values.First();
            var tac = club.savedTactics.First(t => t.id == club.activeTacticsId);

            tac.slots[3].customInstructions = new PlayerInstructions
            {
                shootingFrequency = ShootingFrequency.ShootOften,
                passingRange = PassingRange.Long,
            };
            tac.slots[5].role = null;          // 역할이 비어 있는 자리도 있다

            var back = SaveGame.Deserialize(SaveGame.Serialize(w, "슬롯")).world!;
            var loaded = back.Club(club.id)!.savedTactics.First(t => t.id == tac.id);

            Assert.Equal(tac.slots.Count, loaded.slots.Count);
            for (int i = 0; i < tac.slots.Count; i++)
            {
                var a = tac.slots[i];
                var b = loaded.slots[i];
                Assert.Equal(a.slotIndex, b.slotIndex);
                Assert.Equal(a.position, b.position);
                Assert.Equal(a.assignedPlayerId, b.assignedPlayerId);
                Assert.Equal(a.role?.role, b.role?.role);
                Assert.Equal(a.role?.duty, b.role?.duty);
                Assert.Equal(a.role?.suitability, b.role?.suitability);
            }

            Assert.Null(loaded.slots[5].role);
            Assert.Null(loaded.slots[0].customInstructions);
            Assert.Equal(ShootingFrequency.ShootOften, loaded.slots[3].customInstructions!.shootingFrequency);
            Assert.Equal(PassingRange.Long, loaded.slots[3].customInstructions!.passingRange);
        }

        [Fact]
        public void 뒤쪽_0_을_빼도_같은_값으로_읽힌다()
        {
            // AttrJson.WriteArray 가 뒤쪽 0 을 적지 않는다. 한 경기도 뛰지 않은 선수의
            // 기록이 [] 로 적히는데, 읽는 쪽이 모자란 자리를 0 으로 둬야 값이 같다.
            var w = Make(97531);
            var back = SaveGame.Deserialize(SaveGame.Serialize(w, "빈기록")).world;

            foreach (var a in w.Players.Values.Take(300))
            {
                var b = back.Player(a.id)!;
                Assert.Equal(a.seasonStats?.appearances, b.seasonStats?.appearances);
                Assert.Equal(a.seasonStats?.goals, b.seasonStats?.goals);
                Assert.Equal(a.careerStats?.appearances, b.careerStats?.appearances);
                Assert.Equal(a.contract?.hasNoShopClause, b.contract?.hasNoShopClause);
                Assert.Equal(a.contract?.agentId, b.contract?.agentId);
                Assert.Equal(a.homegrown?.isClubAcademy, b.homegrown?.isClubAcademy);

                // 비어 있던 묶음은 비어서 돌아와야 한다 — [] 를 객체로 되살리면 안 된다.
                Assert.Equal(a.careerStats == null, b.careerStats == null);
                Assert.Equal(a.homegrown == null, b.homegrown == null);
            }
        }
    }
}
