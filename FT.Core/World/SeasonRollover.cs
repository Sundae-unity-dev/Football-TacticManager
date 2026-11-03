using System;
using System.Collections.Generic;
using System.Linq;
using FootballTactician.Balance;
using FootballTactician.Data;
using FootballTactician.Simulation;

namespace FootballTactician.World
{
    /// <summary>
    /// 시즌을 넘긴다 — 승강 · 재계약 · 은퇴 · 보강 · 재정 · 일정 재생성.
    ///
    /// 한 해에 한 번 도는 길이지만, 세계의 거의 모든 모듈을 건드린다.
    /// 진행 엔진 안에 두면 "매주 도는 코드"와 "한 해에 한 번 도는 코드"가 한 파일에 섞여,
    /// 라운드를 고치려고 열었다가 승강 규칙을 읽게 된다.
    ///
    /// 순서가 뜻을 가진다. 성적으로 승강을 정하고(순위가 바뀌기 전에),
    /// 계약과 은퇴로 명단을 비우고, 그다음에 보강한다 — 뒤집으면 빈자리를 메운 뒤에
    /// 선수가 떠나 스쿼드가 모자란 채로 시즌이 시작된다.
    /// </summary>
    public static class SeasonRollover
    {
        /// <summary>
        /// 남는 돈을 시설과 아카데미에 쓴다.
        ///
        /// 이것이 없으면 구단이 매년 수입의 20% 남짓을 영원히 쌓아 몇 시즌 만에
        /// 자금이 연 수입의 두 배를 넘는다. 실제 구단은 남는 돈을 시설에 넣는다.
        /// </summary>
        public static void InvestSurplus(GameWorld w)
        {
            foreach (var club in w.Clubs.Values)
            {
                long revenue = Math.Max(1, club.finances.annualRevenue);

                // 연 수입의 1.2배를 넘게 쌓아 두면 투자한다.
                while (club.finances.balance > revenue * 1.2
                       && (club.trainingFacilitiesRating < 5 || club.academyRating < 5))
                {
                    long cost = (long)(revenue * BalanceConfig.FacilityUpgradeCost);
                    if (club.finances.balance - cost < revenue * 0.4) break;

                    // 뒤처진 쪽부터 올린다.
                    bool training = club.trainingFacilitiesRating <= club.academyRating
                                    && club.trainingFacilitiesRating < 5;
                    if (training) club.trainingFacilitiesRating++;
                    else if (club.academyRating < 5) club.academyRating++;
                    else break;

                    club.finances.balance -= cost;

                    if (club.id == w.ManagedClubId)
                        w.Post("시설 투자",
                               training
                                   ? $"훈련 시설을 {club.trainingFacilitiesRating}단계로 올렸습니다. ({cost / 1_000_000d:F1}M€)"
                                   : $"아카데미를 {club.academyRating}단계로 올렸습니다. ({cost / 1_000_000d:F1}M€)",
                               InboxKind.Board);
                }

                // 그래도 남으면 이사회가 가져간다. 무한정 쌓이지 않게 한다.
                long cap = (long)(revenue * 2.0);
                if (club.finances.balance > cap)
                    club.finances.balance = cap + (long)((club.finances.balance - cap) * 0.4);
            }
        }

        /// <summary>
        /// 이적 예산과 주급 예산을 다시 책정한다. 예산이 소진된 채 회복되지 않으면 시장이 멈추고,
        /// 수입만으로 잡으면 스쿼드가 강해졌을 때 아무도 더 나은 선수를 살 수 없어 역시 멈춘다.
        /// </summary>
        public static void RebudgetClub(GameWorld w, ClubData club)
        {
            // 경기장과 스폰서를 수입에 반영한다. 여기서 하지 않으면 증축해도
            // 승강격이 일어날 때까지 한 푼도 늘지 않는다.
            float rep = w.RepCoefOf(club.id);
            club.finances.annualRevenue = (long)(BalanceConfig.AnnualRevenue(rep) * 10000f
                                               * ClubBusiness.RevenueMultiplier(club));

            long fromRevenue = (long)(club.finances.annualRevenue * BalanceConfig.TransferBudgetRatio);
            long fromBalance = (long)(Math.Max(0, club.finances.balance) * BalanceConfig.BudgetFromBalance);
            long budget = Math.Min(fromRevenue + fromBalance, Math.Max(0, club.finances.balance));

            club.finances.transferBudget = (int)Math.Clamp(budget, 0, int.MaxValue);
            club.finances.weeklyWageBudget =
                (int)(club.finances.annualRevenue
                      * (BalanceConfig.WageWarnRatio + BalanceConfig.StaffWageAllowance) / 52)
                + club.boardWageBonus;   // 이사회가 얹어 준 몫은 재산정에도 남는다
        }

