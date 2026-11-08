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

        static string Unzip(byte[] packed)
        {
            using var src = new MemoryStream(packed);
            using var gz = new GZipStream(src, CompressionMode.Decompress);
            using var r = new StreamReader(gz, Encoding.UTF8);
            return r.ReadToEnd();
        }
    }
}
