using System;
using System.Collections.Generic;
using System.Linq;
using FootballTactician.Balance;
using FootballTactician.Data;
using FootballTactician.Simulation;

namespace FootballTactician.World
{
    /// <summary>
    /// 이사회와의 협상.
    ///
    /// 지금까지 이사회는 목표를 내리고 신뢰도를 매기기만 했다. 감독이 말을 걸 수는 없었다.
    /// 예산이 모자라도, 목표가 터무니없어도 그냥 받아들이는 수밖에 없었다.
    ///
    /// 네 가지를 요청할 수 있다. <b>모두 신뢰도를 쓴다.</b> 공짜로 조를 수 있으면
    /// 매주 예산을 올려 달라고 하는 것 말고 할 일이 없다.
    ///
    /// 성패는 성적이 정한다 — 잘하고 있으면 들어주고, 못하고 있으면 신뢰만 깎인다.
    /// 그래서 "언제 조르는가"가 판단이 된다.
    /// </summary>
    public static class BoardTalks
    {
        public enum Ask
        {
            /// <summary>이적 예산을 늘려 달라.</summary>
            TransferBudget,

            /// <summary>급여 예산을 늘려 달라.</summary>
            WageBudget,

            /// <summary>시즌 목표를 낮춰 달라.</summary>
            EaseObjective,

            /// <summary>훈련 시설에 투자해 달라.</summary>
            Facilities,

            /// <summary>경기장을 늘려 달라.</summary>
            Stadium,
        }

        public enum Verdict { Granted, Refused, TooSoon, NothingToAsk, NoFunds }

        public sealed class Answer
        {
            public Verdict verdict;
            public string headline = "";
            public string detail = "";
            public int confidenceCost;
            public long amount;
        }

        // ── 조를 수 있는가 ────────────────────────────────────────────────

        public static bool OnCooldown(GameWorld w)
            => w.LastBoardAskDay > 0
            && DayNumber(w.Date) - w.LastBoardAskDay < BalanceConfig.BoardAskCooldownDays;

        public static int DaysUntilCanAsk(GameWorld w)
            => Math.Max(0, BalanceConfig.BoardAskCooldownDays - (DayNumber(w.Date) - w.LastBoardAskDay));

        static int DayNumber(GameDate d) => d.year * 372 + d.month * 31 + d.day;

        /// <summary>
        /// 들어줄 확률. 신뢰도가 바탕을 깔고, 성적과 재정이 더하거나 뺀다.
        ///
        /// 요청마다 문턱이 다르다 — 목표를 낮춰 달라는 쪽이 돈 달라는 쪽보다 어렵다.
        /// 같으면 늘 가장 비싼 것만 조르게 된다.
        /// </summary>
        public static float Odds(GameWorld w, Ask ask)
        {
            var mp = w.Manager;
            if (mp == null || w.MyClub is not { } club) return 0f;

            float odds = mp.boardConfidence / 100f * BalanceConfig.BoardOddsFromConfidence;

            // 순위가 기대보다 좋으면 말이 먹힌다. 절대 순위가 아니라 전력 대비로 본다 —
            // 절대 순위를 쓰면 약팀 감독은 영원히 아무것도 얻지 못한다.
            odds += ExpectationGap(w, club) * BalanceConfig.BoardOddsPerPlaceAhead;

            // 곳간이 비어 있으면 돈 이야기는 안 통한다.
            if (ask is Ask.TransferBudget or Ask.WageBudget or Ask.Facilities or Ask.Stadium)
                odds += FundsHealth(club) * BalanceConfig.BoardOddsFromFunds;

            odds -= BalanceConfig.BoardAskDifficulty[(int)ask];

            return Math.Clamp(odds, BalanceConfig.BoardOddsFloor, BalanceConfig.BoardOddsCeiling);
        }

        /// <summary>리그 순위가 전력 순위보다 몇 계단 앞서는가. 양수면 기대 이상.</summary>
        public static float ExpectationGap(GameWorld w, ClubData club)
        {
            var league = w.LeagueOf(club.id);
            if (league?.currentSeason == null) return 0f;

            int place = Standings.PositionOf(league, club.id);
            if (place <= 0) return 0f;

            int strengthRank = league.clubIds
                .OrderByDescending(id => w.Squad(id).Any() ? w.Squad(id).Average(GameWorld.Ca) : 0)
                .ToList()
                .IndexOf(club.id) + 1;

            return strengthRank - place;
        }

