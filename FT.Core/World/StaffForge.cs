using System;
using System.Collections.Generic;
using System.Linq;
using FootballTactician.Balance;
using FootballTactician.Data;

namespace FootballTactician.World
{
    /// <summary>
    /// 스태프를 만든다.
    ///
    /// 구단마다 평판대로 갖춰 놓는다 — 세계의 기준선이 "스태프가 있는 상태"여야
    /// 스태프 화면을 한 번도 안 열어 본 감독이 손해를 보지 않는다.
    ///
    /// 자유 스태프도 함께 만든다. 없으면 고용이 남의 사람을 빼 오는 일이 되고,
    /// 그러면 AI 구단의 스태프가 시즌마다 털린다.
    /// </summary>
    public static class StaffForge
    {
        public static void Populate(GameWorld w, Random rng)
        {
            w.Staff ??= new Dictionary<int, StaffData>();
            int nextId = 1;

            foreach (var club in w.Clubs.Values.OrderBy(c => c.id))
            {
                club.staffIds ??= new List<int>();
                int fill = StaffPlan.RolesFilled(club.reputation);

                for (int i = 0; i < fill && i < StaffPlan.CoreRoles.Length; i++)
                {
                    var s = Create(w, nextId++, StaffPlan.CoreRoles[i], club.reputation, rng);
                    s.clubId = club.id;
                    s.contract = new ContractData
                    {
                        weeklyWage = StaffRoom.AskingWage(s),
                        expiryDate = w.Date.AddDays(365 * (1 + rng.Next(3))),
                    };

                    w.Staff[s.id] = s;
                    club.staffIds.Add(s.id);
                }

                // 더하지 않고 다시 센다 — 급여 합계의 정의는 한 곳(ClubWages)뿐이다.
                ClubWages.Recalc(w, club);
            }

            // 시장에 나와 있는 사람들. 역할마다 고르게 — 한 역할만 비면 그 자리를 못 채운다.
            foreach (var role in StaffPlan.CoreRoles)
                for (int i = 0; i < BalanceConfig.FreeStaffPerRole; i++)
                {
                    // 평판은 고르게 흩는다. 좋은 사람만 널려 있으면 고용이 공짜 승급이 된다.
                    int rep = 1 + rng.Next(5);
                    var s = Create(w, nextId++, role, rep, rng);
                    s.clubId = 0;
                    w.Staff[s.id] = s;
                }
        }

        static StaffData Create(GameWorld w, int id, StaffRole role, int clubRep, Random rng)
        {
            // 구단 평판이 높으면 좋은 사람이 모이지만, 그 안에서도 폭이 있다.
            int rep = Math.Clamp(clubRep + rng.Next(-1, 2), 1, 5);

            // 나라를 골라 그 나라 이름을 쓴다. 선수와 같은 풀이라야 세계가 한 덩어리로 읽힌다.
            var names = NameForge.All[rng.Next(NameForge.All.Count)];
            string first = names.FirstNames[rng.Next(names.FirstNames.Length)];
            string last  = names.LastNames[rng.Next(names.LastNames.Length)];

            var s = new StaffData
            {
                id = id,
                firstName = first,
                lastName = last,
                age = 32 + rng.Next(28),
                nationality = names.Nationality,
                role = role,
                reputation = rep,
                common = new StaffAttributesCommon
                {
                    motivation = Roll(rep, rng),
                    manManagement = Roll(rep, rng),
                },
            };

            // 역할에 맞는 능력만 채운다. 다 채우면 역할 구분이 장식이 된다.
            if (StaffRoom.IsCoach(role))
                s.coaching = new CoachingAttributes
                {
                    tacticalKnowledge = Roll(rep, rng),
                    technicalCoaching = Roll(rep, rng),
                    physicalCoaching = Roll(rep, rng),
                    mentalCoaching = Roll(rep, rng),
                    gkCoaching = role == StaffRole.GoalkeeperCoach ? Roll(rep + 1, rng) : Roll(rep - 2, rng),
                };

            if (StaffRoom.IsScout(role))
                s.scouting = new ScoutingAttributes
                {
                    judgingPlayerAbility = Roll(rep, rng),
                    judgingPlayerPotential = Roll(rep, rng),
                    adaptability = Roll(rep, rng),
                };

            if (StaffRoom.IsMedical(role))
                s.medical = new MedicalAttributes
                {
                    physioTherapy = Roll(rep, rng),
                    sportsScience = Roll(rep, rng),
                };

            if (StaffRoom.IsAnalyst(role))
                s.analytics = new AnalyticsAttributes { dataAnalysis = Roll(rep, rng) };

            return s;
        }

        /// <summary>1~20. 평판이 중심을 옮기고, 그 둘레로 흩어진다.</summary>
        static int Roll(int reputation, Random rng)
        {
            int centre = 4 + Math.Clamp(reputation, 0, 5) * 3;
            return Math.Clamp(centre + rng.Next(-3, 4), 1, 20);
        }
    }
}
