using System;
using System.Collections.Generic;
using System.Linq;
using FootballTactician.Diagnostics;
using FootballTactician.Balance;
using FootballTactician.Data;

namespace FootballTactician.World
{
    /// <summary>
    /// 이적 시장. 호가 산정, 구단과 선수의 수락 판단, 체결, AI 구단끼리의 이적을 모두 여기 둔다.
    ///
    /// 돈이 한쪽으로 쏠리지 않는 것이 가장 중요하다. 파는 쪽과 사는 쪽의 판단 기준이
    /// 어긋나면 상위 구단이 리그 전체를 사들이거나, 반대로 아무 거래도 일어나지 않는다.
    /// </summary>
    public static class TransferMarket
    {
        /// <summary>측정용 거절 사유 집계. null 이면 아무것도 하지 않는다.</summary>
        public static Dictionary<string, int> Diagnostics;

        // ── 창구 ──────────────────────────────────────────────────────────

        public static bool IsWindowOpen(GameWorld w)
        {
            var league = w.MyLeague ?? w.Leagues.Values.FirstOrDefault();
            return league?.transferWindow?.IsWindowOpen(w.Date) ?? false;
        }

        public static string WindowLabel(GameWorld w)
        {
            var tw = (w.MyLeague ?? w.Leagues.Values.FirstOrDefault())?.transferWindow;
            if (tw == null) return "—";
            if (w.Date >= tw.summerStart && w.Date <= tw.summerEnd) return "여름 이적시장";
            if (w.Date >= tw.winterStart && w.Date <= tw.winterEnd) return "겨울 이적시장";
            return "닫힘";
        }

        // ── 가격 ──────────────────────────────────────────────────────────

        /// <summary>
        /// 구단이 부르는 값. 시장 가치에 스쿼드 내 비중과 계약 잔여 기간을 얹는다.
        /// 주전은 비싸게 부르고, 계약이 얼마 안 남은 선수는 싸게 내놓는다.
        /// </summary>
        /// <summary>
        /// 스쿼드 안에서 CA 내림차순 몇 번째인가(0부터). 명단에 없으면 -1.
        /// <c>OrderByDescending(Ca).ToList().FindIndex(...)</c> 와 같은 값을 내지만
        /// 정렬도 할당도 하지 않는다. 회귀 테스트가 두 방식을 견준다.
        /// </summary>
        public static int SquadRank(GameWorld w, PlayerData p)
        {
            float ca = GameWorld.Ca(p);
            int better = 0;
            bool found = false;

            foreach (var x in w.Squad(p.clubId))
            {
                if (x.id == p.id) { found = true; continue; }
                float xca = GameWorld.Ca(x);
                // 같은 값이면 명단 순서가 그대로 남는다(안정 정렬). p 앞의 동점자만 센다.
                if (xca > ca || (xca == ca && !found)) better++;
            }
            return found ? better : -1;
        }

        /// <summary>
        /// 호가가 예산 안에 드는지. 값을 끝까지 계산하지 않고 상·하한으로 먼저 가른다.
        /// <c>mult</c> 는 0.45~2.2 로 묶여 있고 바닥이 5만이므로 이 두 선은 정확하다 —
        /// 거르는 결과는 <c>AskingPrice(w, p) &lt;= budget</c> 와 언제나 같다.
        ///
        /// 이적 창구가 열린 주에 이 비교가 10만 번 가까이 돌고, 대부분은 한참 모자라거나
        /// 한참 남는 쪽이다. 그 둘을 먼저 끊으면 순위를 세는 일 자체가 사라진다.
        /// </summary>
        static bool AskingPriceWithin(GameWorld w, PlayerData p, long budget)
        {
            long floorPrice = (long)Math.Max(50_000f, p.marketValue * 0.45f);
            if (floorPrice > budget) return false;

            long ceilPrice = (long)Math.Max(50_000f, p.marketValue * 2.2f);
            if (ceilPrice <= budget) return true;

            return AskingPrice(w, p) <= budget;
        }