        /// <summary>
        /// 만료된 계약을 정리한다. 재계약하면 주급이 그 구단의 시장가로 돌아오고,
        /// 재계약하지 않으면 자유계약 선수가 된다.
        ///
        /// 이 과정이 없으면 주급이 오르기만 한다. 이적할 때마다 선수가 더 받고, 내려오는 길이 없어
        /// 리그 전체 급여 비중이 매 시즌 1%p 씩 올라 결국 아무도 영입하지 못한다.
        /// </summary>
        public static void ResolveExpiringContracts(GameWorld w, Random rng)
        {
            var leftUs = new List<string>();

            foreach (var club in w.Clubs.Values.ToList())
            {
                float rep = w.RepCoefOf(club.id);
                var ranked = w.Squad(club.id).OrderByDescending(GameWorld.Ca).ToList();

                for (int i = 0; i < ranked.Count; i++)
                {
                    var p = ranked[i];
                    if (p.contract == null || p.contract.expiryDate > w.Date) continue;

                    int market = (int)BalanceConfig.WeeklyWage(GameWorld.Ca(p), p.GetAge(w.Date), rep);
                    bool wanted = i < BalanceConfig.SquadTarget - 2;
                    bool affordable = (club.finances.currentWageSpend - p.contract.weeklyWage + market) * 52f
                                      <= club.finances.annualRevenue * BalanceConfig.WageWarnRatio;

                    // 플레이어 구단은 자동으로 재계약하지 않는다. 시즌 내내 붙잡을 기회를 줬는데
                    // 쓰지 않았다면 선수가 떠나는 것이 맞다. AI 가 대신 재계약해 주면
                    // 재계약 협상이라는 결정 자체가 의미를 잃는다.
                    bool auto = club.id != w.ManagedClubId;

                    if (auto && wanted && affordable && rng.Next(100) < 80)
                    {
                        // 재계약 — 주급이 시장가로 돌아온다
                        p.contract.weeklyWage = Math.Max(500, market);
                        p.contract.startDate = w.Date;
                        p.contract.expiryDate = new GameDate(w.Date.year + 2 + rng.Next(3), 6, 30);
                        p.contract.status = ContractStatus.Active;
                    }
                    else
                    {
                        if (club.id == w.ManagedClubId) leftUs.Add(p.FullName);
                        Release(w, club, p);
                    }
                }

                TransferMarket.RecalcWages(w, club);
            }

            w.ContractWarnings.Clear();

            if (w.ManagedClubId > 0)
            {
                if (leftUs.Count > 0)
                    w.Post("계약 만료로 떠남",
                           $"{string.Join(", ", leftUs)} 의 계약이 끝나 구단을 떠났습니다.",
                           InboxKind.Transfer);

                int freed = w.FreeAgents().Count();
                if (freed > 0)
                    w.Post("자유계약 선수",
                           $"계약이 끝난 선수 {freed}명이 시장에 나왔습니다. 이적료 없이 영입할 수 있습니다.",
                           InboxKind.Transfer);
            }
        }