        /// <summary>0 이면 빠듯하고 1 이면 넉넉하다.</summary>
        static float FundsHealth(ClubData c)
        {
            if (c.finances.annualRevenue <= 0) return 0f;
            return Math.Clamp((float)(c.finances.balance / (double)c.finances.annualRevenue), 0f, 1f);
        }

        // ── 조른다 ────────────────────────────────────────────────────────

        public static Answer Request(GameWorld w, Ask ask, Random rng)
        {
            if (w.Manager is not { } mp || w.MyClub is not { } club)
                return new Answer { verdict = Verdict.NothingToAsk, headline = "구단이 없습니다." };

            if (OnCooldown(w))
                return new Answer
                {
                    verdict = Verdict.TooSoon,
                    headline = "아직 이릅니다.",
                    detail = $"{DaysUntilCanAsk(w)}일 뒤에 다시 이야기할 수 있습니다.",
                };

            if (ask == Ask.EaseObjective && HardestObjective(w) == null)
                return new Answer
                {
                    verdict = Verdict.NothingToAsk,
                    headline = "낮출 목표가 없습니다.",
                    detail = "이사회가 내건 목표가 모두 끝났습니다.",
                };

            int beforeAskDay = w.LastBoardAskDay;
            w.LastBoardAskDay = DayNumber(w.Date);

            bool granted = rng.NextDouble() < Odds(w, ask);

            // 거절당해도 신뢰는 깎인다. 그래야 "밑져야 본전"이 되지 않는다.
            int cost = granted
                ? BalanceConfig.BoardCostGranted[(int)ask]
                : BalanceConfig.BoardCostRefused[(int)ask];

            mp.boardConfidence = Math.Clamp(mp.boardConfidence - cost, 0, 100);

            var answer = granted ? Grant(w, club, ask) : Refuse(w, ask);

            // 들어주겠다고 해 놓고 내줄 것이 없으면 없던 일로 한다.
            // 아무것도 못 받았는데 신뢰도와 기다림만 치르면 요청 자체가 벌이 된다.
            if (answer.verdict is Verdict.NoFunds or Verdict.NothingToAsk)
            {
                mp.boardConfidence = Math.Clamp(mp.boardConfidence + cost, 0, 100);
                w.LastBoardAskDay = beforeAskDay;
                answer.confidenceCost = 0;

                w.Post(answer.headline,
                       answer.detail + "\n\n이번 이야기는 없던 일이 됐습니다.",
                       InboxKind.Board);
                return answer;
            }

            answer.confidenceCost = cost;

            w.Post(answer.headline,
                   answer.detail + $"\n\n이사회 신뢰도가 {cost} 내려 {mp.boardConfidence} 이 됐습니다.",
                   InboxKind.Board);

            return answer;
        }

        static Answer Refuse(GameWorld w, Ask ask) => new()
        {
            verdict = Verdict.Refused,
            headline = $"이사회가 {AskLabel(ask)} 요청을 거절했습니다",
            detail = w.Manager!.boardConfidence >= 60
                ? "지금은 때가 아니라고 합니다. 성적으로 보여 달라는 뜻입니다."
                : "감독의 말에 무게가 실리지 않습니다.",
        };

