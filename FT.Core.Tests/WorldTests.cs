using System;
using System.Collections.Generic;
using System.Linq;
using FootballTactician.Data;
using FootballTactician.World;
using Xunit;

namespace FT.Core.Tests
{
    /// <summary>
    /// 세계 생성·일정·진행에 대한 회귀. FT.Sim world / audit 로 확인한 것을 빌드에 묶어 둔다.
    /// </summary>
    public class WorldTests
    {
        static GameWorld Make(int seed = 20261017) => WorldGenerator.Create(seed);

        // ── 생성 ──────────────────────────────────────────────────────────

        [Fact]
        public void 세계는_국가마다_리그_피라미드를_갖춘다()
        {
            var w = Make();

            // 넣은 나라만 들어가고, 나라마다 자기 부 수만큼 리그를 갖는다.
            // (목록 전체가 아니라 기본 묶음이 들어간다 — 49개국을 다 넣으면 세계가 6배가 된다.)
            var put = Nations.Default;
            int expectedLeagues = put.Sum(n => n.Tiers);

            Assert.Equal(put.Count, w.Nations.Count);
            Assert.Equal(expectedLeagues, w.Leagues.Count);
            // 리그마다 팀 수가 다르므로 합으로 센다.
            int expectedClubs = put.Sum(n =>
            {
                int total = 0;
                for (int t = 1; t <= n.Tiers; t++) total += n.TeamsIn(t);
                return total;
            });
            Assert.Equal(expectedClubs, w.Clubs.Count);
            Assert.Equal(w.Clubs.Count * WorldGenerator.SquadSize, w.Players.Count);

            foreach (var nation in w.Nations)
            {
                int tiers = NameForge.ByCode(nation.Code).Tiers;
                Assert.Equal(tiers, nation.LeagueIds.Count);

                for (int t = 0; t < tiers; t++)
                {
                    var league = w.League(nation.LeagueIds[t])!;
                    Assert.Equal((LeagueTier)(t + 1), league.tier);

                    // 맨 위는 올라갈 곳이, 맨 아래는 내려갈 곳이 없다.
                    Assert.Equal(t == 0 ? 0 : WorldGenerator.PromotionSpots, league.promotionSpots);
                    Assert.Equal(t == tiers - 1 ? 0 : WorldGenerator.PromotionSpots, league.relegationSpots);

                    if (t + 1 >= tiers) continue;

                    // 한 칸씩만 이어야 3부에서 1부로 건너뛰지 않는다.
                    var below = w.League(nation.LeagueIds[t + 1])!;
                    Assert.Equal(below.id, league.relegationLeagueId);
                    Assert.Equal(league.id, below.promotionLeagueId);
                    Assert.Equal(league.relegationSpots, below.promotionSpots);
                }

                // 도시가 모자라면 같은 나라에 같은 이름의 구단이 둘 생긴다.
                var names = nation.LeagueIds
                    .SelectMany(id => w.League(id)!.clubIds)
                    .Select(id => w.Club(id)!.name)
                    .ToList();
                Assert.Equal(names.Count, names.Distinct().Count());
            }

            foreach (var club in w.Clubs.Values)
            {
                Assert.Equal(WorldGenerator.SquadSize, club.playerIds.Count);
                Assert.Equal(club.playerIds.Count, club.playerIds.Distinct().Count());
                Assert.NotEmpty(club.name);
                Assert.NotNull(club.stadium);
            }
        }

        [Fact]
        public void 같은_시드는_같은_세계를_만든다()
        {
            var a = Make(4242);
            var b = Make(4242);
            Assert.Equal(a.Clubs[1].name, b.Clubs[1].name);
            Assert.Equal(a.Players[1].FullName, b.Players[1].FullName);
            Assert.Equal(a.Strength(1), b.Strength(1), 3);
        }

