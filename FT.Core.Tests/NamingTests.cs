using System.Linq;
using FootballTactician.Data;
using FootballTactician.World;
using Xunit;

namespace FT.Core.Tests
{
    /// <summary>
    /// 이름은 겹치면 안 되고, 나라의 말을 따라야 한다.
    ///
    /// 구단 이름이 겹치면 순위표에서 어느 쪽인지 알 수 없고, 약칭이 겹치면 일정이
    /// 뒤섞여 보인다. 이름을 붙이는 방식이 하나뿐이면 "민준 김" 같은 말이 나온다.
    /// </summary>
    public class NamingTests
    {
        /// <summary>겹침은 좁은 세계에서는 잘 드러나지 않는다 — 49개국을 전부 넣고 본다.</summary>
        static GameWorld Everything() => WorldGenerator.Create(31415, 2026, NameForge.All);

        [Fact]
        public void 구단_이름은_세계에서_하나뿐이다()
        {
            // 도시는 모자라면 돌려 쓴다. 그때 접사까지 같으면 이름이 통째로 겹쳤다 —
            // 네덜란드에 "FC Eindhoven" 이 1·2·3부에 하나씩 있었다.
            var w = Everything();

            var dup = w.Clubs.Values.GroupBy(c => c.name).Where(g => g.Count() > 1).ToList();

            Assert.True(dup.Count == 0,
                $"겹치는 구단 이름 {dup.Count}개 — 예: {dup.FirstOrDefault()?.Key}");
        }

        [Fact]
        public void 약칭은_같은_리그_안에서_하나뿐이다()
        {
            // 도시 앞 세 글자만 쓰면 Valencia 와 Valladolid 가 둘 다 VAL 이 된다.
            var w = Everything();

            foreach (var league in w.Leagues.Values)
            {
                var dup = league.clubIds.Select(id => w.Club(id)!.shortName)
                    .GroupBy(x => x).Where(g => g.Count() > 1).ToList();

                Assert.True(dup.Count == 0,
                    $"[{league.shortName}] 겹치는 약칭 — 예: {dup.FirstOrDefault()?.Key}");
            }
        }

        [Fact]
        public void 한_구단_안에_같은_이름의_선수가_없다()
        {
            var w = Everything();

            foreach (var club in w.Clubs.Values)
            {
                var dup = w.Squad(club.id).GroupBy(p => p.FullName)
                    .Where(g => g.Count() > 1).ToList();

                Assert.True(dup.Count == 0,
                    $"[{club.name}] 같은 이름 둘 — {dup.FirstOrDefault()?.Key}");
            }
        }

        [Fact]
        public void 이름은_한글로_나온다()
        {
            var w = WorldGenerator.Create(2024);
            var p = w.Players.Values.First();

            Assert.True(p.FullName.Any(c => c >= '가' && c <= '힣'),
                $"한글이 아닌 이름: {p.FullName}");
        }

        [Fact]
        public void 성을_앞에_쓰는_나라는_붙여_쓴다()
        {
            // 한국·중국은 "김민준", 일본은 "다나카 하루토", 서양은 "해리 워커".
            Assert.Equal("김민준", NameStyle.Full(Nationality.KOR, "민준", "김"));
            Assert.Equal("왕웨이", NameStyle.Full(Nationality.CHN, "웨이", "왕"));
            Assert.Equal("다나카 하루토", NameStyle.Full(Nationality.JPN, "하루토", "다나카"));
            Assert.Equal("해리 워커", NameStyle.Full(Nationality.ENG, "해리", "워커"));
        }

        [Fact]
        public void 로마자_이름은_예전처럼_띄어_쓴다()
        {
            // 예전 세이브에는 로마자 이름이 들어 있다. 성을 앞에 붙이는 규칙을 그대로
            // 걸면 "Jin-sung Choi" 가 "ChoiJin-sung" 이 된다 — 한글일 때만 적용한다.
            Assert.Equal("Jin-sung Choi", NameStyle.Full(Nationality.KOR, "Jin-sung", "Choi"));
            Assert.Equal("Choi", NameStyle.Short(Nationality.KOR, "Jin-sung", "Choi"));
        }

        [Fact]
        public void 좁은_칸의_짧은_이름도_읽을_수_있다()
        {
            // 서양은 성만 써도 알아보지만, "김"만 남기면 누구인지 알 수 없다.
            Assert.Equal("워커", NameStyle.Short(Nationality.ENG, "해리", "워커"));
            Assert.Equal("김민준", NameStyle.Short(Nationality.KOR, "민준", "김"));
        }

        [Fact]
        public void 나라마다_국기_파일이_있다()
        {
            // 화면이 wwwroot/flags/<코드>.png 를 찾는다. 하나라도 빠지면 깨진 그림이 뜬다.
            string flags = System.IO.Path.Combine(
                System.AppContext.BaseDirectory, "..", "..", "..", "..",
                "FT.App", "wwwroot", "flags");

            if (!System.IO.Directory.Exists(flags)) return;   // 다른 곳에서 돌릴 때는 건너뛴다

            foreach (var n in NameForge.All)
                Assert.True(System.IO.File.Exists(System.IO.Path.Combine(flags, n.Code + ".png")),
                    $"{n.Country}({n.Code}) 국기 파일이 없습니다");
        }
    }
}