        static Answer Grant(GameWorld w, ClubData club, Ask ask)
        {
            switch (ask)
            {
                case Ask.TransferBudget:
                {
                    long add = (long)(club.finances.annualRevenue * BalanceConfig.BoardTransferGrant);
                    add = Math.Min(add, Math.Max(0, club.finances.balance - club.finances.transferBudget));
                    if (add <= 0)
                        return new Answer
                        {
                            verdict = Verdict.NoFunds,
                            headline = "곳간이 비어 있습니다",
                            detail = "이사회도 내줄 돈이 없습니다.",
                        };

                    club.finances.transferBudget = (int)Math.Clamp(
                        club.finances.transferBudget + add, 0, int.MaxValue);

                    return new Answer
                    {
                        verdict = Verdict.Granted,
                        amount = add,
                        headline = "이적 예산이 늘었습니다",
                        detail = $"{add / 1_000_000.0:F1}M€ 를 더 쓸 수 있습니다.",
                    };
                }

                case Ask.WageBudget:
                {
                    int add = (int)(club.finances.weeklyWageBudget * BalanceConfig.BoardWageGrant);
                    club.finances.weeklyWageBudget += add;
                    club.boardWageBonus += add;   // 다음 재산정 때 날아가지 않게 기억해 둔다

                    return new Answer
                    {
                        verdict = Verdict.Granted,
                        amount = add,
                        headline = "급여 예산이 늘었습니다",
                        detail = $"주당 {add:N0}€ 를 더 쓸 수 있습니다.",
                    };
                }

                case Ask.EaseObjective:
                {
                    var o = HardestObjective(w)!;
                    int before = o.targetValue;
                    o.targetValue = Math.Max(1, (int)Math.Round(
                        o.targetValue * BalanceConfig.BoardObjectiveEase));
                    o.weight = Math.Max(1, o.weight - 1);

                    return new Answer
                    {
                        verdict = Verdict.Granted,
                        headline = "목표가 낮아졌습니다",
                        detail = $"{o.description} — {before} 에서 {o.targetValue} 로.",
                    };
                }

                case Ask.Stadium:
                {
                    var verdict = ClubBusiness.CanExpand(club);
                    if (verdict == ClubBusiness.BuildVerdict.AtMax)
                        return new Answer
                        {
                            verdict = Verdict.NothingToAsk,
                            headline = "더 늘릴 수 없습니다",
                            detail = "경기장이 이미 최대 규모입니다.",
                        };
                    if (verdict == ClubBusiness.BuildVerdict.NoFunds)
                        return new Answer
                        {
                            verdict = Verdict.NoFunds,
                            headline = "곳간이 비어 있습니다",
                            detail = $"공사비 {ClubBusiness.ExpansionCost(club) / 1_000_000.0:F1}M€ 를 댈 수 없습니다.",
                        };

                    long seats = ClubBusiness.ExpansionSeats(club);
                    long spent = ClubBusiness.ExpansionCost(club);
                    ClubBusiness.Expand(w, club);

                    return new Answer
                    {
                        verdict = Verdict.Granted,
                        amount = spent,
                        headline = "경기장을 늘립니다",
                        detail = $"{seats:N0}석을 더합니다. 수입은 다음 시즌부터 늘어납니다.",
                    };
                }

                default:
                {
                    if (club.trainingFacilitiesRating >= 5)
                        return new Answer
                        {
                            verdict = Verdict.NothingToAsk,
                            headline = "이미 최고 수준입니다",
                            detail = "훈련 시설은 더 올릴 곳이 없습니다.",
                        };

                    long cost = (long)(club.finances.annualRevenue * BalanceConfig.BoardFacilityCost);
                    if (club.finances.balance < cost)
                        return new Answer
                        {
                            verdict = Verdict.NoFunds,
                            headline = "곳간이 비어 있습니다",
                            detail = "공사비를 댈 수 없습니다.",
                        };

                    club.finances.balance -= cost;
                    club.trainingFacilitiesRating++;

                    return new Answer
                    {
                        verdict = Verdict.Granted,
                        amount = cost,
                        headline = "훈련 시설을 올립니다",
                        detail = $"{cost / 1_000_000.0:F1}M€ 를 들여 {club.trainingFacilitiesRating}단계가 됐습니다.",
                    };
                }
            }
        }

        /// <summary>가장 무겁고 아직 끝나지 않은 목표.</summary>
        public static BoardObjective HardestObjective(GameWorld w)
            => w.Objectives?
                .Where(o => o.result == BoardObjectiveResult.Pending)
                .OrderByDescending(o => o.weight)
                .FirstOrDefault();

        public static string AskLabel(Ask a) => a switch
        {
            Ask.TransferBudget => "이적 예산",
            Ask.WageBudget => "급여 예산",
            Ask.EaseObjective => "목표 조정",
            Ask.Stadium => "경기장 증축",
            _ => "시설 투자",
        };
    }
}