        [Fact]
        public void 평판이_높은_구단이_더_강하다()
        {
            var w = Make();
            var byRep = w.Clubs.Values
                .GroupBy(c => c.reputation)
                .ToDictionary(g => g.Key, g => g.Average(c => w.Strength(c.id)));

            for (int stars = 2; stars <= 5; stars++)
                Assert.True(byRep[stars] > byRep[stars - 1],
                    $"별 {stars} ({byRep[stars]:F1}) 이 별 {stars - 1} ({byRep[stars - 1]:F1}) 보다 강해야 한다");
        }

        [Fact]
        public void 모든_구단에_골키퍼가_선발된다()
        {
            var w = Make();
            foreach (var club in w.Clubs.Values)
            {
                var lineup = MatchDay.BuildLineup(w, club.id);
                Assert.NotNull(lineup);

                var gkSlot = lineup.tactics.slots.First(s => (s.position & Position.GK) != 0);
                var keeper = w.Player(gkSlot.assignedPlayerId);
                Assert.NotNull(keeper);
                Assert.True((keeper.positionFlags & Position.GK) != 0,
                    $"{club.name} 의 골문에 필드 선수가 섰다");

                // 11 자리가 서로 다른 선수로 채워진다
                var ids = lineup.tactics.slots.Select(s => s.assignedPlayerId).ToList();
                Assert.Equal(11, ids.Count);
                Assert.DoesNotContain(-1, ids);
                Assert.Equal(11, ids.Distinct().Count());
            }
        }

        [Fact]
        public void 최전방에_팀_최고_공격수가_선다()
        {
            var w = Make(2468);

            foreach (var club in w.Clubs.Values)
            {
                var lineup = MatchDay.BuildLineup(w, club.id)!;
                var strikerSlot = lineup.tactics.slots
                    .FirstOrDefault(s => (s.position & (Position.ST | Position.CF)) != 0);
                if (strikerSlot == null) continue;

                var picked = w.Player(strikerSlot.assignedPlayerId);
                Assert.NotNull(picked);

                // 벤치에 더 나은 스트라이커를 남겨두면 안 된다.
                // 슬롯 순서대로 탐욕 배치하면 윙 자리가 최고 공격수를 먼저 데려가고
                // 최전방에 유망주가 남는다.
                var starters = lineup.tactics.slots.Select(s => s.assignedPlayerId).ToHashSet();
                var benchedStriker = w.Squad(club.id)
                    .Where(p => !p.IsInjured && !starters.Contains(p.id))
                    .Where(p => (p.positionFlags & (Position.ST | Position.CF)) != 0)
                    .OrderByDescending(GameWorld.Ca)
                    .FirstOrDefault();

                if (benchedStriker != null)
                    Assert.True(GameWorld.Ca(picked!) >= GameWorld.Ca(benchedStriker) - 1f,
                        $"{club.name}: 최전방 {picked!.FullName}({GameWorld.Ca(picked):F0}) 보다 " +
                        $"{benchedStriker.FullName}({GameWorld.Ca(benchedStriker):F0}) 이 벤치에 있다");
            }
        }

        [Fact]
        public void 구단_안에_같은_이름의_선수가_없다()
        {
            var w = Make(13579);
            foreach (var club in w.Clubs.Values)
            {
                var names = w.Squad(club.id).Select(p => p.FullName).ToList();
                Assert.Equal(names.Count, names.Distinct().Count());
            }
        }

        [Fact]
        public void 기대_득점은_모든_슈팅에_쌓인다()
        {
            var w = Make(86420);
            var league = w.League(w.Nations[0].LeagueId)!;

            int guard = 0;
            while (guard++ < 6) GameEngine.PlayRound(w, league);

            var played = league.currentSeason.fixtures.Where(f => f.isPlayed).ToList();
            Assert.NotEmpty(played);

            double shots = played.Sum(f => f.result.homeStats.shots + f.result.awayStats.shots);
            double xg = played.Sum(f => f.result.homeStats.xG + f.result.awayStats.xG);
            double goals = played.Sum(f => f.result.homeGoals + f.result.awayGoals);

            // 슈팅당 기대 득점은 실제 축구에서 0.08~0.14 사이다.
            Assert.InRange(xg / shots, 0.06, 0.18);
            // 합계 xG 는 실제 득점과 같은 자릿수여야 한다.
            Assert.InRange(xg / goals, 0.6, 1.6);
        }