        public static long AskingPrice(GameWorld w, PlayerData p)
        {
            var club = w.Club(p.clubId);
            if (club == null) return p.marketValue;

            float mult = 1.0f;

            // 스쿼드 내 서열 — 상위 3명은 쉽게 내주지 않는다.
            // 정렬해서 찾으면 한 명 값을 매길 때마다 25명을 정렬하고 리스트를 새로 만든다.
            // 이적 창구가 열린 주에는 이 함수가 10만 번 넘게 불려 라운드 비용의 1/6 을 먹었다.
            // 세어서 같은 순위를 낸다 — 내림차순 안정 정렬에서 p 의 자리는
            // "더 잘하는 수" + "같은데 명단에서 먼저 오는 수" 다.
            int rank = SquadRank(w, p);
            if (rank >= 0 && rank < 3) mult += 0.55f;
            else if (rank < 8) mult += 0.25f;
            else if (rank >= 18) mult -= 0.15f;           // 남는 자원은 싸게

            // 계약 잔여 — 1년 이하는 협상력이 없다
            int monthsLeft = p.contract == null
                ? 24
                : GameDate.MonthsBetween(w.Date, p.contract.expiryDate);
            if (monthsLeft <= 6) mult -= 0.45f;
            else if (monthsLeft <= 12) mult -= 0.25f;
            else if (monthsLeft >= 48) mult += 0.15f;

            // 이적 목록에 올린 선수는 값을 낮춘다
            if (p.transferStatus == TransferStatus.ListedForTransfer) mult -= 0.25f;
            if (p.transferStatus == TransferStatus.WantAway) mult -= 0.15f;

            // 어린 선수는 잠재력 프리미엄
            int age = w.Age(p);
            float ca = GameWorld.Ca(p), pa = PlayerDevelopment.Pa(p);
            if (age <= 21 && pa > ca + 20f) mult += 0.30f;

            mult = Math.Clamp(mult, 0.45f, 2.2f);
            return (long)Math.Max(50_000, p.marketValue * mult);
        }

        /// <summary>선수가 기대하는 주급. 지금 받는 것과 시장 가치 기준 중 높은 쪽.</summary>
        public static int ExpectedWage(GameWorld w, PlayerData p, ClubData buyer)
            => ExpectedWage(w, p, w.RepCoefOf(buyer.id));

        /// <summary>
        /// 구단 평판 계수를 이미 구해 둔 경우. 한 구단의 선수를 줄줄이 볼 때 쓴다 —
        /// 계수는 구단마다 하나인데, 전에는 선수마다 리그를 찾아 다시 구했다.
        /// </summary>
        public static int ExpectedWage(GameWorld w, PlayerData p, float buyerRepCoef)
        {
            float byValue = BalanceConfig.WeeklyWage(
                GameWorld.Ca(p), w.Age(p), buyerRepCoef);
            int current = p.contract?.weeklyWage ?? 0;
            return (int)Math.Max(byValue, current * 1.15f);
        }

        // ── 판단 ──────────────────────────────────────────────────────────

        public enum Verdict { Accepted, FeeTooLow, SquadTooThin, NotForSale, NoBudget, WageTooLow, ClubNotAppealing, NoWageRoom, HomegrownShort }

        public sealed class OfferOutcome
        {
            public Verdict ClubVerdict;
            public Verdict PlayerVerdict;
            public bool Accepted => ClubVerdict == Verdict.Accepted && PlayerVerdict == Verdict.Accepted;
            public string Message = "";
        }

        /// <summary>파는 구단의 판단.</summary>
        public static Verdict SellerVerdict(GameWorld w, PlayerData p, long fee)
        {
            var seller = w.Club(p.clubId);
            if (seller == null) return Verdict.NotForSale;

            // 빌려 온 선수는 내 것이 아니다. 막지 않으면 임대받은 구단이 팔아 버리고,
            // 시즌 말 복귀가 원소속으로 되돌려 산 구단 명단에 남의 선수가 남는다.
            if (w.Loans.ContainsKey(p.id)) return Verdict.NotForSale;

            // 마지막 홈그로운을 팔아 치우면 다음 영입이 통째로 막힌다.
            if (!Homegrown.CanSell(w, p.clubId, p)) return Verdict.HomegrownShort;

            // 그 포지션에 대체 자원이 없으면 값을 떠나 팔지 않는다
            var pos = p.preferredPosition != Position.None ? p.preferredPosition : p.positionFlags;
            int cover = w.Squad(p.clubId).Count(x => x.id != p.id && (x.positionFlags & pos) != 0);
            if (cover < 1) return Verdict.SquadTooThin;
            if (w.Squad(p.clubId).Count() <= BalanceConfig.SquadMin) return Verdict.SquadTooThin;

            long ask = AskingPrice(w, p);

            // 본인이 나가고 싶어 하면 호가를 조금 깎아 준다
            if (p.transferStatus == TransferStatus.WantAway) ask = (long)(ask * 0.85);

            return fee >= ask ? Verdict.Accepted : Verdict.FeeTooLow;
        }

