using System;
using System.Linq;
using FootballTactician.Data;
using FootballTactician.World;
using Xunit;

namespace FT.Core.Tests
{
    /// <summary>
    /// 탈의실. 숫자가 화면에만 남고 경기에 닿지 않으면 만들 이유가 없다.
    /// </summary>
    public class DressingRoomTests
    {
        static GameWorld Make(int seed = 31415)
        {
            var w = WorldGenerator.Create(seed);
            var club = w.Clubs.Values.First(c => c.reputation == 4);
            ManagerCareer.TakeCharge(w, club.id, "테스트");
            return w;
        }

        [Fact]
        public void 세계를_만들면_모든_구단에_주장이_있다()
        {
            var w = Make();
            foreach (var club in w.Clubs.Values)
            {
                var room = club.dressingRoom;
                Assert.NotNull(room);
                Assert.Contains(room.captainPlayerId, club.playerIds);
                Assert.NotEqual(room.captainPlayerId, room.viceCaptainPlayerId);
            }
        }

        [Fact]
        public void 주장은_탈의실_영향력이_가장_큰_선수다()
        {
            var w = Make();
            var club = w.MyClub!;
            var captain = w.Player(club.dressingRoom.captainPlayerId)!;

            float best = w.Squad(club.id).Max(p => DressingRoom.Influence(w, p));
            Assert.Equal(best, DressingRoom.Influence(w, captain), 3);
        }

        [Fact]
        public void 화합도는_사기_기준선을_움직인다()
        {
            var w = Make();
            var club = w.MyClub!;

            club.dressingRoom.overallHarmony = 50f;
            Assert.Equal(DressingRoom.MoraleAnchor, DressingRoom.MoraleAnchorFor(club), 3);

            club.dressingRoom.overallHarmony = 100f;
            Assert.Equal(DressingRoom.MoraleAnchor + DressingRoom.AnchorSwing,
                         DressingRoom.MoraleAnchorFor(club), 3);

            club.dressingRoom.overallHarmony = 0f;
            Assert.Equal(DressingRoom.MoraleAnchor - DressingRoom.AnchorSwing,
                         DressingRoom.MoraleAnchorFor(club), 3);
        }

        [Fact]
        public void 분위기_좋은_구단이_사기도_높다()
        {
            // 기준선이 사기에 닿지 않으면 분위기는 장식이다.
            // 화합도를 직접 꽂아 재면 안 된다 — 매주 Update 가 명단에서 다시 계산해 덮어쓴다.
            var w = Make(606);
            for (int i = 0; i < 12; i++) GameEngine.PlayNextRound(w);

            var byHarmony = w.Clubs.Values
                .Where(c => w.Squad(c.id).Any())
                .OrderBy(c => c.dressingRoom.overallHarmony)
                .ToList();

            float worst = byHarmony.Take(20).Average(c => w.Squad(c.id).Average(p => p.morale));
            float best = byHarmony.TakeLast(20).Average(c => w.Squad(c.id).Average(p => p.morale));

            Assert.True(best > worst,
                $"화합도 상위 구단 사기 {best:F1} ≤ 하위 구단 {worst:F1}");
        }

        [Fact]
        public void 불만은_인원으로_나눠_잰다()
        {
            // 합계로 재면 스쿼드가 클수록 자동으로 불행해진다.
            var w = Make();
            foreach (var club in w.Clubs.Values)
                Assert.InRange(club.dressingRoom.discontent, 0f, 100f);

            // 큰 스쿼드가 체계적으로 더 불행하면 안 된다.
            var big = w.Clubs.Values.OrderByDescending(c => c.playerIds.Count).Take(20);
            var small = w.Clubs.Values.OrderBy(c => c.playerIds.Count).Take(20);

            Assert.True(Math.Abs(big.Average(c => c.dressingRoom.discontent)
                               - small.Average(c => c.dressingRoom.discontent)) < 12f);
        }

        [Fact]
        public void 방치하지_않은_세계는_분위기가_무너지지_않는다()
        {
            // 전 구단이 바닥이면 분위기는 구단을 가르지 못하고 전체 하향 보정일 뿐이다.
            var w = Make(909);
            for (int s = 0; s < 3; s++)
            {
                int guard = 0;
                while (!w.SeasonFinished && guard++ < 60)
                    if (GameEngine.PlayNextRound(w) == null) break;
                SeasonRollover.StartNextSeason(w);
            }

            var rooms = w.Clubs.Values.Select(c => c.dressingRoom).ToList();
            Assert.InRange(rooms.Average(r => r.overallHarmony), 40f, 75f);
            Assert.True(rooms.Max(r => r.overallHarmony) - rooms.Min(r => r.overallHarmony) > 10f,
                "구단마다 분위기가 갈리지 않는다");
        }