        // ── 일정 ──────────────────────────────────────────────────────────

        [Fact]
        public void 일정은_더블_라운드로빈이다()
        {
            var w = Make();
            foreach (var league in w.Leagues.Values)
            {
                int n = league.clubIds.Count;
                var fx = league.currentSeason.fixtures;

                Assert.Equal(n * (n - 1), fx.Count);
                Assert.Equal((n - 1) * 2, league.currentSeason.totalMatchdays);

                // 대진은 중복되지 않고, 홈·원정 한 번씩 성립한다
                var pairs = fx.Select(f => (f.homeClubId, f.awayClubId)).ToHashSet();
                Assert.Equal(fx.Count, pairs.Count);
                foreach (var (h, a) in pairs)
                    Assert.Contains((a, h), pairs);
            }
        }

        [Fact]
        public void 한_라운드에_모든_구단이_한_번씩_나온다()
        {
            var w = Make();
            foreach (var league in w.Leagues.Values)
                foreach (var round in league.currentSeason.fixtures.GroupBy(f => f.matchday))
                {
                    var sides = round.SelectMany(f => new[] { f.homeClubId, f.awayClubId }).ToList();
                    Assert.Equal(league.clubIds.Count, sides.Count);
                    Assert.Equal(sides.Count, sides.Distinct().Count());
                }
        }

        [Fact]
        public void 홈_경기_수가_구단마다_같다()
        {
            var w = Make();
            foreach (var league in w.Leagues.Values)
            {
                var fx = league.currentSeason.fixtures;
                var home = league.clubIds.Select(id => fx.Count(f => f.homeClubId == id)).ToList();
                Assert.Equal(league.clubIds.Count - 1, home.Min());
                Assert.Equal(league.clubIds.Count - 1, home.Max());
            }
        }

        [Theory]
        [InlineData(1111)]
        [InlineData(2222)]
        [InlineData(31415)]
        [InlineData(90210)]
        public void 홈과_원정이_시즌_내내_번갈아_나온다(int seed)
        {
            // 한 시드만 보면 통과한다. 일정은 어떤 세계에서도 성립해야 하는 성질이다.
            var w = Make(seed);

            foreach (var league in w.Leagues.Values)
            {
                var fx = league.currentSeason.fixtures;

                foreach (int clubId in league.clubIds)
                {
                    var venues = fx
                        .Where(f => f.homeClubId == clubId || f.awayClubId == clubId)
                        .OrderBy(f => f.matchday)
                        .Select(f => f.homeClubId == clubId)
                        .ToList();

                    // 전반기를 전부 홈으로 치르고 후반기를 전부 원정으로 가는 일정이 나온 적이 있다.
                    // 전반기 홈은 그 구단 홈 경기의 절반 언저리여야 한다 — 경기 수로 재면
                    // 상한이 반기 경기 수를 넘어 사실상 검사가 되지 않는다.
                    int homeTotal = venues.Count(h => h);
                    int firstHalfHome = venues.Take(venues.Count / 2).Count(h => h);
                    Assert.InRange(firstHalfHome, homeTotal / 2 - 3, homeTotal / 2 + 3);

                    int run = 1, maxRun = 1;
                    for (int i = 1; i < venues.Count; i++)
                    {
                        run = venues[i] == venues[i - 1] ? run + 1 : 1;
                        maxRun = Math.Max(maxRun, run);
                    }
                    Assert.True(maxRun <= 4,
                        $"{w.Club(clubId)!.name} 이 같은 장소에서 {maxRun}경기 연속으로 뛴다");
                }
            }
        }

