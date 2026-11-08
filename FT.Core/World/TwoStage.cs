using System.Collections.Generic;
using System.Linq;
using FootballTactician.Data;

namespace FootballTactician.World
{
    /// <summary>
    /// 한 해를 둘로 갈라 각각 우승팀을 뽑는다 — 아페르투라와 클라우수라
    /// (docs/리그-제도.md ⑦). 멕시코·우루과이·파라과이·코스타리카·온두라스·과테말라.
    ///
    /// <b>일정은 손대지 않는다.</b> 리그 일정은 원래부터 전반기 한 바퀴를 돌고 후반기에
    /// 홈·원정을 뒤집어 한 바퀴를 더 돈다(<see cref="FixtureScheduler"/>). 멕시코 18팀이면
    /// 1~17라운드가 완전한 한 바퀴, 18~34라운드가 또 한 바퀴다. 그러니까 두 대회를 만드는
    /// 데에 필요한 것은 <b>어디서 끊어 셀지</b>뿐이다.
    ///
    /// <b>한 해는 그대로 한 시즌이다.</b> 우승만 두 번 나오고, 계약 만료·나이·예산은
    /// 연 단위로 한 번만 돈다 — 실제로도 멕시코 선수가 한 해에 두 살을 먹지는 않는다.
    ///
    /// <b>강등은 한 해 합산</b>으로 정한다(<see cref="SeasonTable"/>). 반 년 못했다고
    /// 내려보내면 너무 가혹하고, 실제 규정도 두 대회를 합치거나 여러 해 평균을 쓴다.
    /// </summary>
    public static class TwoStage
    {
        public const int Apertura = 0;
        public const int Clausura = 1;

        /// <summary>그 시즌이 둘로 갈리는지.</summary>
        public static bool Has(LeagueSeason season) => season is { stageMatchday: > 0 };

        /// <summary>
        /// 그 리그의 아페르투라 마지막 라운드. 가르지 않으면 0.
        ///
        /// 일정의 딱 절반이다. 스플릿(⑤)과는 겹쳐 쓰지 않는다 — 둘 다 후반 일정을
        /// 자기 방식으로 쓰므로 한 리그에 같이 넣으면 서로 말을 뒤집는다.
        /// </summary>
        public static int BoundaryFor(NationNames spec, LeagueData league)
        {
            if (spec == null || league == null) return 0;
            if (!spec.HasTwoStages((int)league.tier)) return 0;
            if (spec.SplitTopSize((int)league.tier) >= 2) return 0;

            int total = WorldGenerator.Matchdays(league.numberOfTeams);
            return total >= 4 ? total / 2 : 0;
        }

        /// <summary>지금 치르고 있는 단계. 가르지 않는 리그는 늘 아페르투라(0)다.</summary>
        public static int CurrentStage(LeagueSeason season)
            => Has(season) && season.currentMatchday > season.stageMatchday ? Clausura : Apertura;

        /// <summary>그 경기가 그 단계에 드는지. 가르지 않는 리그는 모두 든다.</summary>
        public static bool InStage(LeagueSeason season, FixtureData f, int stage)
            => !Has(season)
               || (stage == Apertura ? f.matchday <= season.stageMatchday
                                     : f.matchday > season.stageMatchday);

        public static string Label(int stage) => stage == Apertura ? "아페르투라" : "클라우수라";

        /// <summary>그 단계만의 순위표.</summary>
        public static List<StandingEntry> StageTable(LeagueData league, int stage)
        {
            var season = league?.currentSeason;
            if (season == null) return new List<StandingEntry>();
            return Standings.Build(league, f => InStage(season, f, stage));
        }

        /// <summary>
        /// 한 해 전체를 합친 순위표. <b>승강과 대륙 출전권은 이걸 본다.</b>
        /// 가르지 않는 리그에서는 지금 순위표와 같다.
        /// </summary>
        public static List<StandingEntry> SeasonTable(LeagueData league)
        {
            var season = league?.currentSeason;
            if (season == null) return new List<StandingEntry>();
            if (!Has(season)) return season.standings ?? new List<StandingEntry>();

            return Standings.Build(league, null);
        }

        /// <summary>
        /// 아페르투라가 끝났으면 그 우승팀을 가린다. 이미 가렸거나 두 단계가 아니면
        /// 아무 일도 없다.
        ///
        /// 순위가 다시 계산된 뒤에 불러야 한다 — 그 순간 <c>standings</c> 가 아페르투라
        /// 순위표다. 챔피언 결정전이 있는 나라(멕시코)는 전반기에도 치른다.
        /// </summary>
        public static void ApplyIfDue(GameWorld w, LeagueData league)
        {
            var season = league?.currentSeason;
            if (season == null || !Has(season)) return;
            if (season.aperturaChampionClubId > 0) return;

            // 전반기를 다 치렀을 때만. 한 경기라도 남았으면 아직이다.
            if (season.fixtures.Any(f => f.matchday <= season.stageMatchday && !f.isPlayed)) return;

            var table = StageTable(league, Apertura);
            if (table.Count == 0) return;

            var log = new List<string> { $"{league.name} 아페르투라" };
            int champion = ChampionPlayoff.ResolveFor(w, league, table, Apertura, log);

            if (champion <= 0)
            {
                champion = table.OrderBy(e => e.position).First().clubId;
                log.Add($"  우승 — {w.Club(champion)?.name}");
            }

            season.aperturaChampionClubId = champion;

            if (w.ManagedClubId > 0 && league.clubIds.Contains(w.ManagedClubId))
            {
                w.Post($"{league.name} 아페르투라 우승",
                       (champion == w.ManagedClubId ? "우리가 가져왔습니다. " : "")
                       + $"{w.Club(champion)?.name} 이 전반기 우승을 차지했습니다. "
                       + "후반기 클라우수라는 순위표를 처음부터 다시 셉니다.\n"
                       + string.Join("\n", log.Skip(1)),
                       InboxKind.Board, clubId: champion);
            }
        }

        /// <summary>
        /// 그 시즌의 우승팀들. 가르지 않으면 하나, 가르면 둘이다 — 같은 구단이 둘 다
        /// 가져갔으면 하나로 치지 않고 둘로 둔다(두 번 우승한 것이 맞다).
        /// </summary>
        public static List<int> ChampionsOf(LeagueSeason season)
        {
            var list = new List<int>();
            if (season == null) return list;

            if (season.aperturaChampionClubId > 0) list.Add(season.aperturaChampionClubId);

            int last = ChampionPlayoff.ChampionOf(season);
            if (last > 0) list.Add(last);

            return list;
        }
    }
}
