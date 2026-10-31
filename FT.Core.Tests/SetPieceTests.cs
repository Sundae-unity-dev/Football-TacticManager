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
    /// 세트피스. 찬스를 더 주는 것이 아니라 일부를 대체해야 하고,
    /// 배달 방식은 상대와 내 스쿼드에 따라 갈려야 한다.
    /// </summary>
    public class SetPieceTests
    {
        static PlayerData Player(int value, Position pos = Position.ST)
        {
            var p = new PlayerData
            {
                positionFlags = pos,
                technical = new TechnicalAttributes(),
                physical = new PhysicalAttributes(),
                mental = new MentalAttributes(),
                goalkeeper = new GoalkeeperAttributes(),
            };
            foreach (var f in typeof(TechnicalAttributes).GetFields()) f.SetValue(p.technical, value);
            foreach (var f in typeof(PhysicalAttributes).GetFields()) f.SetValue(p.physical, value);
            foreach (var f in typeof(MentalAttributes).GetFields()) f.SetValue(p.mental, value);
            foreach (var f in typeof(GoalkeeperAttributes).GetFields()) f.SetValue(p.goalkeeper, value);
            return p;
        }

        static PlayerData[] Eleven(int value)
            => Enumerable.Range(0, 11).Select(_ => Player(value)).ToArray();

        // ── 대체이지 추가가 아니다 ─────────────────────────────────────────

        [Fact]
        public void 세트피스_비율은_페널티와_프리킥을_포함한다()
        {
            // 따로 더하면 세트피스가 공격 시도의 절반을 가져간다.
            Assert.True(BalanceConfig.PenaltyShare + BalanceConfig.FreeKickShare
                        < BalanceConfig.SetPieceShare);
        }

        [Fact]
        public void 공격_시도가_세트피스로_바뀌는_비율이_설정과_맞는다()
        {
            var rng = new Random(4242);
            int set = 0, pen = 0, fk = 0;
            const int n = 200_000;

            for (int i = 0; i < n; i++)
                switch (SetPieces.Roll(rng))
                {
                    case SetPieces.Kind.Penalty: pen++; set++; break;
                    case SetPieces.Kind.FreeKick: fk++; set++; break;
                    case SetPieces.Kind.Corner: set++; break;
                }

            Assert.Equal(BalanceConfig.SetPieceShare, set / (float)n, 2);
            // 20만 번이라도 0.001 자리는 표본 오차에 걸린다. 비율이 맞는지만 본다.
            Assert.InRange(pen / (float)n, BalanceConfig.PenaltyShare * 0.9f, BalanceConfig.PenaltyShare * 1.1f);
            Assert.InRange(fk / (float)n, BalanceConfig.FreeKickShare * 0.95f, BalanceConfig.FreeKickShare * 1.05f);
        }

        [Fact]
        public void 세트피스_득점_비율이_현실적인_범위에_있다()
        {
            // 열린 플레이만 있으면 코너·프리킥·페널티가 장식이고,
            // 너무 많으면 경기가 세트피스 놀음이 된다.
            var w = WorldGenerator.Create(31415);
            var clubs = w.Clubs.Values.Where(c => c.playerIds.Count >= 16).Take(2).ToList();

            MatchLineup Make(ClubData c)
            {
                var tac = FormationDatabase.CreateTactics("4-2-3-1", "검사");
                var squad = w.Squad(c.id).ToList();
                LineupBuilder.Assign(tac, squad);
                RoleDefaults.Fill(tac);
                return new MatchLineup { clubId = c.id, tactics = tac, benchIds = LineupBuilder.Bench(tac, squad) };
            }

            var la = Make(clubs[0]);
            var lb = Make(clubs[1]);
            int open = 0, setGoals = 0;

            for (int i = 0; i < 1500; i++)
            {
                var r = MatchEngine.Simulate(new MatchSimContext
                {
                    home = la, away = lb, seed = i * 7919 + 13,
                }, w.Lookup);

                foreach (var e in r.events)
                    switch (e.eventType)
                    {
                        case MatchEventType.Goal: open++; break;
                        case MatchEventType.CornerKickGoal:
                        case MatchEventType.FreeKickGoal:
                        case MatchEventType.PenaltyScored: setGoals++; break;
                    }
            }

            float share = setGoals / (float)(open + setGoals);
            Assert.InRange(share, BalanceConfig.SetPieceGoalShareMin, BalanceConfig.SetPieceGoalShareMax);
        }

        // ── 제공권이 축이 된다 ────────────────────────────────────────────

        [Fact]
        public void 제공권이_좋으면_코너_기대득점이_오른다()
        {
            float strong = SetPieces.Xg(SetPieces.Kind.Corner, 1f, 17f, 13f, SetPieceDelivery.Lofted, 15f);
            float even = SetPieces.Xg(SetPieces.Kind.Corner, 1f, 13f, 13f, SetPieceDelivery.Lofted, 15f);
            float weak = SetPieces.Xg(SetPieces.Kind.Corner, 1f, 11f, 15f, SetPieceDelivery.Lofted, 15f);

            Assert.True(strong > even);
            Assert.True(even > weak);
        }

        [Fact]
        public void 제공권은_키와_힘에서_나온다()
        {
            var tall = Eleven(10);
            foreach (var p in tall) { p.technical.heading = 18; p.physical.jumping = 18; p.physical.strength = 18; }

            Assert.True(SetPieces.AerialThreat(tall) > SetPieces.AerialThreat(Eleven(10)));
        }

        [Fact]
        public void 골키퍼가_박스를_장악하면_코너가_덜_들어간다()
        {
            float vsStrong = SetPieces.Xg(SetPieces.Kind.Corner, 1f, 14f, 13f, SetPieceDelivery.Lofted, 19f);
            float vsWeak = SetPieces.Xg(SetPieces.Kind.Corner, 1f, 14f, 13f, SetPieceDelivery.Lofted, 10f);

            Assert.True(vsWeak > vsStrong);
        }

        // ── 배달 방식이 거래다 ────────────────────────────────────────────

        [Fact]
        public void 배달_방식은_상황에_따라_최선이_바뀐다()
        {
            // 한 가지가 늘 최선이면 고를 이유가 없다.
            var cases = new (float aerial, float defence, float gk, float move, float taker)[]
            {
                (17f, 12f, 15f, 1.0f, 1.0f),     // 장신
                (12f, 17f, 15f, 1.0f, 1.0f),     // 단신
                (12f, 17f, 15f, 1.3f, 1.0f),     // 단신이지만 잘 움직임
                (13f, 13f, 15f, 1.0f, 1.3f),     // 최고의 키커
            };

            var winners = new HashSet<SetPieceDelivery>();
            foreach (var (aerial, defence, gk, move, taker) in cases)
            {
                SetPieceDelivery best = SetPieceDelivery.Inswinger;
                float bestXg = -1f;

                foreach (var d in Enum.GetValues<SetPieceDelivery>())
                {
                    float xg = SetPieces.Xg(SetPieces.Kind.Corner, taker, aerial, defence, d, gk, move);
                    if (xg > bestXg) { bestXg = xg; best = d; }
                }
                winners.Add(best);
            }

            Assert.True(winners.Count >= 3,
                $"최선이 되는 배달이 {winners.Count}가지뿐이다 — 나머지는 고를 이유가 없다");
        }

        [Fact]
        public void 높이_띄우면_제공권에_더_크게_건다()
        {
            // 우위면 더 먹고 열세면 더 잃어야 거래다.
            float loftStrong = SetPieces.Xg(SetPieces.Kind.Corner, 1f, 17f, 12f, SetPieceDelivery.Lofted, 15f);
            float driveStrong = SetPieces.Xg(SetPieces.Kind.Corner, 1f, 17f, 12f, SetPieceDelivery.DrivenLow, 15f);
            float loftWeak = SetPieces.Xg(SetPieces.Kind.Corner, 1f, 12f, 17f, SetPieceDelivery.Lofted, 15f);
            float driveWeak = SetPieces.Xg(SetPieces.Kind.Corner, 1f, 12f, 17f, SetPieceDelivery.DrivenLow, 15f);

            Assert.True(loftStrong > driveStrong);
            Assert.True(loftWeak < driveWeak);
        }

        [Fact]
        public void 짧게_처리하면_기대득점이_가장_낮다()
        {
            // 그 대신 공을 지킨다. 숫자만 보면 늘 최악이어야 맞다.
            foreach (var d in Enum.GetValues<SetPieceDelivery>())
            {
                if (d == SetPieceDelivery.ShortPass) continue;
                Assert.True(
                    SetPieces.Xg(SetPieces.Kind.Corner, 1f, 13f, 13f, SetPieceDelivery.ShortPass, 15f)
                    < SetPieces.Xg(SetPieces.Kind.Corner, 1f, 13f, 13f, d, 15f));
            }
        }

        // ── 키커 ──────────────────────────────────────────────────────────

        [Fact]
        public void 키커가_좋으면_배달이_좋아진다()
        {
            var weak = Player(7);
            var mid = Player(13);
            var strong = Player(19);

            Assert.True(SetPieces.TakerQuality(weak, SetPieces.Kind.Corner) < 1f);
            Assert.Equal(1f, SetPieces.TakerQuality(mid, SetPieces.Kind.Corner), 2);
            Assert.True(SetPieces.TakerQuality(strong, SetPieces.Kind.Corner) > 1f);
        }

        [Fact]
        public void 지명한_키커가_없으면_가장_잘_차는_선수가_찬다()
        {
            // 설정을 건드리지 않아도 손해가 아니어야 한다.
            var squad = Eleven(10);
            squad[5].technical.setPieces = 19;
            squad[5].technical.crossing = 19;

            Assert.Equal(squad[5], SetPieces.Taker(squad, null, SetPieces.Kind.Corner));
        }

        [Fact]
        public void 지명한_키커가_빠지면_자동으로_넘어간다()
        {
            // 교체돼 나간 선수를 계속 키커로 두면 세트피스가 통째로 사라진다.
            var squad = Eleven(10);
            var tactics = FormationDatabase.CreateTactics("4-2-3-1", "검사");
            tactics.setPieces.cornerKickLeft.Add(new SetPieceRoutine
            {
                type = SetPieceType.CornerKick,
                takerPlayerId = 99999,          // 명단에 없는 선수
            });

            var taker = SetPieces.Taker(squad, tactics, SetPieces.Kind.Corner);
            Assert.NotNull(taker);
            Assert.Contains(taker, squad);
        }

        [Fact]
        public void 골키퍼는_키커로_뽑히지_않는다()
        {
            var squad = Eleven(10);
            squad[0].positionFlags = Position.GK;
            squad[0].technical.setPieces = 20;
            squad[0].technical.crossing = 20;

            Assert.NotEqual(squad[0], SetPieces.Taker(squad, null, SetPieces.Kind.Corner));
        }

        [Fact]
        public void 페널티_키커는_결정력과_침착함으로_고른다()
        {
            var squad = Eleven(10);
            squad[3].technical.setPieces = 20;      // 프리킥 전문
            squad[7].technical.finishing = 19;      // 마무리 전문
            squad[7].mental.composure = 19;

            Assert.Equal(squad[3], SetPieces.Taker(squad, null, SetPieces.Kind.Corner));
            Assert.Equal(squad[7], SetPieces.Taker(squad, null, SetPieces.Kind.Penalty));
        }

        // ── 기록 ──────────────────────────────────────────────────────────

        [Fact]
        public void 선수_득점_합계가_팀_점수와_맞는다()
        {
            // 세트피스 득점을 선수 기록에서 빼면 팀 점수와 득점왕이 어긋난다.
            // 시즌 기록에는 컵 경기도 쌓이므로 리그 일정과는 비교할 수 없다.
            // 한 경기 단위로 본다.
            var w = WorldGenerator.Create(31415);
            var clubs = w.Clubs.Values.Where(c => c.playerIds.Count >= 16).Take(2).ToList();

            MatchLineup Make(ClubData c)
            {
                var tac = FormationDatabase.CreateTactics("4-2-3-1", "검사");
                var squad = w.Squad(c.id).ToList();
                LineupBuilder.Assign(tac, squad);
                RoleDefaults.Fill(tac);
                return new MatchLineup { clubId = c.id, tactics = tac, benchIds = LineupBuilder.Bench(tac, squad) };
            }

            var la = Make(clubs[0]);
            var lb = Make(clubs[1]);
            int matchGoals = 0, scored = 0;

            for (int i = 0; i < 300; i++)
            {
                foreach (var p in w.Players.Values) p.seasonStats = new PlayerStats();

                var ctx = new MatchSimContext { home = la, away = lb, seed = i * 7919 + 13 };
                var r = MatchEngine.Simulate(ctx, w.Lookup);
                MatchEngine.ApplyResult(r, ctx, w.Lookup);

                matchGoals += r.homeGoals + r.awayGoals;
                scored += w.Squad(clubs[0].id).Concat(w.Squad(clubs[1].id))
                           .Sum(p => p.seasonStats?.goals ?? 0);
            }

            Assert.True(matchGoals > 0);
            Assert.Equal(matchGoals, scored);
        }

        // ── AI ────────────────────────────────────────────────────────────

        [Fact]
        public void AI_구단은_스쿼드에_맞는_배달을_고른다()
        {
            // 전부 기본값이면 리그의 모든 팀이 같은 선택을 해 배달이 의미를 잃는다.
            var w = WorldGenerator.Create(31415);

            var deliveries = w.Clubs.Values
                .Select(c => c.savedTactics.First(t => t.id == c.activeTacticsId))
                .Where(t => t.setPieces?.cornerKickLeft is { Count: > 0 })
                .Select(t => t.setPieces.cornerKickLeft[0].delivery)
                .ToList();

            Assert.True(deliveries.Count > 100);
            Assert.True(deliveries.Distinct().Count() >= 2,
                "모든 구단이 같은 배달을 쓴다");
        }

        [Fact]
        public void AI_가_정한_키커는_명단_안의_선수다()
        {
            var w = WorldGenerator.Create(31415);

            foreach (var club in w.Clubs.Values)
            {
                var tac = club.savedTactics.First(t => t.id == club.activeTacticsId);
                if (tac.setPieces?.cornerKickLeft is not { Count: > 0 } list) continue;

                int id = list[0].takerPlayerId;
                if (id <= 0) continue;
                Assert.Contains(id, tac.slots.Select(s => s.assignedPlayerId));
            }
        }

        // ── 세이브 ────────────────────────────────────────────────────────

        [Fact]
        public void 세트피스_설정이_세이브를_왕복한다()
        {
            // SetPieceDelivery.Inswinger 는 0 이라 기본값으로 취급돼 빠질 수 있다.
            var w = WorldGenerator.Create(31415);
            var club = w.Clubs.Values.First();
            ManagerCareer.TakeCharge(w, club.id, "테스트");

            var tac = w.MyClub!.savedTactics.First(t => t.id == w.MyClub.activeTacticsId);
            int taker = tac.slots[5].assignedPlayerId;
            tac.setPieces.cornerKickLeft = new List<SetPieceRoutine>
            {
                new() { type = SetPieceType.CornerKick, delivery = SetPieceDelivery.Inswinger, takerPlayerId = taker },
            };

            var back = SaveGame.Deserialize(SaveGame.Serialize(w, "세트피스")).world!;
            var loaded = back.MyClub!.savedTactics
                .First(t => t.id == back.MyClub.activeTacticsId).setPieces.cornerKickLeft;

            Assert.Single(loaded);
            Assert.Equal(SetPieceDelivery.Inswinger, loaded[0].delivery);
            Assert.Equal(SetPieceType.CornerKick, loaded[0].type);
            Assert.Equal(taker, loaded[0].takerPlayerId);
        }
    }
}