        // ── 진행 ──────────────────────────────────────────────────────────

        [Fact]
        public void 한_시즌을_끝까지_진행할_수_있다()
        {
            var w = Make(555);
            var club = w.Clubs.Values.First(c => c.reputation == 3);
            ManagerCareer.TakeCharge(w, club.id, "테스트");

            int guard = 0;
            while (!w.SeasonFinished && guard++ < 60)
                if (GameEngine.PlayNextRound(w) == null) break;

            var season = w.MyLeague.currentSeason;
            Assert.True(w.SeasonFinished, "시즌이 끝나지 않았다");
            Assert.All(season.fixtures, f => Assert.NotNull(f.result));

            // 순위표 합계가 치른 경기와 맞는다
            int n = w.MyLeague.clubIds.Count;
            Assert.All(season.standings, e => Assert.Equal((n - 1) * 2, e.played));
            Assert.Equal(season.fixtures.Count * 2,
                         season.standings.Sum(e => e.won + e.drawn + e.lost));
            Assert.Equal(season.standings.Sum(e => e.goalsFor),
                         season.standings.Sum(e => e.goalsAgainst));
        }

        [Fact]
        public void 시즌_득점이_실제_축구_범위에_있다()
        {
            var w = Make(777);
            var league = w.League(w.Nations[0].LeagueId);

            int guard = 0;
            while (!GameEngine.LeagueFinished(league) && guard++ < 60)
                if (GameEngine.PlayRound(w, league) == 0) break;

            int played = league.currentSeason.fixtures.Count(f => f.isPlayed);
            double goals = league.currentSeason.fixtures
                .Sum(f => f.result.homeGoals + f.result.awayGoals) / (double)played;

            Assert.InRange(goals, 2.3, 3.3);
        }

        [Fact]
        public void 전력이_최종_순위에_반영된다()
        {
            var w = Make(31337);
            var league = w.League(w.Nations[0].LeagueId);
            var rank = league.clubIds
                .OrderByDescending(id => w.Strength(id))
                .Select((id, i) => (id, r: i + 1))
                .ToDictionary(t => t.id, t => t.r);

            int guard = 0;
            while (!GameEngine.LeagueFinished(league) && guard++ < 60)
                if (GameEngine.PlayRound(w, league) == 0) break;

            double gap = league.currentSeason.standings
                .Average(e => Math.Abs(e.position - rank[e.clubId]));

            // 순위가 무작위면 평균 오차가 약 6.7 이 된다.
            Assert.True(gap < 3.5, $"전력이 순위에 반영되지 않는다 — 평균 오차 {gap:F2}");
        }

        [Fact]
        public void 컨디션은_시즌_내내_바닥으로_흐르지_않는다()
        {
            var w = Make(909);
            var club = w.Clubs.Values.First(c => c.reputation == 4);
            ManagerCareer.TakeCharge(w, club.id, "테스트");

            int guard = 0;
            while (!w.SeasonFinished && guard++ < 60)
                if (GameEngine.PlayNextRound(w) == null) break;

            // 컨디션은 골키퍼 전력에도 곱해진다. 바닥으로 흐르면 시즌 후반 득점이 부풀려진다.
            float avg = w.Squad(club.id).Average(p => p.condition);
            Assert.InRange(avg, 70f, 100f);
        }

        [Fact]
        public void 부상에서_돌아온_선수는_선발로_복귀할_수_있다()
        {
            var w = Make(123);
            var club = w.Clubs.Values.First();
            var starter = w.Squad(club.id).OrderByDescending(GameWorld.Ca).First();

            starter.injury = new InjuryData
            {
                injuryType = "테스트",
                severity = InjurySeverity.Moderate,
                injuryDate = w.Date,
                expectedReturnDate = w.Date.AddDays(28),
                isActive = true,
            };
            var hurt = MatchDay.BuildLineup(w, club.id);
            Assert.DoesNotContain(starter.id, hurt.tactics.slots.Select(s => s.assignedPlayerId));

            starter.injury.isActive = false;
            var healed = MatchDay.BuildLineup(w, club.id);
            Assert.Contains(starter.id, healed.tactics.slots.Select(s => s.assignedPlayerId));
        }

