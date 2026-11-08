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
                var spec = NameForge.ByCode(nation.Code);
                var playoffLog = new List<string>();

                for (int t = 0; t + 1 < nation.LeagueIds.Count; t++)
                {
                    var top = w.League(nation.LeagueIds[t]);
                    var below = w.League(nation.LeagueIds[t + 1]);
                    if (top?.currentSeason == null || below?.currentSeason == null) continue;

                    int spots = Math.Min(top.relegationSpots, below.promotionSpots);
                    if (spots <= 0) continue;

                    // 순위만으로 올라가는 리그가 오히려 소수다 — 마지막 한 자리를
                    // 플레이오프로 가리는 나라가 많다(docs/리그-제도.md ④).
                    var rng = new Random(SeedMix.Of(w.Seed, top.id * 131 + w.SeasonYear, 60611));
                    var (goingDown, goingUp) =
                        PromotionPlayoff.Resolve(w, spec, t + 1, top, below, spots, rng, playoffLog);

                    if (goingDown.Count == 0 || goingUp.Count == 0) continue;

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

                // 플레이오프는 따로 알린다 — 순위만 보면 왜 그 팀이 올라갔는지 알 수 없다.
                if (w.ManagedClubId > 0 && playoffLog.Count > 0
                    && nation.LeagueIds.Contains(w.LeagueOf(w.ManagedClubId)?.id ?? -1))
                {
                    w.Post($"{nation.Country} 플레이오프",
                           string.Join("\n", playoffLog), InboxKind.News);
                }

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
        /// <summary>
        /// 아직 치르지 않은 경기를 모두 치른다.
        ///
        /// 리그마다 라운드 수가 다르다 — 내 리그가 38라운드면 46라운드짜리 2부는
        /// 여덟 라운드가 남은 채로 내 시즌이 끝난다. 그 상태로 승강을 처리하면
        /// <b>덜 치른 순위표</b>로 올리고 내리게 된다.
        /// </summary>
        public static void FinishRemainingFixtures(GameWorld w)
        {
            // 가장 긴 리그가 58라운드(29팀)이므로 넉넉히 잡는다.
            for (int guard = 0; guard < 80; guard++)
            {
                var next = GameEngine.NextWorldFixture(w);
                if (next == null) return;

                int matchday = next.matchday;

                foreach (var league in w.Leagues.Values)
                {
                    var season = league.currentSeason;
                    if (season == null) continue;

                    foreach (var f in season.fixtures
                                            .Where(f => f.matchday == matchday && !f.isPlayed)
                                            .ToList())
                        MatchDay.SimulateFixture(w, f);

                    season.currentMatchday = Math.Max(season.currentMatchday, matchday);
                    Standings.Recalculate(league);
                    LeagueSplit.ApplyIfDue(w, league);
                    TwoStage.ApplyIfDue(w, league);
                }
            }
        }

        public static void StartNextSeason(GameWorld w)
        {
            int nextYear = w.SeasonYear + 1;
            var rng = new Random(SeedMix.Of(w.Seed, nextYear, 20259));

            // 남은 경기를 먼저 치른다. 미완성 순위표로 올리고 내리면 안 된다.
            FinishRemainingFixtures(w);

            // 시상과 명예는 순위표·기록을 비우기 전에, 그리고 승강으로 리그가 바뀌기 전에 끝낸다.
            // 뒤로 밀면 우승팀이 다른 리그 소속으로 기록된다.
            SeasonAwards.RunAll(w, w.SeasonYear);

            // 챔피언 결정전은 시상 **뒤**다(docs/리그-제도.md ⑥).
            //
            // 일정에 없는 경기도 득점과 평점을 선수 기록에 남긴다. 시상보다 먼저 치르면
            // 득점왕이 플레이오프 골로 뒤집힌다 — 실제로는 정규리그만 센다. 승격
            // 플레이오프(④)도 같은 이유로 시상 뒤에 있다.
            //
            // 다만 명예(RecordSeasonTitles)보다는 앞서야 한다. 누가 우승인지 먼저 정해져야 한다.
            RunChampionPlayoffs(w);

            RecordSeasonTitles(w);

            ApplyPromotionAndRelegation(w);

            // 순위표를 비우기 전에 대륙 대회 출전권을 확정한다.
            var continentalEntrants = CupCompetition.QualifiersByRegion(w);

            foreach (var league in w.Leagues.Values)
            {
                var old = league.currentSeason;
                if (old != null)
                {
                    // ⑧ 의 구단 기록은 한 해 합산이다 — "2026시즌 3위" 가 두 줄이나
                    // 남으면 역대 성적을 읽을 수 없다.
                    foreach (var e in TwoStage.SeasonTable(league))
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
                // 홀수 팀이면 매 라운드 한 팀이 쉬므로 라운드가 하나 더 필요하다.
                // 직접 세면 29팀 리그가 56라운드로 적힌다(실제로는 58).
                int matchdays = WorldGenerator.Matchdays(league.clubIds.Count);

                league.currentSeason = new LeagueSeason
                {
                    year = nextYear,
                    startDate = start,
                    endDate = FixtureScheduler.SeasonEnd(nextYear, matchdays),
                    currentMatchday = 0,
                    totalMatchdays = matchdays,
                    fixtures = FixtureScheduler.Build(league.clubIds, start, w.NextFixtureId, rng),
                    standings = league.clubIds.Select(id => new StandingEntry { clubId = id }).ToList(),

                    // 한 해를 둘로 가르는 나라는 해마다 다시 정한다 — 승강으로 팀 수가
                    // 바뀌면 경계도 따라 움직인다(⑦).
                    stageMatchday = TwoStage.BoundaryFor(
                        NameForge.ByCode(w.Nations.FirstOrDefault(
                            n => n.LeagueIds.Contains(league.id))?.Code ?? ""), league),
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
        /// 우승팀을 토너먼트로 가리는 나라들의 챔피언 결정전 (docs/리그-제도.md ⑥).
        ///
        /// 미국·호주·멕시코·인도·인도네시아는 정규리그를 순위를 매기는 과정으로만 쓴다.
        /// 순위표가 완성된 뒤, 시상보다는 앞서 치러야 한다.
        /// </summary>
        static void RunChampionPlayoffs(GameWorld w)
        {
            foreach (var nation in w.Nations)
            {
                var log = new List<string>();

                foreach (int id in nation.LeagueIds)
                {
                    var league = w.League(id);
                    if (league?.currentSeason == null) continue;
                    ChampionPlayoff.Resolve(w, league, log);
                }

                // 순위만 보면 왜 저 팀이 우승인지 알 수 없다 — 대진을 그대로 붙여 준다.
                if (log.Count > 0 && w.ManagedClubId > 0
                    && nation.LeagueIds.Contains(w.LeagueOf(w.ManagedClubId)?.id ?? -1))
                {
                    w.Post($"{nation.Country} 챔피언 결정전",
                           string.Join("\n", log), InboxKind.News);
                }
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

                // 시즌이 넘어가면 순위표가 새것이 된다. 부를 가리지 않고 남겨 둔다 —
                // 2·3부 우승도 화면에 적을 거리다.
                league.lastChampionClubId = ChampionPlayoff.ChampionOf(league.currentSeason);
                league.lastChampionYear = league.currentSeason.year;

                // 1부만 우승으로 센다. 2·3부 1위는 승격이지 우승이 아니다.
                if (league.tier != LeagueTier.First) continue;

                // 한 해에 우승이 둘 나오는 리그가 있다(⑧ 아페르투라·클라우수라).
                // 같은 구단이 둘 다 가져갔으면 두 번 새긴다 — 두 번 우승한 것이 맞다.
                foreach (int championId in TwoStage.ChampionsOf(league.currentSeason))
                {
                    var champ = table.FirstOrDefault(e => e.clubId == championId);

                    winners.Add((championId, SeasonAwards.Honour.League));
                    ContractBonuses.PayTitle(w, championId);   // 계약서에 적은 우승 보너스

                    if (championId != w.ManagedClubId) continue;

                    string how =
                        ChampionPlayoff.WonFromBehind(league.currentSeason) && champ != null
                            ? $"정규리그 {champ.position}위에서 챔피언 결정전을 뚫었습니다."
                        : champ != null ? $"승점 {champ.points}."
                        : "전반기 우승입니다.";

                    w.Post($"{league.name} 우승",
                           how + " 구단 역사에 새겨집니다.", InboxKind.Board);
                }
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
