using System;
using System.Collections.Generic;
using System.Linq;
using FootballTactician.Balance;
using FootballTactician.Data;

namespace FootballTactician.World
{
    /// <summary>
    /// 재계약 협상. 내 구단 선수의 계약이 끝나기 전에 붙잡을 수 있어야 한다.
    ///
    /// 이것이 없으면 주전이 내 의사와 무관하게 떠난다. 선수를 사 오는 것만큼이나
    /// 있는 선수를 지키는 것이 감독의 일이다.
    /// </summary>
    public static class ContractTalks
    {
        /// <summary>이 개월 수 아래로 남으면 재계약 대상으로 본다.</summary>
        public const int TalksWindowMonths = 18;

        /// <summary>계약 만료가 임박한 선수. 급한 순서대로.</summary>
        public static IEnumerable<PlayerData> Expiring(GameWorld w, int clubId)
            => w.Squad(clubId)
                .Where(p => p.contract != null)
                .Where(p => MonthsLeft(w, p) <= TalksWindowMonths)
                .OrderBy(p => MonthsLeft(w, p))
                .ThenByDescending(GameWorld.Ca);

        public static int MonthsLeft(GameWorld w, PlayerData p)
            => p.contract == null ? 0 : GameDate.MonthsBetween(w.Date, p.contract.expiryDate);

        /// <summary>
        /// 선수가 요구하는 주급. 시장가에 스쿼드 내 위치와 나이를 얹는다.
        /// 주전일수록, 어릴수록 많이 부른다.
        /// </summary>
        public static int DemandedWage(GameWorld w, PlayerData p)
        {
            var club = w.Club(p.clubId);
            if (club == null) return p.contract?.weeklyWage ?? 0;

            float market = BalanceConfig.WeeklyWage(
                GameWorld.Ca(p), w.Age(p), w.RepCoefOf(club.id));

            float mult = 1.0f;

            int rank = w.Squad(club.id).OrderByDescending(GameWorld.Ca).ToList()
                .FindIndex(x => x.id == p.id);
            if (rank >= 0 && rank < 3) mult += 0.25f;
            else if (rank < 11) mult += 0.10f;
            else if (rank >= 20) mult -= 0.10f;

            int age = w.Age(p);
            if (age <= 23 && PlayerDevelopment.Pa(p) > GameWorld.Ca(p) + 20f) mult += 0.15f;
            if (age >= 33) mult -= 0.20f;

            // 지금 받는 것보다 적게 부르지는 않는다.
            int current = p.contract?.weeklyWage ?? 0;
            return Math.Max(current, (int)(market * mult));
        }

        /// <summary>선수가 원하는 계약 기간.</summary>
        public static int DemandedYears(GameWorld w, PlayerData p)
        {
            int age = w.Age(p);
            return age >= 33 ? 1 : age >= 30 ? 2 : age <= 23 ? 5 : 4;
        }

        public enum Verdict { Accepted, WageTooLow, TermTooShort, NoWageRoom, NotInterested }

        public sealed class Outcome
        {
            public Verdict Result;
            public string Message = "";
            public bool Accepted => Result == Verdict.Accepted;
        }

        /// <summary>제안을 따져 본다.</summary>
        public static Outcome Evaluate(GameWorld w, PlayerData p, int weeklyWage, int years)
        {
            var club = w.Club(p.clubId);
            if (club == null || p.contract == null)
                return new Outcome { Result = Verdict.NotInterested, Message = "대상이 아닙니다." };

            // 구단이 감당할 수 있는지 — 지금 주급은 이미 장부에 있으므로 차액만 본다.
            long newSpend = club.finances.currentWageSpend - p.contract.weeklyWage + weeklyWage;
            float cap = club.id == w.ManagedClubId
                ? BalanceConfig.WageSanctionRatio
                : BalanceConfig.AiWageCeiling;
            if (newSpend * 52f > club.finances.annualRevenue * cap)
                return new Outcome
                {
                    Result = Verdict.NoWageRoom,
                    Message = $"주급 여력이 부족합니다. 급여 비중이 {newSpend * 52f / club.finances.annualRevenue:P0} 가 됩니다.",
                };

            int want = DemandedWage(w, p);
            if (weeklyWage < want * 0.97f)
                return new Outcome
                {
                    Result = Verdict.WageTooLow,
                    Message = $"{p.FullName} 은 주 {want / 1000d:F0}K€ 를 원합니다.",
                };

            int wantYears = DemandedYears(w, p);
            // 어린 선수는 짧은 계약을 꺼리고, 노장은 긴 계약을 받아들인다.
            if (w.Age(p) <= 26 && years < wantYears - 2)
                return new Outcome
                {
                    Result = Verdict.TermTooShort,
                    Message = $"{p.FullName} 은 더 긴 계약을 원합니다. {wantYears}년 정도를 기대합니다.",
                };

            return new Outcome { Result = Verdict.Accepted, Message = "재계약에 합의했습니다." };
        }

        /// <summary>재계약을 적용한다.</summary>
        public static void Sign(GameWorld w, PlayerData p, int weeklyWage, int years)
        {
            var club = w.Club(p.clubId);
            if (club == null || p.contract == null) return;

            p.contract.weeklyWage = weeklyWage;
            p.contract.startDate = w.Date;
            p.contract.expiryDate = new GameDate(w.Date.year + Math.Max(1, years), 6, 30);
            p.contract.status = ContractStatus.Active;
            p.transferStatus = TransferStatus.NotListed;

            TransferMarket.RecalcWages(w, club);
        }

        /// <summary>
        /// 계약이 임박한 선수를 알린다. 시즌 중 한 번만 보낸다.
        /// 모르고 있다가 주전을 놓치는 일이 없어야 한다.
        /// </summary>
        public static void WarnExpiring(GameWorld w)
        {
            if (w.ManagedClubId <= 0) return;

            foreach (var p in Expiring(w, w.ManagedClubId).ToList())
            {
                int months = MonthsLeft(w, p);
                if (months > 6) continue;
                if (w.ContractWarnings.Contains(p.id)) continue;

                w.ContractWarnings.Add(p.id);
                w.Post($"{p.FullName} 계약 만료 임박",
                       $"{months}개월 남았습니다. 요구 주급은 주 {DemandedWage(w, p) / 1000d:F0}K€ 입니다. " +
                       "재계약하지 않으면 시즌이 끝날 때 떠납니다.",
                       InboxKind.Transfer, playerId: p.id);
            }
        }
    }
}
