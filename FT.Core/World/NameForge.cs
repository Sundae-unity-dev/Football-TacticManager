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

        /// <summary>
        /// 부마다의 팀 수. <b>리그의 모양은 그 나라의 성격이다</b> —
        /// 스무 팀이 서른여덟 경기를 치르는 것은 기본값일 뿐이고, 실제로는 열 팀이
        /// 네 번씩 맞붙는 리그도 열두 팀이 치고받는 리그도 있다.
        /// 아래로 갈수록 팀이 늘어나는 나라도 많다(잉글랜드 2·3·4부는 24팀).
        ///
        /// 길이가 <see cref="Tiers"/> 보다 짧으면 마지막 값을 이어 쓴다.
        /// 비워 두면 전부 스무 팀이다(docs/리그-제도.md).
        /// </summary>
        public int[] TeamsPerTier;

        /// <summary>
        /// 부 사이를 오가는 팀 수. 0번이 1부↔2부, 1번이 2부↔3부다.
        ///
        /// <b>내려가는 수와 올라가는 수는 같다</b> — 그래야 리그 크기가 유지된다.
        /// 실제로는 "2팀 자동 강등 + 1팀 플레이오프"처럼 갈라지는 나라가 많지만,
        /// 승격 플레이오프는 아직 없으므로(docs/리그-제도.md ④) 자동 강등 수로 적는다.
        ///
        /// <b>0 이면 승강이 없다</b> — 미국·호주·인도·멕시코처럼 닫힌 리그다.
        /// 비워 두면 세 팀씩 오간다.
        /// </summary>
        public int[] SwapPerTier;

        /// <summary>
        /// 승격 플레이오프에 나가는 팀 수. 0번이 1부↔2부, 1번이 2부↔3부다.
        ///
        /// 0 이면 순위만으로 올라간다. 잉글랜드는 네 팀(2부 3~6위), 이탈리아는 여섯 팀이
        /// 마지막 한 자리를 다툰다 — 나머지 자리는 그대로 자동 승격이다.
        /// </summary>
        public int[] PlayoffPerTier;

        /// <summary>
        /// 승강 플레이오프를 치르는지. 0번이 1부↔2부, 1번이 2부↔3부다.
        ///
        /// 독일식이다 — 내려갈 뻔한 위 리그 팀과 올라올 뻔한 아래 리그 팀이 직접 맞붙는다.
        /// 자동 자리 위에 한 쌍이 더 붙으므로, 아래 팀이 이긴 해에만 오가는 팀이 하나 늘어난다.
        /// </summary>
        public bool[] SurvivalPlayoffPerTier;

        /// <summary>
        /// 스플릿의 상위 조 팀 수. 0 이면 스플릿이 없다 (docs/리그-제도.md ⑤).
        ///
        /// 정규리그를 치른 뒤 상·하위로 갈라 자기들끼리 한 바퀴 더 돈다. 열두 팀이면
        /// 여섯씩 갈라 다섯 경기를 더 치른다.
        /// </summary>
        public int[] SplitPerTier;

        /// <summary>스플릿 때 승점을 절반(올림)으로 줄이는지. 오스트리아·스위스·루마니아·벨기에.</summary>
        public bool[] HalvePointsPerTier;

        /// <summary>
        /// 챔피언 결정 플레이오프에 나가는 팀 수. 0번이 1부, 1번이 2부다
        /// (docs/리그-제도.md ⑥).
        ///
        /// <b>0 이면 정규리그 1위가 곧 우승팀이다</b> — 유럽 대부분이 그렇다. 미국·호주·
        /// 인도·멕시코는 정규리그를 순위를 매기는 과정으로만 쓰고, 상위 팀들이 토너먼트로
        /// 우승을 가린다. 정규리그 1위에게는 따로 상을 준다(서포터스 실드 같은 것).
        /// </summary>
        public int[] ChampionPlayoffPerTier;

        /// <summary>그 부의 상위 조 팀 수. 스플릿이 없으면 0.</summary>
        public int SplitTopSize(int tier)
        {
            if (SplitPerTier == null || SplitPerTier.Length == 0) return 0;
            int i = System.Math.Clamp(tier - 1, 0, SplitPerTier.Length - 1);
            return System.Math.Clamp(SplitPerTier[i], 0, 15);
        }

        /// <summary>그 부에서 갈라질 때 승점을 절반으로 줄이는지.</summary>
        public bool HalvesPoints(int tier)
        {
            if (HalvePointsPerTier == null || HalvePointsPerTier.Length == 0) return false;
            int i = System.Math.Clamp(tier - 1, 0, HalvePointsPerTier.Length - 1);
            return HalvePointsPerTier[i];
        }

        /// <summary>그 경계의 승격 플레이오프 참가 팀 수. 없으면 0.</summary>
        public int PlayoffTeams(int tier)
        {
            if (PlayoffPerTier == null || PlayoffPerTier.Length == 0) return 0;
            int i = System.Math.Clamp(tier - 1, 0, PlayoffPerTier.Length - 1);
            return System.Math.Clamp(PlayoffPerTier[i], 0, 8);
        }

        /// <summary>그 부의 챔피언 결정 플레이오프 참가 팀 수. 없으면 0.</summary>
        public int ChampionPlayoffTeams(int tier)
        {
            if (ChampionPlayoffPerTier == null || ChampionPlayoffPerTier.Length == 0) return 0;
            int i = System.Math.Clamp(tier - 1, 0, ChampionPlayoffPerTier.Length - 1);
            return System.Math.Clamp(ChampionPlayoffPerTier[i], 0, 16);
        }

        /// <summary>그 경계에서 승강 플레이오프를 치르는지.</summary>
        public bool HasSurvivalPlayoff(int tier)
        {
            if (SurvivalPlayoffPerTier == null || SurvivalPlayoffPerTier.Length == 0) return false;
            int i = System.Math.Clamp(tier - 1, 0, SurvivalPlayoffPerTier.Length - 1);
            return SurvivalPlayoffPerTier[i];
        }

        /// <summary>그 부와 한 칸 아래 사이를 오가는 팀 수. 적어 두지 않았으면 세 팀.</summary>
        public int SwapBelow(int tier)
        {
            if (SwapPerTier == null || SwapPerTier.Length == 0) return 3;
            int i = System.Math.Clamp(tier - 1, 0, SwapPerTier.Length - 1);
            return System.Math.Clamp(SwapPerTier[i], 0, 6);
        }

        /// <summary>그 부의 팀 수. 적어 두지 않았으면 스무 팀.</summary>
        public int TeamsIn(int tier)
        {
            if (TeamsPerTier == null || TeamsPerTier.Length == 0) return 20;
            int i = System.Math.Clamp(tier - 1, 0, TeamsPerTier.Length - 1);
            return System.Math.Clamp(TeamsPerTier[i], 6, 30);
        }

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

        /// <summary>
        /// 도시 이름으로 구단 이름을 만든다.
        ///
        /// 도시는 모자라면 돌려 쓴다(한 도시에 구단 둘은 축구에서 흔하다). 그러면
        /// <b>이름이 통째로 겹칠 수 있다</b> — 네덜란드는 도시 24개로 구단 60개를 만들어야
        /// 해서 "FC Eindhoven" 이 1·2·3부에 하나씩 생겼다. 그래서 이미 쓴 이름인지 묻는
        /// <paramref name="nameTaken"/> 을 받아, 빌 때까지 접사 조합을 돌린다.
        ///
        /// 약칭도 같다. 도시 앞 세 글자만 쓰면 같은 리그에 Valencia 와 Valladolid 가
        /// 함께 있을 때 둘 다 VAL 이 된다 — 순위표에서 어느 쪽인지 알 수 없다.
        /// </summary>
        public static (string name, string shortName) ClubName(
            NationNames n, string city, int index,
            Func<string, bool> nameTaken = null, Func<string, bool> shortTaken = null)
        {
            string full = null;
            int combos = Math.Max(1, n.ClubPrefixes.Length * n.ClubSuffixes.Length);
            for (int step = 0; step < combos; step++)
            {
                full = Compose(n, city, index + step);
                if (nameTaken == null || !nameTaken(full)) break;
            }

            // 접사를 다 돌려도 겹치면(도시를 세 번 넘게 돌려 쓴 경우) 번호를 붙인다.
            if (nameTaken != null && nameTaken(full))
                for (int k = 2; k <= 9 && nameTaken(full); k++)
                    full = Compose(n, city, index) + " " + k.ToString();

            return (full, ShortName(city, shortTaken));
        }

        static string Compose(NationNames n, string city, int index)
        {
            string prefix = n.ClubPrefixes[index % n.ClubPrefixes.Length];
            string suffix = n.ClubSuffixes[(index / n.ClubPrefixes.Length) % n.ClubSuffixes.Length];

            // 접두와 접미가 겹치면("CF Treviso Mar CF") 접미를 버린다.
            if (suffix == prefix) suffix = "";

            string head = string.IsNullOrEmpty(prefix) ? city : $"{prefix} {city}";
            return string.IsNullOrEmpty(suffix) ? head : $"{head} {suffix}";
        }

        /// <summary>
        /// 약칭 세 글자. 앞 세 글자가 이미 쓰였으면 자음 뼈대, 그다음은 글자를 건너뛰며
        /// 비는 것을 찾는다. 폭이 일정해야 순위표가 흔들리지 않으므로 길이는 셋으로 둔다.
        /// </summary>
        static string ShortName(string city, Func<string, bool> taken)
        {
            string core = city.Replace(" ", "").Replace("-", "").ToUpperInvariant();
            if (core.Length < 3) core = (core + "XXX").Substring(0, 3);

            foreach (string candidate in ShortCandidates(core))
                if (taken == null || !taken(candidate)) return candidate;

            return core.Substring(0, 3);
        }

        static IEnumerable<string> ShortCandidates(string core)
        {
            yield return core.Substring(0, 3);

            // 자음 뼈대 — Valencia 는 VLN, Valladolid 는 VLL 이 된다.
            var skeleton = new System.Text.StringBuilder();
            skeleton.Append(core[0]);
            foreach (char c in core.Substring(1))
            {
                if ("AEIOU".IndexOf(c) >= 0) continue;
                skeleton.Append(c);
                if (skeleton.Length == 3) break;
            }
            if (skeleton.Length == 3) yield return skeleton.ToString();

            // 글자를 하나씩 건너뛰며 본다.
            for (int skip = 2; skip < core.Length && skip < 7; skip++)
                if (core.Length > skip)
                    yield return $"{core[0]}{core[1]}{core[skip]}";

            // 그래도 비는 것이 없으면 끝 글자를 돌린다.
            for (char c = 'A'; c <= 'Z'; c++)
                yield return $"{core[0]}{core[1]}{c}";
        }

        public static string StadiumName(NationNames n, Random rng, string city)
            => $"{city} {n.StadiumWords[rng.Next(n.StadiumWords.Length)]}";
    }
}