        /// <summary>
        /// 그 선수를 데려오는 데 실제로 필요한 최소 주급.
        /// 기대 주급만 보여 주면, 표시된 금액 그대로 제안해도 거절당해 감독이 이유를 알 수 없다.
        /// AI 와 화면이 같은 값을 쓴다.
        /// </summary>
        public static int RequiredWage(GameWorld w, PlayerData p, ClubData buyer)
        {
            int expected = ExpectedWage(w, p, buyer);
            return (int)Math.Ceiling(expected * AppealMultiplier(w, p, buyer));
        }

        /// <summary>평판이 낮은 구단으로 갈 때 얹어야 하는 배수.</summary>
        public static float AppealMultiplier(GameWorld w, PlayerData p, ClubData buyer)
        {
            var current = w.Club(p.clubId);
            int drop = (current?.reputation ?? 3) - buyer.reputation;

            // 등급이 내려가는 이적은 별점 차이와 별개로 더 꺼린다.
            var fromTier = (int)w.TierOf(p.clubId);
            var toTier = (int)w.TierOf(buyer.id);
            drop += Math.Max(0, toTier - fromTier);

            return drop switch
            {
                >= 3 => 1.80f,
                2 => 1.45f,
                1 => 1.12f,
                _ => 1.00f,
            };
        }

        /// <summary>선수 본인의 판단.</summary>
        public static Verdict PlayerVerdict(GameWorld w, PlayerData p, ClubData buyer, int weeklyWage)
        {
            if (weeklyWage < ExpectedWage(w, p, buyer) * 0.92f) return Verdict.WageTooLow;
            if (weeklyWage < RequiredWage(w, p, buyer) * 0.995f) return Verdict.ClubNotAppealing;
            return Verdict.Accepted;
        }

        /// <summary>
        /// 사는 구단이 감당할 수 있는지.
        ///
        /// ceiling 은 급여 비중 상한이다. AI 구단은 경고선을 쓴다 — 제재선까지 꽉 채우면
        /// 리그 전체가 한계선에 눌러앉아 아무도 영입하지 못하고 시장이 멈춘다.
        /// 플레이어 구단은 제재선까지 쓸 수 있고, 화면이 미리 경고한다.
        /// </summary>
        public static Verdict BuyerVerdict(
            GameWorld w, ClubData buyer, long fee, int weeklyWage, float? ceiling = null,
            PlayerData incoming = null)
        {
            if (fee > buyer.finances.transferBudget) return Verdict.NoBudget;

            // 홈그로운이 모자라면 외부 영입이 막힌다. 급여 제재선과 같은 방식이다.
            // 홈그로운 선수는 언제나 데려올 수 있다 — 막으면 규정을 지킬 길이 막힌다.
            if (incoming != null && !Homegrown.CanSign(w, buyer.id, incoming))
                return Verdict.HomegrownShort;

            float cap = ceiling ?? (buyer.id == w.ManagedClubId
                ? BalanceConfig.WageSanctionRatio
                : BalanceConfig.AiWageCeiling);

            long newWages = buyer.finances.currentWageSpend + weeklyWage;
            if (newWages * 52f > buyer.finances.annualRevenue * cap)
                return Verdict.NoWageRoom;

            return Verdict.Accepted;
        }

