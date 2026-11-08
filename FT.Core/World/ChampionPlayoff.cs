using System.Collections.Generic;
using System.Linq;
using FootballTactician.Data;

namespace FootballTactician.World
{
    /// <summary>
    /// 정규리그 1위가 아니라 <b>토너먼트로 우승팀을 가린다</b> (docs/리그-제도.md ⑥).
    ///
    /// 미국(MLS 컵) · 호주(파이널 시리즈) · 멕시코(리기야) · 인도 · 인도네시아가 그렇다.
    /// 정규리그는 순위를 매기는 과정일 뿐이고, 상위 몇 팀이 다시 붙어 우승을 가져간다.
    /// 정규리그 1위에게는 따로 상이 있다(서포터스 실드 같은 것) — 여기서는 순위표 1위로
    /// 남겨 두고, 우승은 <see cref="LeagueSeason.championClubId"/> 에 따로 적는다.
    ///
    /// <b>승격 플레이오프(④)와 다른 점은 무엇을 거는가뿐이다.</b> 대진은 둘 다
    /// <see cref="Knockout"/> 을 쓴다 — 씨드 열쇠만 달라서, 같은 팀들이 같은 해에 두 대진을
    /// 치러도 결과가 겹치지 않는다.
    ///
    /// 자리를 바꾸지는 않는다. 승강은 정규리그 순위 그대로다 — 실제로도 플레이오프 성적으로
    /// 내려가지는 않는다(애초에 이 나라들은 대개 강등이 없다, ③).
    /// </summary>
    public static class ChampionPlayoff
    {
        /// <summary>우승 대진의 씨드 열쇠와 경기 id 바닥. 승격 대진과 달라야 한다.</summary>
        static readonly Knockout.Rules Rules = new(51229, 910000, "우승");

        /// <summary>
        /// 그 리그의 챔피언 결정전 참가 팀 수. 치르지 않으면 0.
        ///
        /// 리그에서 나라를 거쳐 설정을 찾는 길은 여기 하나뿐이다 — 화면도 시즌 넘김도
        /// 같은 답을 봐야 한다.
        /// </summary>
        public static int EntrantsFor(GameWorld w, LeagueData league)
        {
            if (w == null || league == null) return 0;

            var nation = w.Nations.FirstOrDefault(n => n.LeagueIds.Contains(league.id));
            if (nation == null) return 0;

            int entrants = NameForge.ByCode(nation.Code)?.ChampionPlayoffTeams((int)league.tier) ?? 0;
            return entrants >= 2 && league.clubIds.Count >= entrants ? entrants : 0;
        }

        /// <summary>
        /// 그 리그의 우승팀을 가린다. 플레이오프가 없거나 팀이 모자라면 0 — 그때는
        /// 정규리그 1위가 우승팀이다.
        ///
        /// 시즌이 다 끝난 뒤에, 순위표를 비우기 전에 불러야 한다.
        /// </summary>
        public static int Resolve(GameWorld w, LeagueData league, List<string> log)
        {
            int entrants = EntrantsFor(w, league);
            if (entrants < 2) return 0;

            var table = league.currentSeason?.standings;
            if (table is not { Count: > 0 }) return 0;

            var byPos = table.OrderBy(e => e.position).Select(e => e.clubId).ToList();
            if (byPos.Count < entrants) return 0;

            var field = byPos.Take(entrants).ToList();

            log.Add($"{league.name} 챔피언 결정전 — 정규리그 상위 {entrants}팀");
            log.Add($"  정규리그 1위 {Knockout.Name(w, byPos[0])}");

            int champion = Knockout.Run(w, field, Rules, log);
            league.currentSeason.championClubId = champion;
            return champion;
        }

        /// <summary>
        /// 그 시즌의 우승팀. 플레이오프를 치렀으면 그 승자, 아니면 정규리그 1위다.
        /// 우승을 묻는 곳은 전부 이걸 거쳐야 한다 — 순위표 1위를 직접 읽으면 틀린다.
        /// </summary>
        public static int ChampionOf(LeagueSeason season)
        {
            if (season == null) return 0;
            if (season.championClubId > 0) return season.championClubId;

            var top = season.standings?.OrderBy(e => e.position).FirstOrDefault();
            return top?.clubId ?? 0;
        }

        /// <summary>정규리그 1위가 우승팀과 다른지 — 화면에서 따로 적어 줘야 한다.</summary>
        public static bool WonFromBehind(LeagueSeason season)
        {
            if (season?.championClubId is not > 0) return false;
            var top = season.standings?.OrderBy(e => e.position).FirstOrDefault();
            return top != null && top.clubId != season.championClubId;
        }
    }
}
