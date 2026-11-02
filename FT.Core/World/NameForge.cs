using System;
using System.Collections.Generic;
using FootballTactician.Data;

namespace FootballTactician.World
{
    /// <summary>
    /// 국가 하나의 이름 재료. 실제 리그·구단·선수 이름은 쓸 수 없으므로 전부 가상이다.
    /// 국가마다 어감을 다르게 두어 이적 목록에서 출신을 구분할 수 있게 한다.
    /// </summary>
    public sealed class NationNames
    {
        public string Code;              // "NSL"
        public Nationality Nationality;  // 모델 enum 과의 연결
        public string Country;           // "노르셀"
        public string LeagueName;        // "노르셀 프리미어"
        public string LeagueShort;       // "NPL"
        public string SecondLeagueName;  // "노르셀 1부리그" — 2부 리그
        public string SecondLeagueShort;
        public string ThirdLeagueName;   // 3부 리그
        public string ThirdLeagueShort;
        public string Currency = "€";

        /// <summary>
        /// 이 나라의 리그 단계 수(1~3). 축구가 깊은 나라만 3부까지 둔다 —
        /// 나라 하나가 세계를 무겁게 하는 정도가 이 값에 비례한다.
        /// </summary>
        public int Tiers = 3;

        /// <summary>
        /// 나라의 수준(0~1). 구단 평판에 곱해져 <b>수입과 선수 수준</b>을 함께 정한다.
        ///
        /// 이것이 없으면 잉글랜드 1부와 뉴질랜드 1부가 전력도 수입도 똑같다 —
        /// 나라를 실제 이름으로 바꾼 의미가 사라진다. 수입은 평판의 3.4제곱이므로
        /// 0.6 인 나라는 1.0 인 나라의 17% 를 번다. 그 격차가 이적의 방향을 만든다.
        /// </summary>
        public float Strength = 1.0f;

        /// <summary>대륙. 새 게임에서 나라를 고를 때 묶어 보여 준다.</summary>
        public string Region = "유럽";

        public string[] Cities;
        public string[] ClubPrefixes;    // 도시 앞에 붙는 것
        public string[] ClubSuffixes;    // 도시 뒤에 붙는 것
        public string[] StadiumWords;
        public string[] FirstNames;
        public string[] LastNames;
    }

    public static class NameForge
    {
        /// <summary>
        /// 세계에 넣을 나라들. 기본은 <see cref="Nations.All"/> 전부다.
        ///
        /// 나라 수가 곧 세계의 크기이므로(3부 나라 = 구단 60개·선수 1,500명),
        /// 새 게임을 시작할 때 고른 나라만 넣을 수 있다 — WorldGenerator.Create 가
        /// 받는 목록이 비면 이 값을 쓴다.
        /// </summary>
        public static readonly IReadOnlyList<NationNames> All = Nations.All;

        public static NationNames ByCode(string code)
        {
            foreach (var n in All) if (n.Code == code) return n;
            return All[0];
        }

        /// <summary>도시 이름으로 구단 이름을 만든다. index 로 접사를 돌려 같은 리그 안에서 겹치지 않게 한다.</summary>
        public static (string name, string shortName) ClubName(NationNames n, string city, int index)
        {
            string prefix = n.ClubPrefixes[index % n.ClubPrefixes.Length];
            string suffix = n.ClubSuffixes[(index / n.ClubPrefixes.Length) % n.ClubSuffixes.Length];

            // 접두와 접미가 겹치면("CF Treviso Mar CF") 접미를 버린다.
            if (suffix == prefix) suffix = "";

            string full = string.IsNullOrEmpty(suffix)
                ? $"{prefix} {city}"
                : $"{prefix} {city} {suffix}";

            // 약칭은 도시명 앞 3글자 — 순위표 폭을 일정하게 유지한다.
            string core = city.Replace(" ", "");
            string sn = core.Length >= 3 ? core.Substring(0, 3).ToUpperInvariant() : core.ToUpperInvariant();
            return (full, sn);
        }

        public static string StadiumName(NationNames n, Random rng, string city)
            => $"{city} {n.StadiumWords[rng.Next(n.StadiumWords.Length)]}";
    }
}
