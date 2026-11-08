using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text;
using FootballTactician.World;
using Xunit;

namespace FT.Core.Tests
{
    /// <summary>
    /// 포맷이 달라진 세이브는 <b>안내 문구로</b> 거절해야 한다.
    ///
    /// 버전 검사가 역직렬화 뒤에 있던 동안에는, 자리가 밀린 값을 만난 변환기가 먼저 터져서
    /// "DeserializeUnableToConvertValue … FormationSlot" 같은 소리가 났다. 세이브를 못 여는
    /// 것은 같지만, 왜 못 여는지 알 수 없는 것은 다른 문제다.
    /// </summary>
    public class SaveVersionTests
    {
        static byte[] Gzip(string json)
        {
            byte[] raw = Encoding.UTF8.GetBytes(json);
            using var ms = new MemoryStream();
            using (var gz = new GZipStream(ms, CompressionLevel.Optimal, leaveOpen: true))
                gz.Write(raw, 0, raw.Length);
            return ms.ToArray();
        }

        /// <summary>
        /// 포맷 4 시절의 전술 슬롯은 이름 있는 객체였다(지금은 배열이다).
        /// 변환기에 닿으면 반드시 터지는 모양이라, 검사 순서를 재는 자료로 알맞다.
        /// </summary>
        const string OldShapedSave =
            "{\"version\":4,\"savedAt\":\"\",\"label\":\"예전 판\",\"world\":{\"Clubs\":{\"1\":" +
            "{\"id\":1,\"savedTactics\":[{\"slots\":[{\"slotIndex\":1,\"position\":8}]}]}}}}";

        [Fact]
        public void 예전_포맷은_포맷_안내로_거절한다()
        {
            var ex = Assert.Throws<InvalidDataException>(
                () => SaveGame.Deserialize(Gzip(OldShapedSave)));

            Assert.Contains("세이브 포맷이 맞지 않습니다", ex.Message);
            Assert.Contains("파일 4", ex.Message);
            Assert.Contains($"현재 {SaveGame.FormatVersion}", ex.Message);

            // 변환기까지 갔다면 이런 말이 섞여 나온다.
            Assert.DoesNotContain("FormationSlot", ex.Message);
        }

        [Fact]
        public void 버전_칸이_없는_세이브도_안내로_거절한다()
        {
            var ex = Assert.Throws<InvalidDataException>(
                () => SaveGame.Deserialize(Gzip("{\"world\":{\"Clubs\":{}}}")));

            Assert.Contains("세이브 포맷이 맞지 않습니다", ex.Message);
            Assert.Contains("파일 0", ex.Message);
        }

        [Fact]
        public void 지금_포맷은_그대로_읽힌다()
        {
            var w = WorldGenerator.Create(13579);
            var club = w.Clubs.Values.First(c => c.reputation == 3);
            ManagerCareer.TakeCharge(w, club.id, "테스트 감독");
            GameEngine.PlayNextRound(w);

            var env = SaveGame.Deserialize(SaveGame.Serialize(w, "지금 판"));

            Assert.Equal(SaveGame.FormatVersion, env.version);
            Assert.Equal("지금 판", env.label);
            Assert.Equal(w.Clubs.Count, env.world.Clubs.Count);
        }
    }
}
