using System;
using System.Collections.Generic;
using System.Linq;
using FootballTactician.Balance;
using FootballTactician.Data;
using FootballTactician.Diagnostics;

namespace FootballTactician.World
{
    /// <summary>
    /// 새 판의 세계를 만든다. 국가 → 리그 → 구단 → 스쿼드 → 일정 순서.
    /// 같은 시드는 같은 세계를 만든다.
    /// </summary>
    public static class WorldGenerator
    {
        public const int ClubsPerLeague = 20;
        public const int SquadSize = 25;

        /// <summary>성격 가짓수. 구단마다 리플렉션으로 다시 세고 있었다.</summary>
        static readonly int PersonalityCount = Enum.GetValues(typeof(Personality)).Length;

        /// <summary>승강 인원. 1부 하위 N 팀과 2부 상위 N 팀이 자리를 바꾼다.</summary>
        public const int PromotionSpots = 3;

        /// <summary>
        /// 기본 리그 단계 수. 나라마다 다르다 — 축구가 깊은 나라는 3부까지,
        /// 리그 하나만 있는 나라는 1부까지다(<see cref="NationNames.Tiers"/>).
        ///
        /// 나라 하나가 세계를 얼마나 무겁게 하는지는 이 값이 정한다 —
        /// 3부 나라는 구단 60개·선수 1,500명, 1부 나라는 20개·500명이다.
        /// </summary>
        public const int Tiers = 3;

        /// <summary>1부 리그 안의 평판 분포. 길이가 ClubsPerLeague 와 같아야 한다.</summary>
        static readonly int[] RepLadder =
        {
            5, 5, 4, 4, 4, 3, 3, 3, 3, 3,
            2, 2, 2, 2, 1, 1, 1, 1, 1, 1
        };

        /// <summary>2부 리그 평판 분포. 상위 몇 팀만 1부 하위권과 겨룰 만하다.</summary>
        static readonly int[] SecondTierRepLadder =
        {
            2, 2, 2, 1, 1, 1, 1, 1, 1, 1,
            1, 1, 1, 1, 1, 1, 1, 1, 1, 1
        };

        /// <summary>3부 리그 평판 분포. 여기서는 1성이 보통이고 2성이 강팀이다.</summary>
        static readonly int[] ThirdTierRepLadder =
        {
            2, 1, 1, 1, 1, 1, 1, 1, 1, 1,
            1, 1, 1, 1, 1, 1, 1, 1, 1, 1
        };

        /// <summary>스쿼드 포지션 구성 — 25인을 이 순서로 채운다.</summary>
        static readonly Position[] SquadShape =
        {
            Position.GK,
            Position.LB, Position.CB, Position.CB, Position.RB,
            Position.CDM, Position.CDM,
            Position.LW, Position.CAM, Position.RW,
            Position.ST,
            // 백업 14인
            Position.GK, Position.GK,
            Position.CB, Position.CB, Position.LB, Position.RB,
            Position.CDM, Position.CM, Position.CM, Position.CAM,
            Position.LW, Position.RW, Position.ST, Position.ST
        };