        [Fact]
        public void 불만은_모두_감독이_손댈_수_있는_것이다()
        {
            // 어쩔 수 없는 불만은 분위기를 설명하지 못하고 짜증만 남긴다.
            var w = Make();
            var club = w.MyClub!;

            foreach (var g in DressingRoom.Grievances(w, club))
            {
                Assert.Contains(g.PlayerId, club.playerIds);
                Assert.False(string.IsNullOrWhiteSpace(g.Reason));
                Assert.True(g.weight > 0);
            }
        }

        [Fact]
        public void 주전에서_빼면_불만이_생긴다()
        {
            var w = Make(1212);
            var club = w.MyClub!;
            var tac = club.savedTactics.First(t => t.id == club.activeTacticsId);

            // 가장 잘하는 선수를 선발에서 내린다.
            var star = w.Squad(club.id).OrderByDescending(p => GameWorld.Ca(p)).First();
            var slot = tac.slots.First(s => s.assignedPlayerId == star.id);
            var bench = w.Squad(club.id).First(p => tac.slots.All(s => s.assignedPlayerId != p.id));
            slot.assignedPlayerId = bench.id;

            Assert.Contains(DressingRoom.Grievances(w, club),
                g => g.PlayerId == star.id && g.Reason.Contains("주전"));
        }

        [Fact]
        public void 관계는_상대방의_역할을_가리킨다()
        {
            var w = Make();
            var club = w.MyClub!;
            var squad = w.Squad(club.id).ToList();

            var young = squad.OrderBy(p => w.Age(p)).First();
            foreach (var rel in DressingRoom.RelationsOf(w, young))
            {
                var other = w.Player(rel.targetPlayerId)!;
                Assert.NotEqual(young.id, other.id);
                Assert.Contains(other.id, club.playerIds);

                // 멘토로 적힌 상대는 나보다 나이가 많아야 한다.
                if (rel.type == RelationshipType.Mentor)
                    Assert.True(w.Age(other) - w.Age(young) >= DressingRoom.MentorAgeGap);

                if (rel.type == RelationshipType.Mentee)
                    Assert.True(w.Age(young) - w.Age(other) >= DressingRoom.MentorAgeGap);
            }
        }

        [Fact]
        public void 주장은_관계에_한_명만_나온다()
        {
            var w = Make();
            var club = w.MyClub!;
            var other = w.Squad(club.id).First(p => p.id != club.dressingRoom.captainPlayerId);

            Assert.Single(DressingRoom.RelationsOf(w, other),
                r => r.type == RelationshipType.Captain);
        }

        [Fact]
        public void 주장을_바꾸면_리더십과_화합도가_따라_움직인다()
        {
            var w = Make(343);
            var club = w.MyClub!;

            var weakest = w.Squad(club.id).OrderBy(p => DressingRoom.Influence(w, p)).First();
            float before = club.dressingRoom.overallHarmony;

            DressingRoom.SetCaptain(w, club, weakest.id);

            Assert.Equal(weakest.id, club.dressingRoom.captainPlayerId);
            Assert.True(club.dressingRoom.overallHarmony < before,
                "영향력 없는 선수를 주장으로 세웠는데 분위기가 그대로다");
        }

        [Fact]
        public void 남의_구단_선수는_주장으로_세울_수_없다()
        {
            var w = Make();
            var club = w.MyClub!;
            int captain = club.dressingRoom.captainPlayerId;
            var stranger = w.Players.Values.First(p => p.clubId != club.id && p.clubId > 0);

            DressingRoom.SetCaptain(w, club, stranger.id);

            Assert.Equal(captain, club.dressingRoom.captainPlayerId);
        }

        [Fact]
        public void 관계는_세이브에_넣지_않는다()
        {
            // 선수 4,000명의 관계를 저장하면 용량이 몇 배가 된다.
            // 나이·국적·포지션에서 똑같이 다시 나오므로 저장할 이유가 없다.
            var w = Make();
            Assert.All(w.Clubs.Values, c => Assert.Empty(c.dressingRoom.relationships));

            var back = SaveGame.Deserialize(SaveGame.Serialize(w, "탈의실")).world!;
            var club = back.MyClub!;

            Assert.Equal(w.MyClub!.dressingRoom.captainPlayerId, club.dressingRoom.captainPlayerId);
            Assert.NotEmpty(DressingRoom.RelationsOf(back, back.Squad(club.id).First()));
        }
    }
}