        /// <summary>
        /// 인원이 모자란 구단을 채운다. 자유계약 선수를 먼저 보고, 모자라면 유스에서 올린다.
        ///
        /// 무작위 이적 창구에만 맡기면 방출이 영입보다 빨라 스쿼드가 몇 시즌 만에 무너진다.
        /// 인원 유지는 확정적으로 보장해야 하는 불변 조건이다.
        /// </summary>
        public static void TopUpSquads(GameWorld w, Random rng)
        {
            foreach (var club in w.Clubs.Values)
            {
                // 내 구단은 **경기를 치를 수 있는 최소 인원까지만** 채운다.
                //
                // 가득 채워 주면 AI 가 내 스쿼드를 대신 짜 준다 — 내가 재계약하지 않기로
                // 한 선수를 자유계약으로 도로 주워 오는 일까지 벌어졌다. 그러면 재계약을
                // 하지 않겠다는 결정 자체가 뜻을 잃는다(ResolveExpiringContracts 가 플레이어
                // 구단을 자동 재계약하지 않는 것과 같은 이유다).
                //
                // 그렇다고 비워 둘 수도 없다. 열한 명을 못 세우면 경기가 서지 않는다.
                int target = club.id == w.ManagedClubId
                    ? BalanceConfig.MinPlayableSquad
                    : BalanceConfig.SquadTarget;

                if (club.playerIds.Count >= target) continue;

                var nation = NationNamesFor(w, club.id);
                float rep = w.RepCoefOf(club.id);

                while (club.playerIds.Count < target)
                {
                    var need = MissingPosition(w, club);

                    // 1) 자유계약 — 감당할 수 있는 선수 중 가장 나은 쪽
                    var free = w.FreeAgents()
                        .Where(p => (p.positionFlags & need) != 0)
                        .OrderByDescending(p => AiRecruiting.Score(
                            w, club, p, need, AiRecruiting.SkillOf(club)))
                        .FirstOrDefault(p => TransferMarket.BuyerVerdict(
                            w, club, 0, TransferMarket.RequiredWage(w, p, club))
                            == TransferMarket.Verdict.Accepted);

                    if (free != null)
                    {
                        TransferMarket.SignFreeAgent(w, club, free,
                            TransferMarket.RequiredWage(w, free, club), 2 + rng.Next(3));
                        continue;
                    }

                    // 2) 급히 사람을 구한다. 구단이 선수 없이 남는 일은 없어야 한다.
                    //
                    // 열여섯 살을 만들어 메우면 안 된다. 은퇴로 매년 수백 명이 빠지는데
                    // 그 자리를 전부 10대로 채우면 세계 평균 나이가 23세 아래로 내려가
                    // 유소년 리그가 된다 — 급히 명단을 채우는 구단은 저니맨을 데려온다.
                    int fillerAge = BalanceConfig.FillerAgeMin
                                  + rng.Next(BalanceConfig.FillerAgeMax - BalanceConfig.FillerAgeMin + 1);
                    var youth = WorldGenerator.CreateYouthPlayer(
                        rng, w.NextPlayerId++, club, nation, rep, need, w.Date, fillerAge);
                    w.Players[youth.id] = youth;
                    club.playerIds.Add(youth.id);
                    CapToWageRoom(w, club, youth);
                }

                TransferMarket.RecalcWages(w, club);
                WorldGenerator.ApplyBestEleven(club, w);
            }
        }

        /// <summary>
        /// 급히 구한 사람은 구단이 낼 수 있는 돈만 받는다.
        ///
        /// 자유계약으로 데려오는 길은 급여 여력을 보지만(BuyerVerdict), 아무도 못 구해
        /// <b>사람을 만들어 채우는 길은 한 푼도 보지 않았다</b>. 한도에 걸린 구단일수록
        /// 자유계약을 사지 못해 이 길로만 명단을 채우게 되고, 그래서 급여 비중이
        /// 제한 없이 올라갔다 — 10시즌 뒤 3부 평균 75.6%, 최고 114.9% 였다.
        ///
        /// 명단은 반드시 채워야 하므로 거절할 수는 없다. 대신 <b>받는 돈을 여력에
        /// 맞춘다</b> — 돈이 없는 구단은 최저 주급짜리 저니맨을 쓴다. 현실에서도 그렇다.
        /// </summary>
        static void CapToWageRoom(GameWorld w, ClubData club, PlayerData p)
        {
            if (p.contract == null || club.finances.annualRevenue <= 0) return;

            long limit = (long)(club.finances.annualRevenue * BalanceConfig.WageWarnRatio / 52);
            long others = ClubWages.Of(w, club) - p.contract.weeklyWage;
            long room = limit - others;

            p.contract.weeklyWage = (int)Math.Clamp(room, 500, p.contract.weeklyWage);
        }

        static NationNames NationNamesFor(GameWorld w, int clubId)
        {
            var nation = w.NationOf(clubId);
            return nation == null ? NameForge.All[0] : NameForge.ByCode(nation.Code);
        }

        static readonly Position[] NeededSlots =
        {
            Position.GK, Position.CB, Position.CB, Position.LB, Position.RB,
            Position.CDM, Position.CM, Position.CAM,
            Position.LW, Position.RW, Position.ST,
        };

        /// <summary>가장 인원이 얇은 자리.</summary>
        static Position MissingPosition(GameWorld w, ClubData club)
        {
            Position worst = Position.CM;
            int fewest = int.MaxValue;

            foreach (var pos in NeededSlots.Distinct())
            {
                int have = w.Squad(club.id).Count(p => (p.positionFlags & pos) != 0);
                int want = pos == Position.CB ? 4 : pos == Position.GK ? 3 : 2;
                int gap = want - have;
                if (gap > 0 && have < fewest) { fewest = have; worst = pos; }
            }
            return worst;
        }

