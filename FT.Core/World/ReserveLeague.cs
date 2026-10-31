using System;
using System.Collections.Generic;
using System.Linq;
using FootballTactician.Balance;
using FootballTactician.Data;
using FootballTactician.Simulation;

namespace FootballTactician.World
{
    /// <summary>
    /// 2군 경기.
    ///
    /// 1군에 못 드는 선수는 임대를 보내지 않으면 한 경기도 못 뛴다. 그렇다고 모두 내보낼 수도
    /// 없다 — 스쿼드가 얇아진다. 2군이 있으면 데리고 있으면서 뛰게 할 수 있다.
    ///
    /// <b>선수도 리그도 새로 만들지 않는다.</b> 구단마다 2군 팀을 만들면 선수가 두 배가 되고,
    /// 방금 2.3초로 줄여 놓은 저장이 다시 늘어난다. 1군 명단에 없는 선수들끼리
    /// 같은 리그 안에서 맞붙는 것으로 충분하다.
    ///
    /// 일정도 저장하지 않는다. 주차와 리그만 알면 누가 누구와 붙는지 그때그때 정해진다.
    /// </summary>
    public static class ReserveLeague
    {
        /// <summary>2군 경기가 열리는 리그 라운드. 리그·컵·A매치가 없는 주를 쓴다.</summary>
        public static readonly int[] Matchdays = { 5, 13, 21, 29, 37 };

        public static bool IsMatchday(int matchday) => Matchdays.Contains(matchday);

        /// <summary>
        /// 그 구단의 2군. 1군 선발·벤치에 없고 임대 중도 아닌 선수들.
        /// </summary>
        public static List<PlayerData> Squad(GameWorld w, ClubData club)
        {
            var tac = club.savedTactics?.FirstOrDefault(t => t.id == club.activeTacticsId);
            var firstTeam = tac?.slots.Select(s => s.assignedPlayerId).ToHashSet() ?? new HashSet<int>();

            return w.Squad(club.id)
                .Where(p => !firstTeam.Contains(p.id))
                .Where(p => !p.IsInjured)
                .Where(p => !w.Loans.ContainsKey(p.id))
                .OrderByDescending(GameWorld.Ca)
                .Take(BalanceConfig.ReserveSquadMax)
                .ToList();
        }

        /// <summary>
        /// 2군 라운드를 치른다.
        ///
        /// 경기를 실제로 시뮬레이션하지는 않는다. 1군 경기와 같은 무게로 돌리면
        /// 리그가 12개인 세계에서 한 주에 수백 경기가 더 늘어난다.
        /// 출전과 성장만 기록한다 — 2군의 값어치는 결과가 아니라 <b>뛰었다는 사실</b>이다.
        /// </summary>
        public static void RunMatchday(GameWorld w, Random rng)
        {
            foreach (var club in w.Clubs.Values)
            {
                var squad = Squad(w, club);
                if (squad.Count < BalanceConfig.ReserveSquadMin) continue;

                foreach (var p in squad)
                {
                    p.seasonStats ??= new PlayerStats();
                    p.reserveAppearances++;

                    // 2군 경기도 체력을 쓴다. 공짜로 출전 시간을 얻으면 1군 로테이션이 무의미해진다.
                    p.condition = Math.Clamp(
                        p.condition - BalanceConfig.ReserveConditionCost, 0f, 100f);

                    // 뛰면 사기가 오른다. 벤치에 앉아 있는 것보다 낫다.
                    p.morale = Math.Clamp(p.morale + BalanceConfig.ReserveMorale, 25f, 95f);

                    // 2군에서도 다친다. 1군보다는 드물다.
                    if (rng.NextDouble() < BalanceConfig.ReserveInjuryChance)
                    {
                        int weeks = 1 + rng.Next(2);
                        p.injury = new InjuryData
                        {
                            injuryType = "2군 경기 중 부상",
                            severity = InjurySeverity.Minor,
                            injuryDate = w.Date,
                            expectedReturnDate = w.Date.AddDays(weeks * 7),
                            isActive = true,
                        };
                    }
                }
            }
        }

        /// <summary>
        /// 주간 성장에 쓰는 출전 배수. 2군에서 뛰면 벤치보다 낫지만 1군만 못하다.
        /// 이 값이 1군과 같으면 아무도 1군에 들려 하지 않는다.
        /// </summary>
        public static float MinutesFactor(PlayerData p, bool startsForFirstTeam)
        {
            if (startsForFirstTeam) return BalanceConfig.FirstTeamMinutes;

            return p.reserveAppearances > 0
                ? BalanceConfig.ReserveMinutes
                : BalanceConfig.BenchMinutes;
        }

        /// <summary>시즌이 바뀌면 2군 출전 기록을 비운다.</summary>
        public static void StartSeason(GameWorld w)
        {
            foreach (var p in w.Players.Values) p.reserveAppearances = 0;
        }
    }
}
