using System;
using System.Collections.Generic;
using System.Linq;
using FootballTactician.Balance;
using FootballTactician.Data;

namespace FootballTactician.World
{
    /// <summary>
    /// 시즌 시상과 기록 보관.
    ///
    /// 우승해도 아무 일이 없었다. <c>ClubHistory.leagueTitles</c> 는 세계를 만들 때 한 번
    /// 정해지고 그 뒤로 영영 늘지 않았다 — 20년을 지배해도 명예는 생성 당시 숫자 그대로였다.
    ///
    /// 시상은 <b>이미 센 것</b>으로 뽑는다. 따로 점수를 매기면 화면의 기록과 시상이 어긋난다.
    /// </summary>
    public static class SeasonAwards
    {
        // ── 명예 ──────────────────────────────────────────────────────────

        /// <summary>리그 우승·컵 우승·대륙 우승을 구단 기록에 새긴다.</summary>
        public static void RecordTitles(GameWorld w, List<(int clubId, Honour kind)> winners)
        {
            foreach (var (clubId, kind) in winners)
            {
                var h = w.Club(clubId)?.history;
                if (h == null) continue;

                switch (kind)
                {
                    case Honour.League: h.leagueTitles++; break;
                    case Honour.Cup: h.cupTitles++; break;
                    default: h.continentalTitles++; break;
                }
            }
        }

        public enum Honour { League, Cup, Continental }

        // ── 시상 ──────────────────────────────────────────────────────────

        /// <summary>
        /// 한 리그의 시즌 시상.
        ///
        /// 득점왕은 득점, 도움왕은 도움으로 센다 — 이미 화면에 있는 숫자 그대로다.
        /// 최우수 선수는 평점으로 뽑되 <b>출전 수 문턱</b>을 둔다.
        /// 두 경기 뛰고 평점 9.0 인 선수가 상을 받으면 상이 우스워진다.
        /// </summary>
        public static SeasonAwardSet For(GameWorld w, LeagueData league, int year)
        {
            var squad = league.clubIds.SelectMany(id => w.Squad(id)).ToList();

            var set = new SeasonAwardSet { leagueId = league.id, year = year };
            if (squad.Count == 0) return set;

            int played = league.currentSeason?.standings?.Max(e => e.played) ?? 0;
            int minGames = Math.Max(1, (int)(played * BalanceConfig.AwardMinGamesShare));

            var eligible = squad.Where(p => (p.seasonStats?.appearances ?? 0) >= minGames).ToList();
            if (eligible.Count == 0) eligible = squad;

            var scorer = squad.OrderByDescending(p => p.seasonStats?.goals ?? 0).First();
            set.topScorerId = scorer.id;
            set.topScorerName = scorer.FullName;
            set.topScorerGoals = scorer.seasonStats?.goals ?? 0;

            var assister = squad.OrderByDescending(p => p.seasonStats?.assists ?? 0).First();
            set.topAssistId = assister.id;
            set.topAssistName = assister.FullName;
            set.topAssists = assister.seasonStats?.assists ?? 0;

            var best = eligible.OrderByDescending(p => p.seasonStats?.averageRating ?? 0f).First();
            set.bestPlayerId = best.id;
            set.bestPlayerName = best.FullName;
            set.bestRating = best.seasonStats?.averageRating ?? 0f;

            // 영플레이어는 나이 문턱을 넘지 못한 선수 중에서 고른다.
            var young = eligible.Where(p => w.Age(p) <= BalanceConfig.AwardYoungMaxAge).ToList();
            if (young.Count > 0)
            {
                var y = young.OrderByDescending(p => p.seasonStats?.averageRating ?? 0f).First();
                set.bestYoungId = y.id;
                set.bestYoungName = y.FullName;
                set.bestYoungRating = y.seasonStats?.averageRating ?? 0f;
            }

            // 올해의 감독은 전력 대비 가장 높이 올라간 구단의 감독이다.
            // 그냥 우승팀을 주면 늘 가장 부자 구단이 받는다.
            var table = league.currentSeason?.standings;
            if (table is { Count: > 0 })
            {
                var byStrength = league.clubIds
                    .OrderByDescending(id => w.Squad(id).Any() ? w.Squad(id).Average(GameWorld.Ca) : 0)
                    .ToList();

                var over = table
                    .Select(e => (e.clubId, gap: byStrength.IndexOf(e.clubId) - (e.position - 1)))
                    .OrderByDescending(x => x.gap)
                    .First();

                set.managerClubId = over.clubId;
                set.managerClubName = w.Club(over.clubId)?.name;
                set.managerPlacesGained = over.gap;
            }

            return set;
        }

        /// <summary>모든 리그의 시상을 뽑아 보관하고, 내 구단이 받으면 알린다.</summary>
        public static void RunAll(GameWorld w, int year)
        {
            foreach (var league in w.Leagues.Values)
            {
                var set = For(w, league, year);
                if (set.bestPlayerId == 0 && set.topScorerId == 0) continue;

                w.Awards.Add(set);

                // 기록이 끝없이 쌓이면 세이브가 불어난다. 최근 것만 남긴다.
                if (w.Awards.Count > BalanceConfig.AwardsKept)
                    w.Awards.RemoveAt(0);

                if (w.ManagedClubId <= 0) continue;
                AnnounceMine(w, league, set);
            }
        }

        static void AnnounceMine(GameWorld w, LeagueData league, SeasonAwardSet set)
        {
            var lines = new List<string>();

            void Line(int playerId, string title, string tail)
            {
                var p = w.Player(playerId);
                if (p == null || p.clubId != w.ManagedClubId) return;
                lines.Add($"{title} — {p.FullName} ({tail})");
            }

            Line(set.topScorerId, "득점왕", $"{set.topScorerGoals}골");
            Line(set.topAssistId, "도움왕", $"{set.topAssists}도움");
            Line(set.bestPlayerId, "최우수 선수", $"평점 {set.bestRating:F2}");
            Line(set.bestYoungId, "영플레이어", $"평점 {set.bestYoungRating:F2}");

            if (set.managerClubId == w.ManagedClubId)
                lines.Add($"올해의 감독 — 전력 기준보다 {set.managerPlacesGained}계단 위로 올렸습니다");

            if (lines.Count > 0)
                w.Post($"{set.year} {league.name} 시상",
                       string.Join("\n", lines), InboxKind.News);
        }
    }

}