        /// <summary>선수를 방출한다. 소속이 없어지고 이적료 없이 데려갈 수 있다.</summary>
        public static void Release(GameWorld w, ClubData club, PlayerData p)
        {
            // 빌려 온 선수는 방출할 수 없다. 내보내면 원소속 구단의 선수가 사라진다.
            if (w.Loans.ContainsKey(p.id) && w.Loans[p.id].hostClubId == club.id) return;

            club.playerIds.Remove(p.id);
            p.clubId = 0;
            TransferRequests.Clear(w, p.id);
            p.transferStatus = TransferStatus.ListedForTransfer;
            if (p.contract != null) p.contract.status = ContractStatus.Expired;

            var tac = club.savedTactics.FirstOrDefault(t => t.id == club.activeTacticsId);
            if (tac != null && tac.slots.Any(s => s.assignedPlayerId == p.id))
                WorldGenerator.ApplyBestEleven(club, w);
        }

        /// <summary>
        /// 1부 하위 N 팀과 2부 상위 N 팀이 자리를 바꾼다.
        /// 리그가 바뀌면 수입과 선수 가치도 등급에 맞춰 다시 매긴다.
        /// </summary>
        public static void ApplyPromotionAndRelegation(GameWorld w)
        {
            foreach (var nation in w.Nations)
            {
                // 인접한 단계마다 따로 처리한다. 1·2부만 보면 3부는 영영 갇힌다.
                //
                // 먼저 내려갈 팀과 올라갈 팀을 전부 추려 둔 뒤에 옮긴다. 한 쌍씩 옮기면
                // 2부에서 1부로 올라간 팀이 곧바로 3부와의 처리에 끼어든다.
                var moves = new List<(int clubId, LeagueData from, LeagueData to, bool down)>();

                for (int t = 0; t + 1 < nation.LeagueIds.Count; t++)
                {
                    var top = w.League(nation.LeagueIds[t]);
                    var below = w.League(nation.LeagueIds[t + 1]);
                    if (top?.currentSeason == null || below?.currentSeason == null) continue;

                    int spots = Math.Min(top.relegationSpots, below.promotionSpots);
                    if (spots <= 0) continue;

                    var goingDown = top.currentSeason.standings
                        .OrderByDescending(e => e.position).Take(spots).Select(e => e.clubId).ToList();
                    var goingUp = below.currentSeason.standings
                        .OrderBy(e => e.position).Take(spots).Select(e => e.clubId).ToList();

                    if (goingDown.Count < spots || goingUp.Count < spots) continue;

                    foreach (int id in goingDown) moves.Add((id, top, below, true));
                    foreach (int id in goingUp) moves.Add((id, below, top, false));

                    if (w.ManagedClubId > 0)
                    {
                        if (goingDown.Contains(w.ManagedClubId))
                            w.Post("강등", $"{w.MyClub?.name} 이 {below.name} 로 내려갑니다.", InboxKind.Board);
                        else if (goingUp.Contains(w.ManagedClubId))
                            w.Post("승격", $"{w.MyClub?.name} 이 {top.name} 로 올라갑니다.", InboxKind.Board);
                    }
                }

                foreach (var (id, from, to, down) in moves) Move(w, id, from, to, down);

                // "올라가면 올려 준다"고 적은 계약은 여기서 오른다.
                foreach (var (id, _, _, down) in moves)
                    if (!down) ContractBonuses.ApplyPromotionRaises(w, id);

                // 내 나라의 승강은 단계를 가리지 않고 알린다 — 3부 승강도 내 다음 상대를 바꾼다.
                if (w.ManagedClubId > 0 && moves.Count > 0
                    && nation.LeagueIds.Contains(w.LeagueOf(w.ManagedClubId)?.id ?? -1))
                {
                    string Names(bool down) => string.Join(", ", moves
                        .Where(m => m.down == down)
                        .Select(m => $"{w.Club(m.clubId)?.name}({m.to.shortName})"));

                    w.Post($"{nation.Country} 승강 결과",
                           $"승격 — {Names(false)} / 강등 — {Names(true)}", InboxKind.News);
                }
            }
        }

