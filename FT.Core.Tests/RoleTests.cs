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
    /// 역할과 임무. 역할을 고르는 것이 결정이어야 하고,
    /// 역할을 쓰지 않는 쪽이 불리해져서도 안 된다.
    /// </summary>
    public class RoleTests
    {
        static GameWorld Make(int seed = 31415) => WorldGenerator.Create(seed);

        // ── 쓰지 않아도 손해가 아니어야 한다 ───────────────────────────────

        [Fact]
        public void 역할을_지정하지_않은_전술은_깎이지_않는다()
        {
            // 없는 것을 벌하면 역할을 쓰지 않는 쪽이 불리해진다.
            // 실제로 그렇게 뒀을 때 동급 경기 득점이 2.55 에서 1.63 으로 떨어졌다.
            Assert.Equal(1f, RoleFit.BalanceMultiplier(new RoleAssignment[0]), 4);
            Assert.Equal(1f, RoleFit.BalanceMultiplier(new RoleAssignment[] { null, null, null }), 4);
        }

        [Fact]
        public void 관례_역할은_최적이_아니다()
        {
            // 관례값이 늘 최적이면 감독이 역할을 손댈 이유가 사라진다.
            // AI 가 이미 맞춰 둔 구단이 있으므로, 관례값 자체를 선수들과 대 봐야 한다.
            var w = Make();
            int better = 0, total = 0;

            foreach (var p in w.Players.Values.Where(x => x.clubId > 0).Take(600))
            {
                foreach (var pos in new[] { Position.CB, Position.CM, Position.ST, Position.LW })
                {
                    if ((p.positionFlags & pos) == 0) continue;

                    total++;
                    var convention = RoleDefaults.ForPosition(pos);
                    if (RoleFit.Best(p, pos) is { } best && best != convention) better++;
                }
            }

            Assert.True(total > 0);
            Assert.True(better > total / 4,
                $"{total}명 중 {better}명만 관례값이 아닌 역할이 더 맞는다 — 바꿀 이유가 약하다");
        }

        // ── 임무 ──────────────────────────────────────────────────────────

        [Fact]
        public void 임무는_총량을_바꾸지_않는다()
        {
            // 올리기만 하면 전원을 공격 임무로 두는 것이 정답이 된다.
            foreach (var def in RoleDatabase.All.Values)
            foreach (var duty in Enum.GetValues<RoleDuty>())
            {
                var (atk, dfn) = RoleFit.DutyShift(def, duty);
                Assert.Equal(2f, atk + dfn, 4);
            }
        }

        [Fact]
        public void 역할의_기본_임무는_보정이_없다()
        {
            // 절대값으로 재면 수비 임무만 허용하는 전문 역할이 자동으로 더 수비적이 되는데,
            // 그건 포지션 가중치가 이미 반영한 것이라 두 번 걸린다.
            // 실제로 그렇게 뒀을 때 리그 득점이 2.74 에서 2.32 로 내려갔다.
            foreach (var def in RoleDatabase.All.Values)
            {
                var (atk, dfn) = RoleFit.DutyShift(def, RoleFit.DefaultDuty(def));
                Assert.Equal(1f, atk, 4);
                Assert.Equal(1f, dfn, 4);
            }
        }

        [Fact]
        public void 기본_임무는_허용된_임무_중_하나다()
        {
            foreach (var def in RoleDatabase.All.Values)
                Assert.True(RoleFit.Allows(def, RoleFit.DefaultDuty(def)),
                    $"{def.displayName} 의 기본 임무가 허용 목록에 없다");
        }

        // ── 적합도 ────────────────────────────────────────────────────────

        /// <summary>모든 능력치가 같은 값인 선수. 치우침만 따로 보기 위한 기준점.</summary>
        static PlayerData Flat(int value)
        {
            var p = new PlayerData
            {
                positionFlags = Position.ST,
                technical = new TechnicalAttributes(),
                physical = new PhysicalAttributes(),
                mental = new MentalAttributes(),
                goalkeeper = new GoalkeeperAttributes(),
            };

            foreach (var f in typeof(TechnicalAttributes).GetFields()) f.SetValue(p.technical, value);
            foreach (var f in typeof(PhysicalAttributes).GetFields()) f.SetValue(p.physical, value);
            foreach (var f in typeof(MentalAttributes).GetFields()) f.SetValue(p.mental, value);
            return p;
        }

        [Fact]
        public void 적합도는_총합이_아니라_치우침을_본다()
        {
            // 능력치 총합이 같아도 결정력이 높은 선수가 포처에 맞아야 한다.
            // 총합을 보면 "제일 잘하는 선수를 아무 역할에나" 두는 것이 정답이 된다.
            var poacher = RoleDatabase.Get(PlayerRole.Poacher);

            var flat = Flat(12);
            var sharp = Flat(12);
            sharp.technical.finishing = 18;
            sharp.mental.offTheBall = 18;

            var dull = Flat(12);
            dull.technical.finishing = 6;
            dull.mental.offTheBall = 6;

            Assert.True(RoleFit.Score(sharp, poacher) > RoleFit.Score(flat, poacher));
            Assert.True(RoleFit.Score(dull, poacher) < RoleFit.Score(flat, poacher));

            // 전체를 똑같이 올린 선수는 적합도가 변하지 않아야 한다 — 치우침이 그대로이므로.
            Assert.Equal(RoleFit.Score(Flat(8), poacher), RoleFit.Score(Flat(18), poacher), 3);
        }

        [Fact]
        public void 적합도_배수는_정해진_폭_안에_있다()
        {
            var w = Make();
            foreach (var p in w.Players.Values.Where(x => x.clubId > 0).Take(500))
            foreach (var def in RoleDatabase.All.Values)
                Assert.InRange(RoleFit.Multiplier(p, def),
                    1f - BalanceConfig.RoleFitSwing - 0.001f,
                    1f + BalanceConfig.RoleFitSwing + 0.001f);
        }

        [Fact]
        public void 자리마다_고를_역할이_여럿_있다()
        {
            foreach (var pos in new[]
                     {
                         Position.GK, Position.CB, Position.LB, Position.CDM,
                         Position.CM, Position.CAM, Position.LW, Position.ST,
                     })
                Assert.True(RoleFit.For(pos).Count >= 2, $"{pos} 에서 고를 역할이 모자라다");
        }

        [Fact]
        public void 필드_선수에게_골키퍼_역할이_붙지_않는다()
        {
            // PlayerRole 에는 None 이 없고 기본값이 Goalkeeper(0) 다.
            // 역할을 명시하지 않으면 전원이 골키퍼가 된다.
            var w = Make();

            foreach (var club in w.Clubs.Values)
            {
                var tac = club.savedTactics.First(t => t.id == club.activeTacticsId);
                foreach (var slot in tac.slots)
                {
                    Assert.NotNull(slot.role);
                    var def = RoleDatabase.Get(slot.role!.role);
                    Assert.True((def.applicablePositions & slot.position) != 0,
                        $"{slot.position} 자리에 {def.displayName} 역할이 붙어 있다");
                }
            }
        }

        // ── 팀 균형 ───────────────────────────────────────────────────────

        [Fact]
        public void 만드는_역할만_있으면_공격이_겉돈다()
        {
            var creators = RoleDatabase.All.Values
                .Where(d => RoleFit.TypeOf(d) == RoleFit.Archetype.Creator)
                .Take(6)
                .Select(d => new RoleAssignment { role = d.role })
                .ToList();

            Assert.True(creators.Count >= 2, "만드는 역할이 모자라 검사할 수 없다");
            Assert.True(RoleFit.BalanceMultiplier(creators) < 1f);
        }

        [Fact]
        public void 정상_포메이션은_균형_감점을_받지_않는다()
        {
            // 반반을 요구하면 4-2-3-1 처럼 최전방이 한 명인 포메이션까지 깎여
            // 리그 전체 득점이 내려간다. 비율이 아니라 부재를 봐야 한다.
            var w = Make();
            foreach (var club in w.Clubs.Values)
            {
                var tac = club.savedTactics.First(t => t.id == club.activeTacticsId);
                Assert.Equal(1f, RoleFit.BalanceMultiplier(tac.slots.Select(s => s.role)), 3);
            }
        }

        // ── AI ────────────────────────────────────────────────────────────

        [Fact]
        public void AI_역할_배정이_전술_색깔_배분을_밀지_않는다()
        {
            // 같은 난수를 쓰면 추첨 횟수가 밀려 다음 구단들의 전술 색깔이 통째로 바뀐다.
            // 전에 이것 때문에 리그 득점이 2.63 에서 2.41 로 움직였다.
            var a = Make(31415);
            var b = Make(31415);

            foreach (var club in a.Clubs.Values)
            {
                var ta = club.savedTactics.First(t => t.id == club.activeTacticsId).instructions;
                var tb = b.Club(club.id)!.savedTactics
                    .First(t => t.id == b.Club(club.id)!.activeTacticsId).instructions;

                Assert.Equal(ta.buildUpStyle, tb.buildUpStyle);
                Assert.Equal(ta.defensiveLine, tb.defensiveLine);
            }
        }

        [Fact]
        public void 좋은_구단일수록_역할을_잘_맞춘다()
        {
            var w = Make();

            float FitOf(ClubData c)
            {
                var tac = c.savedTactics.First(t => t.id == c.activeTacticsId);
                var scores = tac.slots
                    .Select(s => (p: w.Player(s.assignedPlayerId), r: s.role))
                    .Where(x => x.p != null && x.r != null)
                    .Select(x => RoleFit.Score(x.p!, RoleDatabase.Get(x.r!.role)))
                    .ToList();
                return scores.Count == 0 ? 0f : scores.Average();
            }

            float top = w.Clubs.Values.Where(c => c.reputation >= 4).Average(FitOf);
            float bottom = w.Clubs.Values.Where(c => c.reputation <= 2).Average(FitOf);

            Assert.True(top > bottom,
                $"5성 구단 적합도 {top:F3} ≤ 약팀 {bottom:F3} — 구단 수준이 역할 선택에 반영되지 않는다");
        }

        // ── 화면이 쓰는 동작 ───────────────────────────────────────────────

        [Fact]
        public void 역할을_바꿔도_임무가_허용_범위를_벗어나지_않는다()
        {
            // 화면에서 공격 임무로 둔 뒤 수비 전용 역할로 바꾸면 허용되지 않는 조합이 남는다.
            var w = Make();
            var club = w.Clubs.Values.First();
            var tac = club.savedTactics.First(t => t.id == club.activeTacticsId);
            var slot = tac.slots.First(s => (s.position & Position.CB) != 0);

            slot.role = new RoleAssignment { role = PlayerRole.BallPlayingDefender, duty = RoleDuty.Support };

            foreach (var role in RoleFit.For(slot.position))
            {
                var def = RoleDatabase.Get(role);
                var duty = RoleFit.Allows(def, slot.role.duty)
                    ? slot.role.duty
                    : RoleFit.DefaultDuty(def);

                slot.role = new RoleAssignment { role = role, duty = duty };
                Assert.True(RoleFit.Allows(def, slot.role.duty),
                    $"{def.displayName} 에 허용되지 않는 임무가 남았다");
            }
        }

        [Fact]
        public void 자동_역할_배정은_균형을_깨지_않는다()
        {
            // 화면의 "선수에 맞춰 역할 고르기" 와 같은 절차.
            var w = Make();
            var club = w.Clubs.Values.First(c => c.reputation <= 2);
            var tac = club.savedTactics.First(t => t.id == club.activeTacticsId);

            float before = RoleFit.BalanceMultiplier(tac.slots.Select(s => s.role));

            foreach (var slot in tac.slots)
            {
                var p = w.Player(slot.assignedPlayerId);
                if (p == null || RoleFit.Best(p, slot.position) is not { } best) continue;

                var prev = slot.role;
                float bal = RoleFit.BalanceMultiplier(tac.slots.Select(s => s.role));

                slot.role = new RoleAssignment
                {
                    role = best,
                    duty = RoleFit.DefaultDuty(RoleDatabase.Get(best)),
                };

                if (RoleFit.BalanceMultiplier(tac.slots.Select(s => s.role)) < bal) slot.role = prev;
            }

            Assert.True(RoleFit.BalanceMultiplier(tac.slots.Select(s => s.role)) >= before);
        }

        [Fact]
        public void 역할_사전은_여러_스레드에서_동시에_읽어도_온전하다()
        {
            // 전에는 빈 사전을 먼저 공개하고 나중에 채워, 병렬로 도는 테스트가
            // 절반만 찬 사전을 읽고 이따금 깨졌다. 게임은 단일 스레드라 안 드러난다.
            int expected = RoleDatabase.All.Count;
            Assert.True(expected > 30);

            var counts = new System.Collections.Concurrent.ConcurrentBag<int>();
            System.Threading.Tasks.Parallel.For(0, 64, _ =>
            {
                counts.Add(RoleDatabase.All.Count);
                // 모든 역할을 실제로 꺼내 본다 — 키가 빠져 있으면 여기서 터진다.
                foreach (var role in Enum.GetValues<PlayerRole>())
                    Assert.NotNull(RoleDatabase.Get(role));
            });

            Assert.All(counts, c => Assert.Equal(expected, c));
        }

        [Fact]
        public void 모든_역할에_이름과_설명이_있다()
        {
            // 화면에 그대로 나가는 값이다.
            foreach (var def in RoleDatabase.All.Values)
            {
                Assert.False(string.IsNullOrWhiteSpace(def.displayName));
                Assert.False(string.IsNullOrWhiteSpace(def.shortDescription));
                Assert.NotEqual(Position.None, def.applicablePositions);
            }
        }

        // ── 세이브 ────────────────────────────────────────────────────────

        [Fact]
        public void 역할과_임무가_세이브를_왕복한다()
        {
            var w = Make();
            var club = w.Clubs.Values.First();
            ManagerCareer.TakeCharge(w, club.id, "테스트");

            var tac = w.MyClub!.savedTactics.First(t => t.id == w.MyClub.activeTacticsId);
            var slot = tac.slots.First(s => (s.position & Position.ST) != 0);
            slot.role = new RoleAssignment { role = PlayerRole.Poacher, duty = RoleDuty.Attack };

            var back = SaveGame.Deserialize(SaveGame.Serialize(w, "역할")).world!;
            var loaded = back.MyClub!.savedTactics
                .First(t => t.id == back.MyClub.activeTacticsId)
                .slots.First(s => (s.position & Position.ST) != 0);

            Assert.NotNull(loaded.role);
            Assert.Equal(PlayerRole.Poacher, loaded.role!.role);
            Assert.Equal(RoleDuty.Attack, loaded.role.duty);
        }

        [Fact]
        public void 골키퍼_역할도_세이브를_왕복한다()
        {
            // PlayerRole.Goalkeeper 는 0 이라 기본값으로 취급돼 저장에서 빠질 수 있다.
            var w = Make();
            var club = w.Clubs.Values.First();
            ManagerCareer.TakeCharge(w, club.id, "테스트");

            var tac = w.MyClub!.savedTactics.First(t => t.id == w.MyClub.activeTacticsId);
            var gk = tac.slots.First(s => (s.position & Position.GK) != 0);
            gk.role = new RoleAssignment { role = PlayerRole.Goalkeeper, duty = RoleDuty.Defend };

            var back = SaveGame.Deserialize(SaveGame.Serialize(w, "골키퍼")).world!;
            var loaded = back.MyClub!.savedTactics
                .First(t => t.id == back.MyClub.activeTacticsId)
                .slots.First(s => (s.position & Position.GK) != 0);

            Assert.NotNull(loaded.role);
            Assert.Equal(PlayerRole.Goalkeeper, loaded.role!.role);
            Assert.Equal(RoleDuty.Defend, loaded.role.duty);
        }
    }
}