        /// <summary>
        /// 세계를 만든다. <paramref name="nations"/> 를 주면 그 나라들만 만든다 —
        /// 나라 수가 곧 세계의 크기다(나라 하나 = 리그 3개 · 구단 60개 · 선수 1,500명).
        /// 주지 않으면 <see cref="Nations.Default"/>(3부까지 갖춘 네 나라)다.
        /// 전부 넣고 싶으면 <see cref="NameForge.All"/> 를 건네면 된다.
        /// </summary>
        public static GameWorld Create(int seed, int startYear = 2026,
                                       IReadOnlyList<NationNames> nations = null)
        {
            var rng = new Random(seed);
            var w = new GameWorld
            {
                Seed = seed,
                SeasonYear = startYear,
                Date = new GameDate(startYear, 7, 1),
            };

            int nextClubId = 1;
            int nextPlayerId = 1;
            int nextLeagueId = 1;
            int nextFixtureId = 1;

            // 구단 이름은 **세계 전체에서** 하나뿐이어야 한다. 순위표와 이적 기록이
            // 같은 이름을 둘 보여 주면 어느 쪽인지 알 길이 없다.
            var usedClubNames = new HashSet<string>();

            foreach (var nation in nations ?? Nations.Default)
            {
                int tiers = Math.Clamp(nation.Tiers, 1, 3);

                // 도시를 섞어 시드마다 평판 서열이 달라지게 한다. 각 부가 도시를 나눠 쓴다.
                // 도시가 모자라면 돌려 쓴다 — 한 도시에 구단이 둘인 것은 축구에서 흔하다
                // (접두사가 달라 이름은 겹치지 않는다).
                int needed = ClubsPerLeague * tiers;
                var pool = nation.Cities.OrderBy(_ => rng.Next()).ToList();
                var cities = new List<string>(needed);
                while (cities.Count < needed)
                    cities.AddRange(pool.Take(Math.Min(pool.Count, needed - cities.Count)));

                var leagues = new List<LeagueData>();
                for (int t = 0; t < tiers; t++)
                {
                    using (SimProfile.Measure("리그 12개"))
                    leagues.Add(MakeLeague(w, rng, nation, (LeagueTier)(t + 1), nextLeagueId++,
                        cities.Skip(ClubsPerLeague * t).Take(ClubsPerLeague).ToList(), startYear,
                        usedClubNames,
                        ref nextClubId, ref nextPlayerId, ref nextFixtureId));
                }

                // 승강을 위아래로 이어 붙인다. 한 칸씩만 이어야 3부에서 1부로 건너뛰지 않는다.
                for (int t = 0; t + 1 < leagues.Count; t++)
                {
                    leagues[t].relegationLeagueId = leagues[t + 1].id;
                    leagues[t + 1].promotionLeagueId = leagues[t].id;
                }

                w.Nations.Add(new NationState
                {
                    Code = nation.Code,
                    Country = nation.Country,
                    LeagueId = leagues[0].id,
                    LeagueIds = leagues.Select(l => l.id).ToList(),
                });
            }

            w.NextClubId = nextClubId;
            w.NextPlayerId = nextPlayerId;
            w.NextFixtureId = nextFixtureId;

            using (SimProfile.Measure("스태프"))
                StaffForge.Populate(w, new Random(seed ^ 0x57AFF));

            // 급여가 수입을 넘지 않게 한 번 맞춘다. 스태프까지 더한 뒤라야 뜻이 있다.
            using (SimProfile.Measure("급여 맞추기"))
                ClubWages.FitToIncome(w, BalanceConfig.WageWarnRatio);

            // 스폰서도 처음부터 있어야 한다. 첫 시즌이 끝나야 생기면
            // 새 게임의 재정 화면에 "스폰서 없음" 이 떠 있다.
            using (SimProfile.Measure("스폰서"))
                ClubBusiness.RenewExpired(w, new Random(seed ^ 0x590A5));
            using (SimProfile.Measure("홈그로운"))
                Homegrown.SeedWorld(w, new Random(seed ^ 0x40C41));   // 용도별 난수 — 공용 rng 를 쓰면 컵 추첨이 밀린다
            using (SimProfile.Measure("컵 대진"))
                BuildCupsInitial(w, rng);
            using (SimProfile.Measure("AI 전술"))
                AiTactics.AssignAll(w, new Random(seed ^ 0x7AC));
            using (SimProfile.Measure("탈의실"))
                DressingRoom.UpdateAll(w);
            using (SimProfile.Measure("순위표"))
                Standings.Recalculate(w);
            return w;
        }

        /// <summary>첫 시즌 컵. 이후 시즌은 CupCompetition.BuildAll 이 맡는다.</summary>
        static void BuildCupsInitial(GameWorld w, Random rng)
        {
            foreach (var nation in w.Nations)
            {
                var cup = CupCompetition.Build(w, nation, w.SeasonYear, rng);
                if (cup != null) w.Cups[cup.id] = cup;
            }

            var continental = CupCompetition.BuildContinental(w, w.SeasonYear, rng);
            if (continental != null) w.Cups[continental.id] = continental;
        }

