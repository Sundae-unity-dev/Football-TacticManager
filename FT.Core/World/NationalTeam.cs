using System;
using System.Collections.Generic;
using System.Linq;
using FootballTactician.Balance;
using FootballTactician.Data;

namespace FootballTactician.World
{
    /// <summary>
    /// 국가대표.
    ///
    /// 감독이 고를 수 있는 것이 아니라 <b>감독에게 일어나는 일</b>이다.
    /// 좋은 선수는 뽑혀 나가고 지쳐서 돌아온다 — 스쿼드를 깊게 둘 이유가 생긴다.
    /// 뽑히는 것 자체는 선수에게 좋은 일이라 사기가 오르고 몸값이 붙는다.
    ///
    /// 명단은 저장하지 않는다. 국적과 실력에서 그때그때 다시 나온다.
    /// </summary>
    public static class NationalTeam
    {
        /// <summary>A매치가 열리는 리그 라운드. 이 주에는 리그 경기가 없다고 본다.</summary>
        public static readonly int[] BreakMatchdays = { 3, 10, 16, 22, 28, 33 };

        /// <summary>그 나라 대표팀에 뽑힐 만한 선수인지. 개인 목표가 이 판단을 쓴다.</summary>
        public static bool IsCandidate(GameWorld w, PlayerData p)
        {
            if (p == null || p.clubId <= 0) return false;

            float ca = GameWorld.Ca(p);
            return ca >= Threshold(w, p.nationality) - BalanceConfig.CallUpHopeGap;
        }

        /// <summary>
        /// 그 나라의 대표 발탁선. 나라마다 선수층이 달라 절대 기준으로는 잴 수 없다 —
        /// 선수층이 얇은 나라에서는 더 낮은 실력으로도 뽑힌다.
        /// </summary>
        public static float Threshold(GameWorld w, Nationality nation)
        {
            var pool = w.Players.Values
                .Where(p => p.clubId > 0 && p.nationality == nation)
                .Select(GameWorld.Ca)
                .OrderByDescending(x => x)
                .Take(BalanceConfig.NationalSquadSize)
                .ToList();

            return pool.Count == 0 ? 999f : pool[^1];
        }

        /// <summary>그 나라의 이번 소집 명단. 실력 순으로 뽑되 자리별로 최소한을 채운다.</summary>
        public static List<PlayerData> Squad(GameWorld w, Nationality nation)
        {
            var pool = w.Players.Values
                .Where(p => p.clubId > 0 && p.nationality == nation && !p.IsInjured)
                .OrderByDescending(GameWorld.Ca)
                .ToList();

            var picked = new List<PlayerData>();

            // 골키퍼를 먼저 채운다. 실력 순으로만 뽑으면 골키퍼 없는 대표팀이 나온다.
            foreach (var gk in pool.Where(p => (p.positionFlags & Position.GK) != 0)
                                   .Take(BalanceConfig.NationalKeepers))
                picked.Add(gk);

            foreach (var p in pool)
            {
                if (picked.Count >= BalanceConfig.NationalSquadSize) break;
                if (picked.Contains(p)) continue;
                picked.Add(p);
            }

            return picked;
        }

        /// <summary>이번 라운드가 A매치 주간인지.</summary>
        public static bool IsBreak(int matchday) => BreakMatchdays.Contains(matchday);

        /// <summary>
        /// A매치 주간을 치른다.
        ///
        /// 뽑힌 선수는 경기를 뛰고 지쳐서 돌아온다. 가끔 다치기도 한다 —
        /// 감독이 막을 수 없는 일이라, 스쿼드가 얇으면 그대로 타격이 된다.
        /// </summary>
        public static void RunBreak(GameWorld w, Random rng)
        {
            foreach (var nation in w.Nations)
            {
                var code = NameForge.ByCode(nation.Code).Nationality;
                var squad = Squad(w, code);

                foreach (var p in squad)
                {
                    p.caps++;

                    // 소집은 체력을 쓴다. 두 경기를 치르고 오가는 길이 있다.
                    p.condition = Math.Clamp(p.condition - BalanceConfig.CallUpConditionCost, 0f, 100f);

                    // 뽑힌 것 자체가 선수에게는 좋은 일이다.
                    p.morale = Math.Clamp(p.morale + BalanceConfig.CallUpMorale, 25f, 95f);

                    // 대표팀에서 득점하면 기록에 남는다.
                    if ((p.positionFlags & (Position.ST | Position.CF | Position.LW | Position.RW)) != 0
                        && rng.NextDouble() < BalanceConfig.CallUpGoalChance)
                        p.internationalGoals++;

                    // 대표팀에서 다쳐 오는 일. 감독이 막을 수 없다.
                    if (rng.NextDouble() < BalanceConfig.CallUpInjuryChance)
                    {
                        int weeks = 1 + rng.Next(3);
                        p.injury = new InjuryData
                        {
                            injuryType = "A매치 중 부상",
                            severity = weeks <= 1 ? InjurySeverity.Minor : InjurySeverity.Moderate,
                            injuryDate = w.Date,
                            expectedReturnDate = w.Date.AddDays(weeks * 7),
                            isActive = true,
                        };

                        if (p.clubId == w.ManagedClubId)
                            w.Post("A매치 부상",
                                $"{p.FullName} 이 국가대표 경기에서 다쳐 돌아왔습니다. " +
                                $"복귀까지 약 {weeks}주로 보입니다.",
                                InboxKind.Injury, playerId: p.id);
                    }
                }
            }

            ReportToManager(w);
        }

        static void ReportToManager(GameWorld w)
        {
            if (w.ManagedClubId <= 0 || w.MyClub == null) return;

            var called = w.Squad(w.ManagedClubId)
                .Where(p => p.caps > 0 && !p.IsInjured)
                .Where(p => Squad(w, p.nationality).Any(x => x.id == p.id))
                .ToList();

            if (called.Count == 0) return;

            // 한 명이면 그 선수로, 여럿이면 가리킬 선수가 하나로 안 정해지니 내 구단으로
            // 이어 준다(대상은 선수 → 구단 순으로 고른다). 어느 쪽이든 눌리기는 한다.
            w.Post("A매치 소집",
                $"{called.Count}명이 국가대표에 소집됐습니다 — " +
                string.Join(", ", called.Take(6).Select(p => p.FullName)) +
                (called.Count > 6 ? " 외" : "") +
                ". 지친 상태로 돌아옵니다.",
                InboxKind.News,
                playerId: called.Count == 1 ? called[0].id : 0,
                clubId: w.ManagedClubId);
        }

        /// <summary>대표 경력이 몸값에 붙이는 배수.</summary>
        public static float ValueBonus(PlayerData p)
            => 1f + Math.Min(p.caps, BalanceConfig.CapValueCeiling)
                    / (float)BalanceConfig.CapValueCeiling * BalanceConfig.CapValueBonus;
    }
}