        [Fact]
        public void 목표를_채우면_시즌_말에_달성으로_처리된다()
        {
            // 강팀을 맡아 상위권으로 마치게 한다.
            var w = Make(4321);
            var league = w.League(w.Nations[0].LeagueId)!;
            var best = league.clubIds.OrderByDescending(id => w.Strength(id)).First();
            ManagerCareer.TakeCharge(w, best, "테스트");

            int guard = 0;
            while (!w.SeasonFinished && guard++ < 60)
                if (GameEngine.PlayNextRound(w) == null) break;

            int pos = Standings.PositionOf(league, best);
            var leagueGoal = w.Objectives.First(o => o.type == BoardObjectiveType.LeagueFinish
                                                  || o.type == BoardObjectiveType.AvoidRelegation);

            // 시즌이 끝났으면 어떤 목표도 '진행 중' 으로 남아서는 안 된다.
            // 마지막 경기 날짜가 마감일보다 이르다는 이유로 미정이던 적이 있다.
            Assert.All(w.Objectives, o => Assert.NotEqual(BoardObjectiveResult.Pending, o.result));

            if (pos <= leagueGoal.targetValue)
                Assert.Equal(BoardObjectiveResult.Achieved, leagueGoal.result);
        }

        [Fact]
        public void 목표를_달성한_감독은_경질되지_않는다()
        {
            var w = Make(4321);
            var league = w.League(w.Nations[0].LeagueId)!;
            var best = league.clubIds.OrderByDescending(id => w.Strength(id)).First();
            ManagerCareer.TakeCharge(w, best, "테스트");

            int guard = 0;
            while (!w.SeasonFinished && guard++ < 60)
                if (GameEngine.PlayNextRound(w) == null) break;

            int pos = Standings.PositionOf(league, best);
            var leagueGoal = w.Objectives.First(o => o.type == BoardObjectiveType.LeagueFinish
                                                  || o.type == BoardObjectiveType.AvoidRelegation);

            if (leagueGoal.result == BoardObjectiveResult.Achieved)
                Assert.False(w.Sacked,
                    $"{pos}위로 목표를 채웠는데 경질됐다 — 신뢰도 {w.Manager.boardConfidence}");
        }

        // ── 성장 ──────────────────────────────────────────────────────────

        [Fact]
        public void 유망주는_한_시즌_동안_실제로_성장한다()
        {
            var w = Make(246);
            var rng = new Random(11);
            var young = w.Players.Values
                .Where(p => w.Age(p) <= 19 && GameWorld.Ca(p) < PlayerDevelopment.Pa(p) * 0.75f)
                .Take(40).ToList();

            Assert.NotEmpty(young);
            var before = young.ToDictionary(p => p.id, GameWorld.Ca);

            for (int week = 0; week < 40; week++)
                foreach (var p in young)
                    PlayerDevelopment.ApplyWeek(w, p, rng, 1.0f, 1.0f, 1.0f);

            float gain = young.Average(p => GameWorld.Ca(p) - before[p.id]);
            Assert.InRange(gain, 3f, 20f);
        }

        [Fact]
        public void 성장은_잠재력을_넘지_않는다()
        {
            var w = Make(357);
            var rng = new Random(5);
            var young = w.Players.Values.Where(p => w.Age(p) <= 20).Take(60).ToList();

            for (int week = 0; week < 40 * 8; week++)
                foreach (var p in young)
                    PlayerDevelopment.ApplyWeek(w, p, rng, 1.7f, 1.4f, 1.2f);

            foreach (var p in young)
                Assert.True(GameWorld.Ca(p) <= PlayerDevelopment.Pa(p) + 12f,
                    $"{p.FullName} CA {GameWorld.Ca(p):F0} 가 PA {PlayerDevelopment.Pa(p):F0} 를 크게 넘었다");
        }

