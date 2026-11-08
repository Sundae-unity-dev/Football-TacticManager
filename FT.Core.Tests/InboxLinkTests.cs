using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text;
using FootballTactician.World;
using Xunit;

namespace FT.Core.Tests
{
    /// <summary>
    /// 받은 소식이 가리키는 선수·구단 id. 화면은 이걸 보고 그 대상으로 데려간다.
    /// 세이브에 새로 붙은 칸이라, 예전 세이브가 그대로 읽히는지를 같이 본다.
    /// </summary>
    public class InboxLinkTests
    {
        [Fact]
        public void 대상_id가_세이브를_왕복한다()
        {
            var w = WorldGenerator.Create(13579);
            var p = w.Players.Values.First();
            w.Post("부상", "테스트", InboxKind.Injury, playerId: p.id);
            w.Post("경기", "테스트", InboxKind.Match, clubId: p.clubId);
            w.Post("소식", "대상 없음");

            var back = SaveGame.Deserialize(SaveGame.Serialize(w, "소식")).world;

            Assert.Equal(0, back.Inbox[0].playerId);
            Assert.Equal(0, back.Inbox[0].clubId);
            Assert.Equal(p.clubId, back.Inbox[1].clubId);
            Assert.Equal(p.id, back.Inbox[2].playerId);
        }

        [Fact]
        public void 대상_칸이_없던_예전_세이브도_읽힌다()
        {
            var w = WorldGenerator.Create(13579);
            w.Post("예전 소식", "대상 칸이 생기기 전", InboxKind.Transfer);

            // 대상 없는 소식은 0 이라 애초에 저장되지 않는다(WhenWritingDefault).
            // 즉 지금 쓴 이 세이브가 곧 '칸이 없던 시절'의 모양이다 — 정말 그런지 확인하고 읽는다.
            var packed = SaveGame.Serialize(w, "예전");
            var json = Unzip(packed);
            Assert.DoesNotContain("\"playerId\":0", json);

            var back = SaveGame.Deserialize(packed).world;
            Assert.Equal("예전 소식", back.Inbox[0].title);
            Assert.Equal(0, back.Inbox[0].playerId);
            Assert.Equal(0, back.Inbox[0].clubId);
        }

        /// <summary>
        /// A매치 소집 소식만 대상 id 를 달지 않고 있었다 — 본문이 내 선수 이름을 대 놓고
        /// 가리키는데도 눌리지 않았다. 한 명이면 그 선수로, 여럿이면 내 구단으로 이어진다.
        /// </summary>
        [Fact]
        public void 소집_소식은_언제나_어딘가로_이어진다()
        {
            bool sawSingle = false, sawSeveral = false;

            foreach (int seed in new[] { 13579, 2468, 31337, 909, 4242, 515, 24680 })
            {
                var w = WorldGenerator.Create(seed);
                var club = w.Clubs.Values.First(c => c.reputation == 3);
                ManagerCareer.TakeCharge(w, club.id, "테스트");

                NationalTeam.RunBreak(w, new System.Random(seed));

                var call = w.Inbox.FirstOrDefault(i => i.title == "A매치 소집");
                if (call == null) continue;

                // 어느 쪽이든 갈 곳이 있어야 한다 — 이게 이 소식에만 없었다.
                Assert.True(call.playerId > 0 || call.clubId > 0,
                            $"가리키는 곳이 없다: {call.body}");
                Assert.Equal(club.id, call.clubId);

                if (call.body.StartsWith("1명이"))
                {
                    sawSingle = true;
                    Assert.True(call.playerId > 0, "한 명만 소집됐는데 가리키는 선수가 없다");
                    Assert.Equal(club.id, w.Player(call.playerId)?.clubId);
                }
                else
                {
                    sawSeveral = true;
                    Assert.Equal(0, call.playerId);   // 하나로 정해지지 않으니 구단으로 간다
                }
            }

            Assert.True(sawSingle, "한 명만 소집된 경우가 없어 규칙이 일하는지 알 수 없다");
            Assert.True(sawSeveral, "여럿 소집된 경우가 없어 규칙이 일하는지 알 수 없다");
        }

        static string Unzip(byte[] packed)
        {
            using var src = new MemoryStream(packed);
            using var gz = new GZipStream(src, CompressionMode.Decompress);
            using var r = new StreamReader(gz, Encoding.UTF8);
            return r.ReadToEnd();
        }
    }
}