        static void Move(GameWorld w, int clubId, LeagueData from, LeagueData to, bool down)
        {
            var club = w.Club(clubId);
            if (club == null) return;

            from.clubIds.Remove(clubId);
            if (!to.clubIds.Contains(clubId)) to.clubIds.Add(clubId);
            club.leagueId = to.id.ToString();

            // 별점도 한 칸 움직인다. 강등되면 자원이 줄고, 승격하면 늘어난다.
            club.reputation = Math.Clamp(club.reputation + (down ? -1 : 1), 1, 5);

            float rep = GameWorld.RepCoef(club.reputation) * GameWorld.TierFactor(to.tier);
            long oldRevenue = Math.Max(1, club.finances.annualRevenue);
            club.finances.annualRevenue = (long)(BalanceConfig.AnnualRevenue(rep) * 10000f
                                              * ClubBusiness.RevenueMultiplier(club));

            // 주급도 함께 움직인다. 그대로 두면 강등 구단은 급여 비중이 치솟아 파산하고,
            // 승격 구단은 비중이 바닥나 몇 시즌 동안 전력이 리그 수준을 따라가지 못한다.
            // 계약 연동 조항(승강격 시 주급 변동)이 실제로 쓰이는 자리다.
            float wageShift = (float)Math.Pow(
                club.finances.annualRevenue / (double)oldRevenue, 0.55);
            wageShift = Math.Clamp(wageShift, 0.55f, 1.8f);

            foreach (var p in w.Squad(clubId))
            {
                if (p.contract != null)
                    p.contract.weeklyWage = Math.Max(500, (int)(p.contract.weeklyWage * wageShift));
                p.marketValue = (int)(BalanceConfig.Value(
                    GameWorld.Ca(p), p.GetAge(w.Date), rep) * 10000f);
            }

            TransferMarket.RecalcWages(w, club);
            RebudgetClub(w, club);
        }