        /// <summary>리그 하나와 그 구단·스쿼드·일정을 만든다.</summary>
        static LeagueData MakeLeague(
            GameWorld w, Random rng, NationNames nation, LeagueTier tier, int leagueId,
            List<string> cities, int startYear, HashSet<string> usedClubNames,
            ref int nextClubId, ref int nextPlayerId, ref int nextFixtureId)
        {
            bool top = tier == LeagueTier.First;
            bool bottom = (int)tier >= Math.Clamp(nation.Tiers, 1, 3);

            var (name, shortName) = tier switch
            {
                LeagueTier.First => (nation.LeagueName, nation.LeagueShort),
                LeagueTier.Second => (nation.SecondLeagueName, nation.SecondLeagueShort),
                _ => (nation.ThirdLeagueName, nation.ThirdLeagueShort),
            };

            var league = new LeagueData
            {
                id = leagueId,
                name = name,
                shortName = shortName,
                regionName = nation.Country,
                tier = tier,
                type = CompetitionType.League,
                numberOfTeams = ClubsPerLeague,

                // 맨 위는 올라갈 곳이, 맨 아래는 내려갈 곳이 없다.
                promotionSpots = top ? 0 : PromotionSpots,
                relegationSpots = bottom ? 0 : PromotionSpots,
                playoffSpots = 0,
                clubIds = new List<int>(),
                rules = new LeagueRules(),
                matchRules = new MatchRules(),
            };

            var ladder = tier switch
            {
                LeagueTier.First => RepLadder,
                LeagueTier.Second => SecondTierRepLadder,
                _ => ThirdTierRepLadder,
            };

            // 약칭은 **같은 리그 안에서만** 겹치지 않으면 된다 — 리그마다 새로 센다.
            var usedShorts = new HashSet<string>();

            for (int i = 0; i < ClubsPerLeague; i++)
            {
                int stars = ladder[i];
                var club = MakeClub(rng, nextClubId++, league.id, nation, cities[i], i, stars, tier,
                                    usedClubNames, usedShorts);
                using (SimProfile.Measure("  ├ 스쿼드 25인"))
                    FillSquad(rng, club, nation, stars, w.Date, ref nextPlayerId, w, tier);
                w.Clubs[club.id] = club;
                league.clubIds.Add(club.id);
            }

            var start = FixtureScheduler.SeasonStart(startYear);
            List<FixtureData> fixtures;
            using (SimProfile.Measure("  └ 일정 380경기"))
                fixtures = FixtureScheduler.Build(league.clubIds, start, nextFixtureId, rng);
            nextFixtureId += fixtures.Count + 1;

            league.currentSeason = new LeagueSeason
            {
                year = startYear,
                startDate = start,
                endDate = FixtureScheduler.SeasonEnd(startYear, (ClubsPerLeague - 1) * 2),
                currentMatchday = 0,
                totalMatchdays = (ClubsPerLeague - 1) * 2,
                fixtures = fixtures,
                standings = league.clubIds.Select(id => new StandingEntry { clubId = id }).ToList(),
            };
            league.transferWindow = new TransferWindowData
            {
                summerStart = new GameDate(startYear, 7, 1),
                summerEnd = new GameDate(startYear, 8, 31),
                winterStart = new GameDate(startYear + 1, 1, 1),
                winterEnd = new GameDate(startYear + 1, 1, 31),
            };

            w.Leagues[league.id] = league;
            return league;
        }

        // ── 구단 ──────────────────────────────────────────────────────────