        /// <summary>제안 하나를 전부 따져 본다.</summary>
        public static OfferOutcome Evaluate(GameWorld w, TransferOffer offer)
        {
            var p = w.Player(offer.playerId);
            var buyer = w.Club(offer.fromClubId);
            if (p == null || buyer == null)
                return new OfferOutcome { ClubVerdict = Verdict.NotForSale, Message = "대상을 찾을 수 없습니다." };

            var outcome = new OfferOutcome
            {
                ClubVerdict = SellerVerdict(w, p, offer.upfrontFee),
                PlayerVerdict = PlayerVerdict(w, p, buyer, offer.proposedWeeklyWage),
            };

            var budget = BuyerVerdict(w, buyer, offer.upfrontFee, offer.proposedWeeklyWage, null, p);
            if (budget != Verdict.Accepted)
            {
                outcome.ClubVerdict = budget;
                outcome.Message = Describe(budget, w, p, offer);
                return outcome;
            }

            outcome.Message = outcome.Accepted
                ? "합의했습니다."
                : Describe(outcome.ClubVerdict != Verdict.Accepted ? outcome.ClubVerdict : outcome.PlayerVerdict,
                           w, p, offer);
            return outcome;
        }

        static string Describe(Verdict v, GameWorld w, PlayerData p, TransferOffer offer) => v switch
        {
            Verdict.FeeTooLow => $"{w.Club(p.clubId)?.name} 이 이적료가 부족하다고 합니다. 호가는 {AskingPrice(w, p) / 1_000_000d:F1}M€ 입니다.",
            Verdict.SquadTooThin => $"{w.Club(p.clubId)?.name} 이 대체 자원이 없어 매각할 수 없다고 합니다.",
            Verdict.NotForSale => "매각 대상이 아닙니다.",
            Verdict.NoBudget => "이적 예산이 부족합니다.",
            Verdict.NoWageRoom => "주급 여력이 부족합니다. 급여 비중 한도를 넘습니다.",
            Verdict.WageTooLow => $"{p.FullName} 이 주급이 부족하다고 합니다. 기대치는 주 {ExpectedWage(w, p, w.Club(offer.fromClubId)!) / 1000d:F0}K€ 입니다.",
            Verdict.ClubNotAppealing => $"{p.FullName} 이 지금보다 작은 구단으로 가는 데 난색을 보입니다. 주급을 더 얹어야 합니다.",
            _ => "",
        };

        // ── 체결 ──────────────────────────────────────────────────────────

        /// <summary>이적을 실제로 반영한다. 돈, 소속, 계약, 라인업을 모두 갱신한다.</summary>
        public static void Execute(GameWorld w, TransferOffer offer)
        {
            var p = w.Player(offer.playerId);
            var buyer = w.Club(offer.fromClubId);
            var seller = w.Club(offer.toClubId);
            if (p == null || buyer == null || seller == null) return;

            long fee = offer.upfrontFee;

            seller.playerIds.Remove(p.id);
            buyer.playerIds.Add(p.id);
            p.clubId = buyer.id;
            Homegrown.OnTransfer(w, p, seller, buyer);   // 구단 햇수는 여기서 끊긴다
            TransferRequests.Clear(w, p.id);     // 떠났으면 요청도 끝이다
            Scouting.Forget(w, p.id);            // 내 선수가 되면 보고서가 필요 없다
            p.transferStatus = TransferStatus.NotListed;

            p.contract = new ContractData
            {
                clubId = buyer.id,
                weeklyWage = offer.proposedWeeklyWage,
                startDate = w.Date,
                expiryDate = new GameDate(w.Date.year + Math.Max(1, offer.proposedContractYears), 6, 30),
                status = ContractStatus.Active,
            };

            buyer.finances.balance -= fee;
            buyer.finances.transferBudget = (int)Math.Max(0, buyer.finances.transferBudget - fee);
            seller.finances.balance += fee;
            seller.finances.transferBudget = (int)Math.Min(int.MaxValue, seller.finances.transferBudget + fee * 0.7);

            RecalcWages(w, buyer);
            RecalcWages(w, seller);

            WorldGenerator.ApplyBestEleven(buyer, w);
            WorldGenerator.ApplyBestEleven(seller, w);

            offer.status = TransferOfferStatus.Completed;
            w.TotalTransfers++;
            w.Transfers.Insert(0, new TransferRecord
            {
                date = w.Date,
                playerId = p.id,
                playerName = p.FullName,
                fromClubId = seller.id,
                toClubId = buyer.id,
                fee = fee,
                weeklyWage = offer.proposedWeeklyWage,
            });
            if (w.Transfers.Count > 200) w.Transfers.RemoveRange(200, w.Transfers.Count - 200);
        }

