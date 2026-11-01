using System;
using System.Collections.Generic;
using FootballTactician.Data;
using FootballTactician.Simulation;

namespace FootballTactician.World
{
    /// <summary>
    /// 계약서에 적은 보너스를 실제로 지급한다.
    ///
    /// 협상에서 "골당 5K€" 를 내주고 장부에서 빠져나가지 않으면, 그 협상은 숫자놀이다.
    /// 출전·득점은 경기마다, 우승은 시즌 끝에 나간다.
    ///
    /// <b>보너스가 0 인 계약은 한 푼도 건드리지 않는다.</b> AI 구단은 보너스를 협상하지
    /// 않으므로(ContractNegotiation 은 사람만 탄다) 239개 구단에서는 이 코드가
    /// null 검사 한 번으로 끝난다 — 세계의 동작도, 지문도 그대로다.
    /// </summary>
    public static class ContractBonuses
    {
        /// <summary>경기 하나가 끝났을 때. 출전 보너스와 골 보너스.</summary>
        public static void PayMatch(GameWorld w, MatchLineup home, MatchLineup away, MatchResultData result)
        {
            if (result == null) return;
            PayAppearances(w, home);
            PayAppearances(w, away);

            if (result.events == null) return;
            foreach (var e in result.events)
            {
                if (e.eventType != MatchEventType.Goal) continue;
                var p = w.Player(e.playerId);
                int bonus = p?.contract?.bonuses?.goalBonus ?? 0;
                if (bonus > 0) Pay(w, p.clubId, bonus);
            }
        }

        static void PayAppearances(GameWorld w, MatchLineup lineup)
        {
            if (lineup?.tactics?.slots == null) return;
            foreach (var slot in lineup.tactics.slots)
            {
                var p = w.Player(slot.assignedPlayerId);
                int bonus = p?.contract?.bonuses?.appearanceBonus ?? 0;
                if (bonus > 0) Pay(w, p.clubId, bonus);
            }
        }

        /// <summary>리그 우승이 확정됐을 때 한 번.</summary>
        public static void PayTitle(GameWorld w, int clubId)
        {
            foreach (var p in w.Squad(clubId))
            {
                int bonus = p.contract?.bonuses?.titleBonus ?? 0;
                if (bonus > 0) Pay(w, clubId, bonus);
            }
        }

        /// <summary>
        /// 승격하면 "올라가면 올려 준다"고 적은 계약의 주급이 오른다.
        /// 강등 조항은 아직 협상 탁자에 없으므로 내리지는 않는다.
        /// </summary>
        public static void ApplyPromotionRaises(GameWorld w, int clubId)
        {
            var club = w.Club(clubId);
            if (club == null) return;

            bool any = false;
            foreach (var p in w.Squad(clubId))
            {
                var cl = p.contract?.clauses;
                if (cl == null || !cl.hasPromoRelegationClause || cl.promoWageIncrease <= 0) continue;

                p.contract.weeklyWage += (int)(p.contract.weeklyWage * (cl.promoWageIncrease / 100f));
                any = true;
            }
            if (any) TransferMarket.RecalcWages(w, club);
        }

        /// <summary>
        /// 방출 조항이 걸린 선수의 호가. 조항 금액이 호가보다 낮으면 그 값이 곧 가격이다.
        /// 조항을 넣어 주고 싼값에 뺏기는 것이 거래의 뒷면이다.
        /// </summary>
        public static long ClauseCap(PlayerData p, long asking)
        {
            var cl = p?.contract?.clauses;
            if (cl == null || !cl.hasReleaseClause || cl.releaseClauseAmount <= 0) return asking;
            return Math.Min(asking, cl.releaseClauseAmount);
        }

        static void Pay(GameWorld w, int clubId, int amount)
        {
            var club = w.Club(clubId);
            if (club == null) return;
            club.finances.balance -= amount;
        }
    }
}
