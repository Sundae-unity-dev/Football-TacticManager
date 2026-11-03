using System.Collections.Generic;
using System.Linq;
using FootballTactician.Data;
using FootballTactician.World;
using Xunit;

namespace FT.Core.Tests
{
    /// <summary>
    /// 대륙 대회는 <b>대륙마다 하나씩</b>이다.
    ///
    /// 예전에는 대회가 하나였고 모든 나라가 네 팀씩 보냈다. 네 나라짜리 세계에서는
    /// 16팀이라 맞아떨어졌지만, 131개국을 넣으면 한 대회에 524팀이 들어간다 —
    /// 대륙을 넘나드는 대회가 아니라 세계 대회가 되고 대진은 읽을 수 없게 된다.
    /// </summary>
    public class ContinentalCupTests
    {
        static GameWorld Of(params string[] codes)
            => WorldGenerator.Create(31415, 2026,
                   codes.Select(Nations.ByCode).ToList());

        static List<CupState> Continentals(GameWorld w)
            => w.Cups.Values.Where(c => c.continental).ToList();

        [Fact]
        public void 대륙마다_대회가_하나씩_열린다()
        {
            // 유럽 둘 · 아시아 둘 · 남아메리카 하나 → 대회 셋.
            var w = Of("ENG", "ESP", "KOR", "JPN", "BRA");

            var cups = Continentals(w);
            Assert.Equal(3, cups.Count);
            Assert.Contains(cups, c => c.name.Contains("유럽"));
            Assert.Contains(cups, c => c.name.Contains("아시아"));
            Assert.Contains(cups, c => c.name.Contains("남아메리카"));
        }

        [Fact]
        public void 다른_대륙_구단이_한_대회에_섞이지_않는다()
        {
            var w = Of("ENG", "ESP", "KOR", "JPN", "BRA", "ARG", "NGA");

            foreach (var cup in Continentals(w))
            {
                var regions = cup.entrants
                    .Select(id => NameForge.ByCode(w.NationOf(id)!.Code).Region)
                    .Distinct()
                    .ToList();

                Assert.True(regions.Count == 1,
                    $"{cup.name} 에 대륙이 섞였다: {string.Join(", ", regions)}");
            }
        }

        [Fact]
        public void 내_구단은_내_대륙_대회를_본다()
        {
            var w = Of("ENG", "ESP", "KOR", "JPN");
            var korean = w.Clubs.Values.First(c => w.NationOf(c.id)!.Code == "KOR");
            ManagerCareer.TakeCharge(w, korean.id, "테스트 감독");

            var cup = CupCompetition.ContinentalCupFor(w, korean.id);

            Assert.NotNull(cup);
            Assert.Contains("아시아", cup.name);
        }

        [Fact]
        public void 나라가_많은_대륙도_열여섯_팀을_넘지_않는다()
        {
            // 유럽만 여섯 나라. 나라당 네 팀이면 24팀이 되지만 대회는 16팀이다.
            var w = Of("ENG", "ESP", "ITA", "GER", "FRA", "NED");

            var cup = Continentals(w).Single();
            Assert.Equal(16, cup.entrants.Count);
            Assert.True(cup.HasGroups, "열여섯이면 조별리그로 시작해야 한다");
        }

        [Fact]
        public void 강한_나라의_우승팀이_먼저_들어간다()
        {
            // 자리가 모자라면 약한 나라의 2위보다 강한 나라의 1위가 먼저다.
            var w = Of("ENG", "ESP", "ITA", "GER", "FRA", "NED");
            var cup = Continentals(w).Single();

            foreach (string code in new[] { "ENG", "ESP", "ITA", "GER", "FRA", "NED" })
            {
                var nation = w.Nations.First(n => n.Code == code);
                var top = w.League(nation.LeagueIds[0])!;
                int champion = top.clubIds.OrderByDescending(w.Strength).First();

                Assert.Contains(champion, cup.entrants);
            }
        }

        [Fact]
        public void 나라가_하나뿐인_대륙도_대회가_선다()
        {
            // 한국만 있으면 아시아 대회에 네 팀이 모여 토너먼트로 치른다.
            var w = Of("KOR");

            var cup = Continentals(w).Single();
            Assert.Contains("아시아", cup.name);
            Assert.Equal(4, cup.entrants.Count);
            Assert.False(cup.HasGroups, "네 팀이면 바로 토너먼트다");
        }

        [Fact]
        public void 대회_번호는_대륙마다_고정이다()
        {
            // 번호가 흔들리면 세이브를 불러왔을 때 다른 대회를 가리킨다.
            var w = Of("ENG", "KOR", "BRA", "NGA");

            foreach (var cup in Continentals(w))
            {
                int index = cup.id - CupCompetition.ContinentalCupId;
                Assert.InRange(index, 0, CupCompetition.Regions.Length - 1);
                Assert.Contains(CupCompetition.Regions[index], cup.name);
            }
        }

        [Fact]
        public void 나라가_많은_대륙은_조가_여덟이_된다()
        {
            // 자리가 열여섯뿐이면 유럽 마흔 나라 중 스물넷은 평생 대륙 대회를 못 본다.
            var codes = new[]
            {
                "ENG","ESP","ITA","GER","FRA","NED","POR","BEL","TUR","SCO",
                "RUS","UKR","AUT","SUI","DEN","SWE","NOR","POL","GRE","CZE",
            };
            var w = Of(codes);

            var cup = Continentals(w).Single();
            Assert.Equal(32, cup.entrants.Count);
            Assert.Equal(8, cup.groups.Count);
            Assert.Equal(4, cup.roundMatchdays.Length);   // 16강부터라 한 번 더 치른다
        }

        [Fact]
        public void 서른두_팀_대회도_우승자가_나온다()
        {
            var codes = new[]
            {
                "ENG","ESP","ITA","GER","FRA","NED","POR","BEL","TUR","SCO",
                "RUS","UKR","AUT","SUI","DEN","SWE","NOR","POL","GRE","CZE",
            };
            var w = Of(codes);
            ManagerCareer.TakeCharge(w, w.Clubs.Values.First().id, "테스트 감독");

            int guard = 0;
            while (!w.SeasonFinished && guard++ < 60)
                if (GameEngine.PlayNextRound(w) == null) break;

            var cup = Continentals(w).Single();
            Assert.True(cup.winnerClubId > 0,
                $"32팀 대회에 우승자가 없다 — 라운드 {cup.rounds.Count}개");
        }

        [Fact]
        public void 시즌을_넘겨도_대륙마다_하나씩_남는다()
        {
            var w = Of("ENG", "ESP", "KOR", "JPN", "BRA");
            ManagerCareer.TakeCharge(w, w.Clubs.Values.First().id, "테스트 감독");

            int guard = 0;
            while (!w.SeasonFinished && guard++ < 60)
                if (GameEngine.PlayNextRound(w) == null) break;
            SeasonRollover.StartNextSeason(w);

            var cups = Continentals(w);
            Assert.Equal(3, cups.Count);
            Assert.Equal(cups.Count, cups.Select(c => c.id).Distinct().Count());
        }
    }
}