        // ── 세이브 ────────────────────────────────────────────────────────

        [Fact]
        public void 세이브는_왕복해도_같은_상태를_준다()
        {
            var w = Make(8080);
            var club = w.Clubs.Values.First(c => c.reputation == 3);
            ManagerCareer.TakeCharge(w, club.id, "테스트 감독");
            for (int i = 0; i < 5; i++) GameEngine.PlayNextRound(w);

            var back = SaveGame.Deserialize(SaveGame.Serialize(w, "회귀")).world;

            Assert.Equal(w.Clubs.Count, back.Clubs.Count);
            Assert.Equal(w.Players.Count, back.Players.Count);
            Assert.Equal(w.ManagedClubId, back.ManagedClubId);
            Assert.Equal(w.ManagerName, back.ManagerName);
            Assert.Equal(w.Manager.boardConfidence, back.Manager.boardConfidence);
            Assert.Equal(w.Date, back.Date);
            Assert.Equal(w.Objectives.Count, back.Objectives.Count);

            var a = w.Players.Values.OrderBy(p => p.id).First();
            var b = back.Player(a.id);
            Assert.Equal(a.FullName, b.FullName);
            Assert.Equal(a.AbilityScore, b.AbilityScore, 3);
            Assert.Equal(a.condition, b.condition, 3);

            var la = w.MyLeague.currentSeason;
            var lb = back.MyLeague.currentSeason;
            Assert.Equal(la.fixtures.Count(f => f.isPlayed), lb.fixtures.Count(f => f.isPlayed));
            Assert.Equal(la.standings.Select(s => s.clubId), lb.standings.Select(s => s.clubId));
            Assert.Equal(la.standings.Select(s => s.points), lb.standings.Select(s => s.points));
        }