        static ClubData MakeClub(
            Random rng, int id, int leagueId, NationNames nation,
            string city, int index, int stars, LeagueTier tier,
            HashSet<string> usedNames, HashSet<string> usedShorts)
        {
            // 이미 쓴 이름이면 다른 조합을 고른다 — 도시를 돌려 쓰면 이름이 통째로 겹친다.
            var (name, shortName) = NameForge.ClubName(
                nation, city, index,
                n => usedNames.Contains(n), sn => usedShorts.Contains(sn));
            usedNames.Add(name);
            usedShorts.Add(shortName);
            float rep = GameWorld.RepCoef(stars) * GameWorld.TierFactor(tier) * nation.Strength;
            long revenue = (long)(BalanceConfig.AnnualRevenue(rep) * 10000f);   // 만 유로 → 유로

            var club = new ClubData
            {
                id = id,
                name = name,
                shortName = shortName,
                leagueId = leagueId.ToString(),
                reputation = stars,
                playerIds = new List<int>(),
                staffIds = new List<int>(),
                managerId = -1,
                affiliateClubIds = new List<int>(),
                academyRating = Math.Clamp(stars + rng.Next(-1, 2), 1, 5),
                trainingFacilitiesRating = Math.Clamp(stars + rng.Next(-1, 2), 1, 5),
                finances = new ClubFinances
                {
                    balance = (long)(revenue * 0.18),
                    transferBudget = (int)Math.Min(int.MaxValue, revenue * 0.22),
                    weeklyWageBudget = (int)(revenue * (0.55 + BalanceConfig.StaffWageAllowance) / 52),
                    currentWageSpend = 0,
                    annualRevenue = revenue,
                    annualExpenditure = (long)(revenue * 0.9),
                    status = stars >= 4 ? FinancialStatus.Excellent
                           : stars >= 2 ? FinancialStatus.Stable
                           : FinancialStatus.Tight,
                },
                stadium = new StadiumData
                {
                    name = NameForge.StadiumName(nation, rng, city),
                    capacity = 8000 + stars * stars * 2200 + rng.Next(4000),
                    currentCondition = 70 + rng.Next(30),
                    ticketPriceAverage = 15 + stars * 8,
                    atmosphereRating = 45f + stars * 9f + rng.Next(8),
                    averageAttendanceRate = 62f + stars * 6f + rng.Next(10),
                    fanBaseSize = stars,
                    homeAdvantageBonus = stars * 1.4f,
                },
                history = new ClubHistory
                {
                    founded = 1890 + rng.Next(70),
                    leagueTitles = stars >= 4 ? rng.Next(3, 18) : stars == 3 ? rng.Next(0, 4) : 0,
                    cupTitles = stars >= 3 ? rng.Next(0, 9) : rng.Next(0, 2),
                    continentalTitles = stars == 5 ? rng.Next(0, 3) : 0,
                    recentSeasons = new List<SeasonRecord>(),
                },
                colors = Kit(rng),
                // 값은 비워 둔다. 선수가 아직 없어 난수로 채우면 명단과 어긋난 분위기가 되고,
                // 첫 주에 DressingRoom.Update 가 덮어쓰므로 무의미하다.
                dressingRoom = new DressingRoomMorale
                {
                    clubId = id,
                    relationships = new List<PlayerRelationship>(),
                    influencerPlayerIds = new List<int>(),
                },
                savedTactics = new List<TacticsData>(),
                activeTacticsId = -1,
            };

            var tac = FormationDatabase.CreateTactics("4-2-3-1", "기본 전술");
            Simulation.RoleDefaults.Fill(tac);
            tac.id = 1;
            club.savedTactics.Add(tac);
            club.activeTacticsId = 1;

            return club;
        }

        static readonly string[][] KitPalette =
        {
            new[] { "#3DDCA3", "#0F1A2E" }, new[] { "#E4573D", "#1A1A1A" },
            new[] { "#2F6FE4", "#FFFFFF" }, new[] { "#F2A33C", "#23303F" },
            new[] { "#8E5BD6", "#F3F4F6" }, new[] { "#D93A63", "#12202E" },
            new[] { "#19B5C9", "#0B1522" }, new[] { "#C8CE2E", "#1E2A1A" },
            new[] { "#FFFFFF", "#162B4D" }, new[] { "#6D7B8D", "#101820" },
        };

        static ClubColors Kit(Random rng)
        {
            var pair = KitPalette[rng.Next(KitPalette.Length)];
            return new ClubColors { primaryHex = pair[0], secondaryHex = pair[1] };
        }

        // ── 스쿼드 ────────────────────────────────────────────────────────