        /// <summary>
        /// 다음 시즌을 연다. 성적을 기록하고 일정을 새로 짜며 시즌 통계를 비운다.
        /// 승강은 아직 없다 — 하부 리그가 생기면 여기에 붙인다.
        /// </summary>
        public static void StartNextSeason(GameWorld w)
        {
            int nextYear = w.SeasonYear + 1;
            var rng = new Random(SeedMix.Of(w.Seed, nextYear, 20259));

            // 시상과 명예는 순위표·기록을 비우기 전에, 그리고 승강으로 리그가 바뀌기 전에 끝낸다.
            // 뒤로 밀면 우승팀이 다른 리그 소속으로 기록된다.
            SeasonAwards.RunAll(w, w.SeasonYear);
            RecordSeasonTitles(w);

            ApplyPromotionAndRelegation(w);

            // 순위표를 비우기 전에 대륙 대회 출전권을 확정한다.
            var continentalEntrants = CupCompetition.QualifiersByRegion(w);

            foreach (var league in w.Leagues.Values)
            {
                var old = league.currentSeason;
                if (old != null)
                {
                    foreach (var e in old.standings)
                    {
                        var club = w.Club(e.clubId);
                        club?.history?.recentSeasons?.Add(new SeasonRecord
                        {
                            season = old.year,
                            leagueId = league.id.ToString(),
                            finalPosition = e.position,
                            points = e.points,
                            goalsFor = e.goalsFor,
                            goalsAgainst = e.goalsAgainst,
                        });
                        if (club?.history?.recentSeasons?.Count > 10)
                            club.history.recentSeasons.RemoveAt(0);
                    }
                }

                var start = FixtureScheduler.SeasonStart(nextYear);
                int matchdays = (league.clubIds.Count - 1) * 2;

                league.currentSeason = new LeagueSeason
                {
                    year = nextYear,
                    startDate = start,
                    endDate = FixtureScheduler.SeasonEnd(nextYear, matchdays),
                    currentMatchday = 0,
                    totalMatchdays = matchdays,
                    fixtures = FixtureScheduler.Build(league.clubIds, start, w.NextFixtureId, rng),
                    standings = league.clubIds.Select(id => new StandingEntry { clubId = id }).ToList(),
                };
                w.NextFixtureId += league.currentSeason.fixtures.Count + 1;

                league.transferWindow = new TransferWindowData
                {
                    summerStart = new GameDate(nextYear, 7, 1),
                    summerEnd = new GameDate(nextYear, 8, 31),
                    winterStart = new GameDate(nextYear + 1, 1, 1),
                    winterEnd = new GameDate(nextYear + 1, 1, 31),
                };

                Standings.Recalculate(league);
            }

            // 선수 시즌 기록을 비우고 한 살 더 먹은 상태로 가치를 다시 매긴다.
            foreach (var p in w.Players.Values)
            {
                // 통산에 더한 뒤에 비운다. 순서가 뒤집히면 한 시즌이 통째로 사라진다.
                p.careerStats ??= new PlayerStats();
                p.careerStats.Add(p.seasonStats);

                p.seasonStats = new PlayerStats();
                p.recentRatings.Clear();
                var club = w.Club(p.clubId);
                if (club != null)
                    // 대표 경력은 몸값에 붙는다. 같은 실력이면 국가대표가 더 비싸다.
                    p.marketValue = (int)(BalanceConfig.Value(
                        GameWorld.Ca(p), p.GetAge(w.Date), w.RepCoefOf(club.id))
                        * NationalTeam.ValueBonus(p) * 10000f);
            }

            // 임대 선수를 먼저 돌려보낸다. 계약 정리와 인원 보충이 실제 명단을 보고 돌아야 한다.
            LoanMarket.ReturnAll(w);

            // 은퇴가 계약 정리보다 앞선다. 뒤에 두면 떠날 선수와 재계약을 맺고,
            // 인원 보충이 빈자리를 보지 못한다.
            Retirement.RunAll(w, new Random(SeedMix.Of(w.Seed, nextYear * 97, 4409)));
            ClubBusiness.RenewExpired(w, new Random(SeedMix.Of(w.Seed, nextYear * 131, 8821)));
            foreach (var c in w.Clubs.Values) ClubBusiness.DriftTowardExpected(c);
            Homegrown.AdvanceYear(w);

            ResolveExpiringContracts(w, rng);
            YouthAcademy.RunAll(w, rng);
            TopUpSquads(w, rng);
            InvestSurplus(w);
            foreach (var club in w.Clubs.Values) RebudgetClub(w, club);

            w.SeasonYear = nextYear;
            w.Date = new GameDate(nextYear, 7, 1);
            PressRoom.StartSeason(w);
            TransferRequests.StartSeason(w);
            Scouting.StartWeek(w);
            ReserveLeague.StartSeason(w);
            w.TransferListsReady = false;

            CupCompetition.BuildAll(w, rng, continentalEntrants);

            if (w.ManagedClubId > 0 && !w.Sacked)
            {
                w.Objectives = BoardObjectives.GenerateFor(w, w.ManagedClubId);

                w.Post($"{nextYear} 시즌 개막",
                       $"새 시즌 목표: {string.Join(" / ", w.Objectives.Select(o => o.description))}",
                       InboxKind.Board);
            }
        }

        /// <summary>
        /// 이번 시즌의 우승을 구단 기록에 새긴다.
        ///
        /// 지금까지 우승해도 아무 일이 없었다 — 명예는 세계를 만들 때 정해진 숫자 그대로였다.
        /// 승강으로 리그가 바뀌기 전에, 순위표를 비우기 전에 끝내야 한다.
        /// </summary>
        static void RecordSeasonTitles(GameWorld w)
        {
            var winners = new List<(int clubId, SeasonAwards.Honour kind)>();

            foreach (var league in w.Leagues.Values)
            {
                var table = league.currentSeason?.standings;
                if (table is not { Count: > 0 }) continue;

                // 1부만 우승으로 센다. 2·3부 1위는 승격이지 우승이 아니다.
                if (league.tier != LeagueTier.First) continue;

                var champ = table.OrderBy(e => e.position).First();
                winners.Add((champ.clubId, SeasonAwards.Honour.League));
                ContractBonuses.PayTitle(w, champ.clubId);   // 계약서에 적은 우승 보너스

                if (champ.clubId == w.ManagedClubId)
                    w.Post($"{league.name} 우승",
                           $"승점 {champ.points}. 구단 역사에 새겨집니다.", InboxKind.Board);
            }

            foreach (var cup in w.Cups.Values)
            {
                if (cup.winnerClubId <= 0) continue;

                var kind = cup.continental
                    ? SeasonAwards.Honour.Continental
                    : SeasonAwards.Honour.Cup;
                winners.Add((cup.winnerClubId, kind));

                if (cup.winnerClubId == w.ManagedClubId)
                    w.Post($"{cup.name} 우승",
                           "구단 역사에 새겨집니다.", InboxKind.Board);
            }

            SeasonAwards.RecordTitles(w, winners);
        }
    }
}