        [Fact]
        public void 능력치는_하나도_빠짐없이_왕복한다()
        {
            // 능력치를 이름 없는 배열로 저장한다. 순서가 곧 포맷이라, 필드를 끼워 넣거나
            // 지우면 전혀 다른 값으로 읽힌다. 한 명이라도 어긋나면 안 된다.
            var w = Make(13572);
            var back = SaveGame.Deserialize(SaveGame.Serialize(w, "능력치")).world;

            foreach (var a in w.Players.Values)
            {
                var b = back.Player(a.id);
                Assert.NotNull(b);

                Assert.Equal(a.technical.passing, b!.technical.passing);
                Assert.Equal(a.technical.dribbling, b.technical.dribbling);
                Assert.Equal(a.technical.shooting, b.technical.shooting);
                Assert.Equal(a.technical.finishing, b.technical.finishing);
                Assert.Equal(a.technical.heading, b.technical.heading);
                Assert.Equal(a.technical.crossing, b.technical.crossing);
                Assert.Equal(a.technical.tackling, b.technical.tackling);
                Assert.Equal(a.technical.marking, b.technical.marking);
                Assert.Equal(a.technical.firstTouch, b.technical.firstTouch);
                Assert.Equal(a.technical.longPassing, b.technical.longPassing);
                Assert.Equal(a.technical.setPieces, b.technical.setPieces);

                Assert.Equal(a.physical.speed, b.physical.speed);
                Assert.Equal(a.physical.acceleration, b.physical.acceleration);
                Assert.Equal(a.physical.stamina, b.physical.stamina);
                Assert.Equal(a.physical.strength, b.physical.strength);
                Assert.Equal(a.physical.jumping, b.physical.jumping);
                Assert.Equal(a.physical.agility, b.physical.agility);
                Assert.Equal(a.physical.balance, b.physical.balance);

                Assert.Equal(a.mental.vision, b.mental.vision);
                Assert.Equal(a.mental.composure, b.mental.composure);
                Assert.Equal(a.mental.leadership, b.mental.leadership);
                Assert.Equal(a.mental.courage, b.mental.courage);
                Assert.Equal(a.mental.workRate, b.mental.workRate);
                Assert.Equal(a.mental.concentration, b.mental.concentration);
                Assert.Equal(a.mental.decisions, b.mental.decisions);
                Assert.Equal(a.mental.positioning, b.mental.positioning);
                Assert.Equal(a.mental.aggression, b.mental.aggression);
                Assert.Equal(a.mental.teamwork, b.mental.teamwork);
                Assert.Equal(a.mental.offTheBall, b.mental.offTheBall);

                Assert.Equal(a.goalkeeper.handling, b.goalkeeper.handling);
                Assert.Equal(a.goalkeeper.diving, b.goalkeeper.diving);
                Assert.Equal(a.goalkeeper.reflexes, b.goalkeeper.reflexes);
                Assert.Equal(a.goalkeeper.positioning, b.goalkeeper.positioning);
                Assert.Equal(a.goalkeeper.communication, b.goalkeeper.communication);
                Assert.Equal(a.goalkeeper.kicking, b.goalkeeper.kicking);
                Assert.Equal(a.goalkeeper.throwing, b.goalkeeper.throwing);
                Assert.Equal(a.goalkeeper.oneOnOne, b.goalkeeper.oneOnOne);
                Assert.Equal(a.goalkeeper.aerialAbility, b.goalkeeper.aerialAbility);
                Assert.Equal(a.goalkeeper.commandArea, b.goalkeeper.commandArea);

                Assert.Equal(a.hidden.consistency, b.hidden.consistency);
                Assert.Equal(a.hidden.importantMatches, b.hidden.importantMatches);
                Assert.Equal(a.hidden.pressureHandling, b.hidden.pressureHandling);
                Assert.Equal(a.hidden.injuryProneness, b.hidden.injuryProneness);
                Assert.Equal(a.hidden.recoveryRate, b.hidden.recoveryRate);
                Assert.Equal(a.hidden.professionalism, b.hidden.professionalism);
                Assert.Equal(a.hidden.ambition, b.hidden.ambition);
                Assert.Equal(a.hidden.loyalty, b.hidden.loyalty);
                Assert.Equal(a.hidden.temperament, b.hidden.temperament);
                Assert.Equal(a.hidden.sportsmanship, b.hidden.sportsmanship);
                Assert.Equal(a.hidden.dirtiness, b.hidden.dirtiness);
                Assert.Equal(a.hidden.adaptability, b.hidden.adaptability);
                Assert.Equal(a.hidden.versatility, b.hidden.versatility);
                Assert.Equal(a.hidden.controversy, b.hidden.controversy);
                Assert.Equal(a.hidden.influence, b.hidden.influence);

                // 전력이 같아야 라인업과 매치 결과가 세이브 전후로 같다
                Assert.Equal(a.AbilityScore, b.AbilityScore, 4);
            }
        }

        [Fact]
        public void 지난_경기_상세는_저장하지_않는다()
        {
            // 일정과 순위표는 점수만 쓰고, 타임라인은 방금 치른 경기에만 보여 준다.
            // 3,040경기분 이벤트와 상세 기록을 담으면 세이브가 2MB 가까이 불어난다.
            var w = Make(4455);
            var club = w.Clubs.Values.First(c => c.reputation == 3);
            ManagerCareer.TakeCharge(w, club.id, "테스트");
            for (int i = 0; i < 6; i++) GameEngine.PlayNextRound(w);

            var league = w.MyLeague!;
            var played = league.currentSeason.fixtures.Where(f => f.isPlayed).ToList();
            Assert.NotEmpty(played);
            Assert.Contains(played, f => f.result.events.Count > 0);     // 저장 전에는 들어 있다

            var back = SaveGame.Deserialize(SaveGame.Serialize(w, "슬림")).world;
            var backLeague = back.MyLeague!;

            // 점수와 순위는 그대로여야 한다
            Assert.Equal(league.currentSeason.standings.Select(s => s.points),
                         backLeague.currentSeason.standings.Select(s => s.points));
            foreach (var f in played)
            {
                var b = backLeague.currentSeason.fixtures.First(x => x.id == f.id);
                Assert.Equal(f.result.homeGoals, b.result.homeGoals);
                Assert.Equal(f.result.awayGoals, b.result.awayGoals);
                Assert.Empty(b.result.events);                           // 상세는 버린다
            }
        }