        public static void RecalcWages(GameWorld w, ClubData club)
            => club.finances.currentWageSpend = club.playerIds
                .Sum(id => w.Player(id)?.contract?.weeklyWage ?? 0);

        /// <summary>플레이어가 보내는 제안을 만든다.</summary>
        public static TransferOffer BuildOffer(
            GameWorld w, PlayerData target, long fee, int weeklyWage, int years)
            => new TransferOffer
            {
                offerId = w.NextOfferId++,
                fromClubId = w.ManagedClubId,
                toClubId = target.clubId,
                playerId = target.id,
                offerDate = w.Date,
                status = TransferOfferStatus.Pending,
                transferType = TransferType.Permanent,
                upfrontFee = fee,
                proposedWeeklyWage = weeklyWage,
                proposedContractYears = years,
            };

        // ── 검색 ──────────────────────────────────────────────────────────

        public sealed class ScoutFilter
        {
            public Position Position = Position.None;    // None = 전체
            public int MaxAge = 40;
            public int MinAge = 15;
            public long MaxFee = long.MaxValue;
            public float MinCa = 0;
            public int? LeagueId;
            public bool ListedOnly;
            public string Name = "";
        }

        public static IEnumerable<PlayerData> Scout(GameWorld w, ScoutFilter f, int take = 60)
        {
            var pool = w.Players.Values.Where(p => p.clubId != w.ManagedClubId);

            if (f.LeagueId is int lid)
            {
                var ids = w.League(lid)?.clubIds;
                if (ids != null) pool = pool.Where(p => ids.Contains(p.clubId));
            }
            if (f.Position != Position.None)
                pool = pool.Where(p => (p.positionFlags & f.Position) != 0);
            if (!string.IsNullOrWhiteSpace(f.Name))
                pool = pool.Where(p => p.FullName.Contains(f.Name, StringComparison.OrdinalIgnoreCase));
            if (f.ListedOnly)
                pool = pool.Where(p => p.transferStatus is TransferStatus.ListedForTransfer or TransferStatus.WantAway);

            return pool
                .Where(p => w.Age(p) >= f.MinAge && w.Age(p) <= f.MaxAge)
                .Where(p => GameWorld.Ca(p) >= f.MinCa)
                .Where(p => AskingPrice(w, p) <= f.MaxFee)
                .OrderByDescending(GameWorld.Ca)
                .Take(take)
                .ToList();
        }

        // ── AI 이적 ───────────────────────────────────────────────────────

        /// <summary>
        /// AI 구단끼리 이적을 진행한다. 창구가 열린 주마다 몇 건씩 일어난다.
        /// 세계가 멈춰 있으면 플레이어만 전력을 키우게 되고 리그가 무너진다.
        /// </summary>
        public static int RunAiWindow(GameWorld w, Random rng, int attempts = 0)
        {
            int done = 0;

            // 시도 횟수는 구단 수에 비례해야 한다. 고정값으로 두면 리그가 늘었을 때
            // 구단당 기회가 줄어 시장이 말라붙는다.
            if (attempts <= 0) attempts = Math.Max(14, w.Clubs.Count / 3);

            // 포지션 색인은 창구 한 번에 한 벌이면 된다. 시도마다 6000명을 훑으면
            // 창구가 열린 주의 비용이 라운드 전체를 끌고 간다.
            var index = IndexByPosition(w);
            void Trace(string why) { if (Diagnostics != null) Diagnostics[why] = Diagnostics.GetValueOrDefault(why) + 1; }

            for (int i = 0; i < attempts; i++)
            {
                var buyer = PickBuyer(w, rng);
                if (buyer == null) { Trace("구매자 없음"); continue; }

                // 인원이 넘치면 먼저 정리한다. 사기만 하면 스쿼드가 계속 불어난다.
                if (buyer.playerIds.Count > BalanceConfig.SquadMax && TrySellSurplus(w, buyer, rng))
                {
                    done++;
                    continue;
                }
                if (buyer.playerIds.Count >= BalanceConfig.SquadMax) { Trace("스쿼드 가득"); continue; }

                // 인원이 모자라면 이적료 없는 자유계약 선수부터 본다.
                if (buyer.playerIds.Count < BalanceConfig.SquadTarget && TrySignFreeAgent(w, buyer, rng))
                {
                    done++;
                    continue;
                }

                var need = WeakestPosition(w, buyer);
                var target = FindTarget(w, buyer, need, rng, index);
                if (target == null) { Trace("대상 없음"); continue; }

                long fee = AskingPrice(w, target);
                int wage = (int)(RequiredWage(w, target, buyer) * 1.02f);

                var bv = BuyerVerdict(w, buyer, fee, wage, null, target);
                if (bv != Verdict.Accepted) { Trace("구매측 " + bv); continue; }
                var sv = SellerVerdict(w, target, fee);
                if (sv != Verdict.Accepted) { Trace("판매측 " + sv); continue; }
                var pv = PlayerVerdict(w, target, buyer, wage);
                if (pv != Verdict.Accepted) { Trace("선수 " + pv); continue; }

                Execute(w, new TransferOffer
                {
                    offerId = w.NextOfferId++,
                    fromClubId = buyer.id,
                    toClubId = target.clubId,
                    playerId = target.id,
                    offerDate = w.Date,
                    upfrontFee = fee,
                    proposedWeeklyWage = wage,
                    proposedContractYears = 3 + rng.Next(2),
                    transferType = TransferType.Permanent,
                });
                done++;
            }

            return done;
        }