        static void FillSquad(
            Random rng, ClubData club, NationNames nation, int stars,
            GameDate today, ref int nextPlayerId, GameWorld w, LeagueTier tier)
        {
            float rep = GameWorld.RepCoef(stars) * GameWorld.TierFactor(tier) * nation.Strength;
            var naming = new PlayerFactory.Naming
            {
                First = nation.FirstNames,
                Last = nation.LastNames,
                Nationality = nation.Nationality,
            };
            var used = new HashSet<string>();

            for (int i = 0; i < SquadSize; i++)
            {
                var pos = SquadShape[i];
                bool starter = i < 11;

                // 주전은 전성기 나이대로, 백업은 유망주와 노장을 섞는다.
                // 주전에 18세가 들어가면 실현율이 낮아 구단 전력이 평판과 어긋난다.
                int age = starter
                    ? 23 + rng.Next(8)
                    : (rng.Next(2) == 0 ? 17 + rng.Next(6) : 29 + rng.Next(6));

                var p = PlayerFactory.Create(rng, nextPlayerId++, club.id, rep, pos, age, starter, today, naming);
                MakeNameUnique(p, used, naming, rng);
                p.personality = (Personality)rng.Next(PersonalityCount);

                // 몸값과 주급은 같은 가치에서 나온다. 주급 식이 가치 × 10000 × 계수이므로
                // 가치를 한 번만 구해 둘에 쓴다 — 4.5제곱이 든 계산을 선수마다 두 번 하고 있었다.
                float value = BalanceConfig.Value(GameWorld.Ca(p), age, rep) * 10000f;
                p.marketValue = (int)value;
                p.contract = new ContractData
                {
                    clubId = club.id,
                    weeklyWage = (int)(value * BalanceConfig.WageCoef),
                    startDate = new GameDate(today.year - rng.Next(1, 4), 7, 1),
                    expiryDate = new GameDate(today.year + 1 + rng.Next(4), 6, 30),
                    status = ContractStatus.Active,
                };
                p.seasonStats = new PlayerStats();
                club.playerIds.Add(p.id);
                w.Players[p.id] = p;
            }

            club.finances.currentWageSpend = club.playerIds
                .Sum(id => w.Players[id].contract?.weeklyWage ?? 0);

            // 주장은 여기서 뽑지 않는다. 생성 끝에서 DressingRoom.UpdateAll 이 영향력으로
            // 다시 뽑아 덮어쓰므로, 리더십+기량으로 한 번 뽑던 것은 쓰이지 않는 값이었다 —
            // 주장을 정하는 규칙은 탈의실 한 곳에만 있어야 한다.
            ApplyBestEleven(club, w);
        }

        /// <summary>
        /// 한 구단 안에서 같은 이름이 두 번 나오지 않게 한다.
        /// 이름 풀이 유한해 25인을 뽑으면 동명이인이 생기고, 명단에서 누가 누군지 구분되지 않는다.
        /// </summary>
        static void MakeNameUnique(PlayerData p, HashSet<string> used, PlayerFactory.Naming naming, Random rng)
        {
            for (int attempt = 0; attempt < 40 && !used.Add(p.FullName); attempt++)
            {
                p.lastName = naming.Last[rng.Next(naming.Last.Length)];
                if (attempt >= 20) p.firstName = naming.First[rng.Next(naming.First.Length)];
            }
            used.Add(p.FullName);
        }

        /// <summary>
        /// 유스 선수 한 명. 어리고 싸며 잠재력은 아카데미 등급을 따른다.
        /// 구단이 선수 없이 남는 일이 없도록 하는 마지막 수단이기도 하다.
        /// </summary>
        public static PlayerData CreateYouthPlayer(
            Random rng, int id, ClubData club, NationNames nation,
            float reputation, Position position, GameDate today, int? ageOverride = null)
        {
            var naming = new PlayerFactory.Naming
            {
                First = nation.FirstNames,
                Last = nation.LastNames,
                Nationality = nation.Nationality,
            };

            int age = ageOverride ?? 17 + rng.Next(3);
            // 아카데미가 좋을수록 잠재력이 높다. 평판 계수를 살짝 끌어올려 반영한다.
            float academy = 0.9f + club.academyRating * 0.05f;

            var p = PlayerFactory.Create(
                rng, id, club.id, reputation * academy, position, age, starter: false, today, naming);

            p.homegrown = new HomegrownStatus();
            p.marketValue = (int)(BalanceConfig.Value(
                BalanceConfig.PowerToCa(p.AbilityScore), age, reputation) * 10000f);
            p.contract = new ContractData
            {
                clubId = club.id,
                weeklyWage = Math.Max(500, (int)BalanceConfig.WeeklyWage(
                    BalanceConfig.PowerToCa(p.AbilityScore), age, reputation)),
                startDate = today,
                expiryDate = new GameDate(today.year + 3, 6, 30),
                status = ContractStatus.Active,
            };
            p.seasonStats = new PlayerStats();
            return p;
        }

        /// <summary>활성 전술의 11개 슬롯에 포지션 적합도가 높은 선수를 채운다.</summary>
        public static void ApplyBestEleven(ClubData club, GameWorld w)
        {
            var tac = club.savedTactics.FirstOrDefault(t => t.id == club.activeTacticsId);
            if (tac == null) return;
            LineupBuilder.Assign(tac, club.playerIds.Select(w.Player));
        }
    }
}