        [Fact]
        public void 세이브는_브라우저_저장소에_들어가는_크기다()
        {
            var w = Make();
            var (raw, packed) = SaveGame.Measure(w);
            Assert.True(packed < 2 * 1024 * 1024,
                $"세이브 {packed / 1024}KB — localStorage 한도에 비해 크다");
            Assert.True(packed < raw / 4, "압축이 듣지 않는다");
        }

        // ── 이사회 ────────────────────────────────────────────────────────

        [Fact]
        public void 감독_취임시_시즌_목표가_주어진다()
        {
            var w = Make();
            var club = w.Clubs.Values.First(c => c.reputation == 5);
            ManagerCareer.TakeCharge(w, club.id, "테스트");

            Assert.NotEmpty(w.Objectives);
            Assert.Contains(w.Objectives, o => o.type == BoardObjectiveType.LeagueFinish);
            Assert.Equal(club.id, w.ManagedClubId);
            Assert.NotEmpty(w.Inbox);
        }

        [Fact]
        public void 목표로_주는_대회는_실제로_열린다()
        {
            // 목표와 구현이 어긋나면 감독이 손쓸 수 없는 이유로 경질된다.
            // 열리지 않는 대회를 목표로 주면 시즌 말에 무조건 실패로 처리된다.
            foreach (int stars in new[] { 5, 4, 3, 2, 1 })
            {
                var w = Make(90210 + stars);
                var club = w.Clubs.Values.First(c =>
                    c.reputation == stars && w.TierOf(c.id) == LeagueTier.First);
                ManagerCareer.TakeCharge(w, club.id, "테스트");

                Assert.NotEmpty(w.Objectives);

                foreach (var o in w.Objectives)
                {
                    switch (o.type)
                    {
                        case BoardObjectiveType.WinDomesticCup:
                            Assert.NotNull(CupCompetition.UserCup(w));
                            Assert.Contains(club.id, CupCompetition.UserCup(w)!.entrants);
                            break;

                        case BoardObjectiveType.WinContinentalCup:
                        case BoardObjectiveType.ReachContinentalKnockout:
                            // 대회는 열려야 하고, 목표를 받은 구단은 출전권이 있어야 한다.
                            var cc = CupCompetition.ContinentalCup(w);
                            Assert.NotNull(cc);
                            Assert.Contains(club.id, cc!.entrants);
                            break;
                    }
                }
            }
        }

        [Fact]
        public void 강팀을_맡아_우승해도_경질되지_않는다()
        {
            var w = Make(777000);
            var league = w.League(w.Nations[0].LeagueId)!;
            var best = league.clubIds.OrderByDescending(id => w.Strength(id)).First();
            ManagerCareer.TakeCharge(w, best, "테스트");

            int guard = 0;
            while (!w.SeasonFinished && guard++ < 60)
                if (GameEngine.PlayNextRound(w) == null) break;

            int pos = Standings.PositionOf(league, best);
            if (pos == 1)
                Assert.False(w.Sacked,
                    $"우승했는데 경질됐다 — 신뢰도 {w.Manager.boardConfidence}, " +
                    string.Join(" / ", w.Objectives.Select(o => $"{o.description}:{o.result}")));
        }
    }
}