        /// <summary>자유계약 선수를 데려온다. 이적료가 없어 하위 구단도 전력을 보강할 수 있다.</summary>
        static bool TrySignFreeAgent(GameWorld w, ClubData buyer, Random rng)
        {
            var pos = WeakestPosition(w, buyer);

            // 선수 대부분은 이적료 없이 들어온다. 여기서 색깔을 보지 않으면
            // 이적 시장만 손봐도 스쿼드는 시스템과 겉돈 채로 남는다.
            float skill = AiRecruiting.SkillOf(buyer);

            var candidates = w.FreeAgents()
                .Where(p => (p.positionFlags & pos) != 0)
                .OrderByDescending(p => AiRecruiting.Score(w, buyer, p, pos, skill))
                .Take(8)
                .ToList();

            if (candidates.Count == 0)
            {
                candidates = w.FreeAgents().OrderByDescending(GameWorld.Ca).Take(8).ToList();
                if (candidates.Count == 0) return false;
            }

            var player = candidates[rng.Next(candidates.Count)];
            int wage = (int)(RequiredWage(w, player, buyer) * 1.02f);

            if (BuyerVerdict(w, buyer, 0, wage, null, player) != Verdict.Accepted) return false;
            if (PlayerVerdict(w, player, buyer, wage) != Verdict.Accepted) return false;

            SignFreeAgent(w, buyer, player, wage, 2 + rng.Next(3));
            return true;
        }

        /// <summary>자유계약 영입. 이적료가 없고 파는 구단도 없다.</summary>
        public static void SignFreeAgent(
            GameWorld w, ClubData buyer, PlayerData p, int weeklyWage, int years)
        {
            if (p.clubId != 0) return;

            buyer.playerIds.Add(p.id);
            p.clubId = buyer.id;
            Homegrown.OnTransfer(w, p, null, buyer);     // 자유계약도 구단 햇수는 처음부터다
            p.transferStatus = TransferStatus.NotListed;
            p.contract = new ContractData
            {
                clubId = buyer.id,
                weeklyWage = weeklyWage,
                startDate = w.Date,
                expiryDate = new GameDate(w.Date.year + Math.Max(1, years), 6, 30),
                status = ContractStatus.Active,
            };

            RecalcWages(w, buyer);
            WorldGenerator.ApplyBestEleven(buyer, w);

            w.TotalTransfers++;
            w.Transfers.Insert(0, new TransferRecord
            {
                date = w.Date,
                playerId = p.id,
                playerName = p.FullName,
                fromClubId = 0,
                toClubId = buyer.id,
                fee = 0,
                weeklyWage = weeklyWage,
            });
            if (w.Transfers.Count > 200) w.Transfers.RemoveRange(200, w.Transfers.Count - 200);
        }

