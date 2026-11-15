using System;
using System.Collections.Generic;
using FootballTactician.Data;

namespace FootballTactician.World
{
    /// <summary>
    /// 순위표의 구간 — 그 자리가 시즌 끝에 어떻게 되는지.
    ///
    /// 화면이 짐작으로 칠하면 시즌 끝에 실제로 오르내리는 팀과 어긋난다. 예전 순위표는
    /// "2부 이하는 강등권이 없다"고 보고 칠하지 않았는데, 3부가 있는 나라의 2부는 내려간다.
    /// 그래서 승강을 실제로 정하는 <see cref="PromotionPlayoff.Resolve"/> 와 같은 판단을
    /// 여기 한 곳에 둔다. 저쪽 규칙이 바뀌면 이쪽도 같이 바꿔야 한다 — 테스트가 지킨다.
    /// </summary>
    public static class LeagueZones
    {
        public enum Zone
        {
            None,
            Title,              // 1부 1위 — 우승
            ChampionPlayoff,    // 챔피언 결정전 진출(⑥)
            Continental,        // 대륙 대회
            Promotion,          // 자동 승격
            PromotionPlayoff,   // 승격 플레이오프 · 승강 플레이오프(아래 리그 쪽)
            SurvivalPlayoff,    // 승강 플레이오프(위 리그 쪽)
            Relegation,         // 강등
        }

        /// <summary>그 리그 그 순위의 구간.</summary>
        public static Zone Of(GameWorld w, LeagueData league, int position)
        {
            if (w == null || league == null || position <= 0) return Zone.None;
            int n = league.clubIds.Count;

            var nation = NationOf(w, league);
            var spec = nation == null ? null : NameForge.ByCode(nation.Code);
            int k = nation == null ? (int)league.tier : nation.LeagueIds.IndexOf(league.id) + 1;

            // ── 아래쪽 — 한 칸 아래 리그와의 경계(경계 번호 k) ──
            if (nation != null && k < nation.LeagueIds.Count && w.League(nation.LeagueIds[k]) is { } below)
            {
                int spots = Math.Min(league.relegationSpots, below.promotionSpots);
                if (spots > 0)
                {
                    int fromBottom = n - position + 1;
                    if (fromBottom <= spots) return Zone.Relegation;

                    bool promoPlayoff = (spec?.PlayoffTeams(k) ?? 0) >= 2;
                    if (!promoPlayoff && (spec?.HasSurvivalPlayoff(k) ?? false) && fromBottom == spots + 1)
                        return Zone.SurvivalPlayoff;
                }
            }

            // ── 위쪽 ──
            if (k <= 1)
            {
                int po = ChampionPlayoff.EntrantsFor(w, league);
                if (po >= 2) return position <= po ? Zone.ChampionPlayoff : Zone.None;
                if (position == 1) return Zone.Title;
                if (position <= 4) return Zone.Continental;
                return Zone.None;
            }

            if (w.League(nation.LeagueIds[k - 2]) is { } above)
            {
                int spots = Math.Min(above.relegationSpots, league.promotionSpots);
                if (spots <= 0) return Zone.None;

                int entrants = spec?.PlayoffTeams(k - 1) ?? 0;
                if (entrants >= 2)
                {
                    int auto = spots - 1;
                    if (position <= auto) return Zone.Promotion;
                    if (position <= auto + entrants) return Zone.PromotionPlayoff;
                    return Zone.None;
                }

                if (position <= spots) return Zone.Promotion;
                if ((spec?.HasSurvivalPlayoff(k - 1) ?? false) && position == spots + 1)
                    return Zone.PromotionPlayoff;
            }
            return Zone.None;
        }

        /// <summary>그 리그에 실제로 나오는 구간들. 범례에 쓴다 — 없는 구간을 범례에 적으면 거짓말이 된다.</summary>
        public static List<Zone> Present(GameWorld w, LeagueData league)
        {
            var seen = new List<Zone>();
            for (int pos = 1; pos <= league.clubIds.Count; pos++)
            {
                var z = Of(w, league, pos);
                if (z != Zone.None && !seen.Contains(z)) seen.Add(z);
            }
            seen.Sort();
            return seen;
        }

        public static string Label(Zone z) => z switch
        {
            Zone.Title => "우승",
            Zone.ChampionPlayoff => "챔피언 결정전",
            Zone.Continental => "대륙 대회",
            Zone.Promotion => "승격",
            Zone.PromotionPlayoff => "승격 플레이오프",
            Zone.SurvivalPlayoff => "잔류 플레이오프",
            Zone.Relegation => "강등",
            _ => "",
        };

        static NationState NationOf(GameWorld w, LeagueData league)
        {
            foreach (var n in w.Nations)
                if (n.LeagueIds.Contains(league.id)) return n;
            return null;
        }
    }
}