        /// <summary>
        /// 남는 선수를 인원이 모자란 구단에 넘긴다.
        /// 파는 쪽이 없으면 스쿼드가 한쪽으로 불어나고, 자금이 돌지 않아 시장이 멈춘다.
        /// </summary>
        static bool TrySellSurplus(GameWorld w, ClubData seller, Random rng)
        {
            var surplus = w.Squad(seller.id)
                .OrderByDescending(GameWorld.Ca)
                .Skip(BalanceConfig.SquadTarget - 4)
                .Where(p => !p.IsInjured)
                .ToList();
            if (surplus.Count == 0) return false;

            var player = surplus[rng.Next(surplus.Count)];
            long fee = AskingPrice(w, player);

            // 인원이 모자라면서 그 선수를 감당할 수 있는 구단을 찾는다.
            var buyers = w.Clubs.Values
                .Where(c => c.id != seller.id && c.id != w.ManagedClubId)
                .Where(c => c.playerIds.Count < BalanceConfig.SquadTarget)
                .Where(c => BuyerVerdict(w, c, fee, RequiredWage(w, player, c), null, player) == Verdict.Accepted)
                .Take(12)
                .ToList();
            if (buyers.Count == 0) return false;

            var buyer = buyers[rng.Next(buyers.Count)];
            int wage = (int)(RequiredWage(w, player, buyer) * 1.02f);

            if (PlayerVerdict(w, player, buyer, wage) != Verdict.Accepted) return false;
            if (SellerVerdict(w, player, fee) != Verdict.Accepted) return false;

            Execute(w, new TransferOffer
            {
                offerId = w.NextOfferId++,
                fromClubId = buyer.id,
                toClubId = seller.id,
                playerId = player.id,
                offerDate = w.Date,
                upfrontFee = fee,
                proposedWeeklyWage = wage,
                proposedContractYears = 2 + rng.Next(3),
                transferType = TransferType.Permanent,
            });
            return true;
        }

        static ClubData PickBuyer(GameWorld w, Random rng)
        {
            // 돈이 있고 플레이어 구단이 아닌 곳 중에서 고른다. 평판이 높을수록 자주 움직인다.
            var candidates = w.Clubs.Values
                .Where(c => c.id != w.ManagedClubId)
                .Where(c => c.finances.transferBudget > 1_000_000)
                .ToList();
            if (candidates.Count == 0) return null;

            int total = candidates.Sum(c => c.reputation);
            int roll = rng.Next(total);
            foreach (var c in candidates)
            {
                roll -= c.reputation;
                if (roll < 0) return c;
            }
            return candidates[^1];
        }

        static readonly Position[] CoreSlots =
        {
            Position.GK, Position.CB, Position.LB, Position.RB,
            Position.CDM, Position.CM, Position.CAM,
            Position.LW, Position.RW, Position.ST,
        };

        /// <summary>그 구단에서 가장 약한 포지션.</summary>
        static Position WeakestPosition(GameWorld w, ClubData club)
        {
            Position worst = Position.ST;
            float worstCa = float.MaxValue;

            foreach (var pos in CoreSlots)
            {
                float ca = w.BestCaAt(club.id, pos);
                if (ca < worstCa) { worstCa = ca; worst = pos; }
            }
            return worst;
        }

        /// <summary>
        /// 포지션별 선수 목록. 한 창구 안에서 80번 6000명을 다시 훑지 않기 위해 한 번만 만든다.
        /// <c>positionFlags</c> 는 생성 때 정해지고 변하지 않으므로 창구 한 번 동안 유효하다.
        /// <c>w.Players.Values</c> 순서를 그대로 지킨다 — 순서가 바뀌면 동점 정렬 결과가 달라져
        /// 어느 선수가 뽑히는지까지 바뀐다.
        /// </summary>
        static Dictionary<Position, PositionIndex> IndexByPosition(GameWorld w)
        {
            var map = new Dictionary<Position, PositionIndex>(CoreSlots.Length);
            foreach (var slot in CoreSlots) map[slot] = new PositionIndex(w.Players.Count / 4);

            foreach (var p in w.Players.Values)
            {
                float ca = GameWorld.Ca(p);
                foreach (var slot in CoreSlots)
                    if ((p.positionFlags & slot) != 0) map[slot].Add(p, ca);
            }

            return map;
        }

        /// <summary>색인 없이 부른 경우. 세계 전원을 한 묶음으로 본다 — 결과는 같다.</summary>
        static PositionIndex WholeWorldIndex(GameWorld w)
        {
            var all = new PositionIndex(w.Players.Count);
            foreach (var p in w.Players.Values) all.Add(p, GameWorld.Ca(p));
            return all;
        }

        /// <summary>
        /// 한 자리의 후보들. 목록과 CA 를 나란히 들고 있는다 —
        /// 걸러낼 때 후보마다 CA 를 다시 환산하면, 그 환산이 걸러내기 비용의 절반이다.
        /// CA 는 창구 한 번 동안 변하지 않는다(성장은 구단 순회에서 끝났고, 이적은 소속만 바꾼다).
        /// </summary>
        sealed class PositionIndex
        {
            public readonly List<PlayerData> players;
            public readonly List<float> ca;

            public PositionIndex(int capacity)
            {
                players = new List<PlayerData>(capacity);
                ca = new List<float>(capacity);
            }

            public void Add(PlayerData p, float value) { players.Add(p); ca.Add(value); }
        }

        static PlayerData FindTarget(GameWorld w, ClubData buyer, Position pos, Random rng,
                                     Dictionary<Position, PositionIndex> index = null)
        {
            float bar = w.BestCaAt(buyer.id, pos);

            // 실력만 보면 고압박 팀이 느린 선수를, 점유 팀이 패스 못 하는 선수를 사 온다.
            // 구단 수준만큼 자기 시스템에 맞는 선수를 고른다.
            float skill = AiRecruiting.SkillOf(buyer);

            // 걸러내기와 점수 매기기를 갈라 잰다. 합쳐 두면 6000명을 훑는 값이
            // 비싼 것인지 한 명당 점수가 비싼 것인지 알 수 없다.
            // 색인이 있으면 그 포지션만 훑는다. 없으면 전원을 훑는다 — 결과는 같다.
            PositionIndex source = index != null && index.TryGetValue(pos, out var listed)
                ? listed
                : WholeWorldIndex(w);

            // 걸러내기는 한 번의 순회로 끝낸다. LINQ 를 네 겹 쌓으면 후보마다 대리자가
            // 네 번 불리는데, 이 자리는 창구가 열린 주에 후보 10만 명을 본다.
            // 조건의 순서는 그대로다 — 싼 비트 검사가 먼저, 비싼 호가가 마지막.
            List<PlayerData> pool;
            using (SimProfile.Measure("      · 후보 걸러내기"))
            {
                pool = new List<PlayerData>();
                long budget = buyer.finances.transferBudget;
                float cut = bar + 2f;

                for (int i = 0; i < source.players.Count; i++)
                {
                    var p = source.players[i];
                    if (p.clubId == buyer.id || p.clubId == w.ManagedClubId) continue;
                    if ((p.positionFlags & pos) == 0) continue;
                    if (!(source.ca[i] > cut)) continue;
                    if (!AskingPriceWithin(w, p, budget)) continue;
                    pool.Add(p);
                }
            }
            SimProfile.Tally("      · 남은 후보 수", pool.Count);

            List<PlayerData> options;
            using (SimProfile.Measure("      · 점수·정렬"))
                options = pool
                    .OrderByDescending(p => AiRecruiting.Score(w, buyer, p, pos, skill))
                    .Take(10)
                    .ToList();

            return options.Count == 0 ? null : options[rng.Next(options.Count)];
        }

        // ── 시즌 사이 정리 ────────────────────────────────────────────────

        /// <summary>구단이 남는 선수를 이적 목록에 올린다.</summary>
        public static void UpdateTransferLists(GameWorld w, Random rng)
        {
            foreach (var club in w.Clubs.Values)
            {
                if (club.id == w.ManagedClubId) continue;

                var ranked = w.Squad(club.id).OrderByDescending(GameWorld.Ca).ToList();
                for (int i = 0; i < ranked.Count; i++)
                {
                    var p = ranked[i];
                    bool surplus = i >= 20 && w.Age(p) >= 24;
                    bool expiring = p.contract != null
                        && GameDate.MonthsBetween(w.Date, p.contract.expiryDate) <= 12;

                    p.transferStatus = (surplus || (expiring && i >= 14)) && rng.Next(100) < 40
                        ? TransferStatus.ListedForTransfer
                        : TransferStatus.NotListed;
                }
            }
        }
    }

}
