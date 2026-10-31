using System;
using System.Collections.Generic;
using System.Linq;
using FootballTactician.Balance;
using FootballTactician.Data;

namespace FootballTactician.World
{
    /// <summary>
    /// 스태프 — 코치진·스카우트·의료진·분석가.
    ///
    /// <c>StaffData</c> 와 능력치는 처음부터 있었지만 아무도 읽지 않았다. 훈련 효율은
    /// 시설과 평판에서만 나왔고, 스카우트 보고서의 질은 누가 보든 같았다.
    ///
    /// 네 갈래로 갈라 붙인다. 하나가 다 하면 "좋은 스태프"를 모으는 것 말고 할 일이 없다.
    ///
    /// <list type="bullet">
    /// <item>코칭 — 선수 성장</item>
    /// <item>스카우팅 — 보고서의 질과 횟수</item>
    /// <item>의료 — 부상 빈도와 회복</item>
    /// <item>분석 — 상대 정보</item>
    /// </list>
    ///
    /// <b>주급은 선수와 같은 지갑에서 나간다.</b> 이것이 이 시스템의 유일한 대가다.
    /// 따로 떼어 놓으면 좋은 스태프를 안 쓸 이유가 없어진다.
    /// </summary>
    public static class StaffRoom
    {
        // ── 분류 ──────────────────────────────────────────────────────────

        public static bool IsCoach(StaffRole r) => r is StaffRole.HeadCoach or StaffRole.AssistantCoach
                                                     or StaffRole.PhysicalCoach or StaffRole.GoalkeeperCoach
                                                     or StaffRole.YouthCoordinator;

        public static bool IsScout(StaffRole r) => r is StaffRole.HeadScout or StaffRole.RegionalScout;

        public static bool IsMedical(StaffRole r) => r is StaffRole.TeamDoctor or StaffRole.Physio;

        public static bool IsAnalyst(StaffRole r) => r is StaffRole.DataAnalyst;

        public static IEnumerable<StaffData> Of(GameWorld w, int clubId)
        {
            var club = w.Club(clubId);
            if (club?.staffIds == null) yield break;

            foreach (int id in club.staffIds)
                if (w.Staff.TryGetValue(id, out var s)) yield return s;
        }

        // ── 네 갈래의 힘 ──────────────────────────────────────────────────

        /// <summary>
        /// 한 갈래의 힘. 0 이면 아무도 없는 것, 1 이면 그 평판대에서 기대되는 만큼.
        ///
        /// 1 을 기준으로 삼는 것이 중요하다. 절대 능력치를 그대로 쓰면 큰 구단이
        /// 모든 갈래에서 앞서고, 스태프 운용이 평판의 그림자가 된다.
        /// </summary>
        static float Strength(GameWorld w, int clubId, Func<StaffRole, bool> pick, Func<StaffData, int> rate)
        {
            var staff = Of(w, clubId).Where(s => pick(s.role)).ToList();
            if (staff.Count == 0) return 0f;

            // 기준은 그 평판 구단이 실제로 두는 인원이다 — 적합도에서 두 번 당한 것과 같은 자리다.
            //
            // 고정된 기준을 쓰면 큰 구단이 네 갈래 모두에서 앞서고 스태프 운용이 평판의 그림자가 된다.
            // "자리를 다 채웠을 때"를 기준으로 삼아도 안 된다. 갈래마다 자리 수가 다르고
            // (분석가는 하나, 코치는 다섯) 작은 구단은 그마저 다 못 채우는데,
            // 3부까지 있는 세계에서는 그런 구단이 다수라 세계 전체의 부상이 밀려 올라간다.
            int rep = w.Club(clubId)?.reputation ?? 3;
            float par = ParFor(rep) * HeadWeight(StaffPlan.TypicalHeads(rep, pick));

            // 머릿수는 거들 뿐이다. 제일 나은 한 명이 대부분을 정한다 —
            // 평범한 코치를 열 명 두는 쪽이 낫다면 영입이 숫자 싸움이 된다.
            var sorted = staff.Select(rate).OrderByDescending(x => x).ToList();

            float score = sorted[0];
            for (int i = 1; i < sorted.Count; i++)
                score += sorted[i] * BalanceConfig.StaffExtraWeight / i;

            return score / par;
        }

        /// <summary>
        /// 그 평판대 스태프 한 명의 기대 점수.
        ///
        /// 구단 평판을 그대로 쓰면 안 된다. 스태프 평판은 구단 평판 ±1 로 뽑고 1~5 로 자르는데,
        /// 1성은 아래쪽이, 5성은 위쪽이 잘려 기대값이 각각 위아래로 밀린다.
        /// 그 밀림을 기준점에도 똑같이 먹이지 않으면 작은 구단의 분석가가
        /// 큰 구단의 분석가보다 좋아 보이는 역전이 생긴다.
        /// </summary>
        static float ParFor(int clubReputation)
        {
            int r = Math.Clamp(clubReputation, 1, 5);
            float expected = (Math.Clamp(r - 1, 1, 5) + r + Math.Clamp(r + 1, 1, 5)) / 3f;
            return BalanceConfig.StaffParBase + expected * BalanceConfig.StaffParPerStar;
        }

        /// <summary>자리가 n 개인 갈래를 다 채웠을 때 쌓이는 배수. Strength 의 합산과 같은 식이다.</summary>
        static float HeadWeight(int heads)
        {
            float wsum = 1f;
            for (int i = 1; i < heads; i++) wsum += BalanceConfig.StaffExtraWeight / i;
            return wsum;
        }

        public static float Coaching(GameWorld w, int clubId) => Strength(w, clubId, IsCoach, s =>
            (s.coaching?.tacticalKnowledge ?? 0) + (s.coaching?.technicalCoaching ?? 0)
          + (s.coaching?.mentalCoaching ?? 0) + (s.common?.motivation ?? 0));

        public static float ScoutingPower(GameWorld w, int clubId) => Strength(w, clubId, IsScout, s =>
            (s.scouting?.judgingPlayerAbility ?? 0) * 2 + (s.scouting?.judgingPlayerPotential ?? 0)
          + (s.scouting?.adaptability ?? 0));

        public static float Medical(GameWorld w, int clubId) => Strength(w, clubId, IsMedical, s =>
            (s.medical?.physioTherapy ?? 0) * 2 + (s.medical?.sportsScience ?? 0) * 2);

        public static float Analytics(GameWorld w, int clubId) => Strength(w, clubId, IsAnalyst, s =>
            (s.analytics?.dataAnalysis ?? 0) * 3 + (s.common?.manManagement ?? 0));

        // ── 효과 ──────────────────────────────────────────────────────────

        /// <summary>
        /// 훈련 배수. 스태프가 없으면 1 보다 낮고, 평판대로 갖추면 1 언저리다.
        /// 한도를 두지 않으면 돈 많은 구단이 코치만 모아 성장을 독식한다.
        /// </summary>
        public static float CoachingFactor(GameWorld w, int clubId)
            => 1f + Math.Clamp((Coaching(w, clubId) - 1f) * BalanceConfig.CoachGrowthSwing,
                               -BalanceConfig.CoachGrowthCap, BalanceConfig.CoachGrowthCap);

        /// <summary>부상 확률 배수. 좋은 의료진은 덜 다치게 한다.</summary>
        public static float InjuryFactor(GameWorld w, int clubId)
            => 1f - Math.Clamp((Medical(w, clubId) - 1f) * BalanceConfig.MedicalInjurySwing,
                               -BalanceConfig.MedicalInjuryCap, BalanceConfig.MedicalInjuryCap);

        /// <summary>회복 주수 배수. 1 보다 작으면 빨리 돌아온다.</summary>
        public static float RecoveryFactor(GameWorld w, int clubId)
            => 1f - Math.Clamp((Medical(w, clubId) - 1f) * BalanceConfig.MedicalRecoverySwing,
                               -BalanceConfig.MedicalRecoveryCap, BalanceConfig.MedicalRecoveryCap);

        /// <summary>
        /// 보고서 한 번이 얼마나 많이 알려 주는지. 좋은 스카우트는 한 번에 더 좁힌다.
        /// 스카우트가 없으면 보고서가 거의 쓸모없다 — 관찰은 사람이 하는 일이다.
        /// </summary>
        public static float ReportValue(GameWorld w, int clubId)
            => Math.Clamp(BalanceConfig.ReportValueFloor
                        + ScoutingPower(w, clubId) * BalanceConfig.ReportValuePerPower,
                          BalanceConfig.ReportValueFloor, BalanceConfig.ReportValueCap);

        /// <summary>주당 관찰 횟수에 붙는 덤.</summary>
        public static int ExtraScoutTrips(GameWorld w, int clubId)
            => (int)(ScoutingPower(w, clubId) * BalanceConfig.ScoutTripsPerPower);

        /// <summary>분석가가 있으면 상대 전술 색깔을 읽어 낸다.</summary>
        public static bool ReadsOpponentStyle(GameWorld w, int clubId)
            => Analytics(w, clubId) >= BalanceConfig.AnalystStyleThreshold;

        // ── 주급 ──────────────────────────────────────────────────────────

        public static int WeeklyWage(StaffData s) => s.contract?.weeklyWage ?? 0;

        public static int WageBill(GameWorld w, int clubId) => Of(w, clubId).Sum(WeeklyWage);

        /// <summary>데려오는 데 드는 주급. 평판과 능력이 함께 값을 올린다.</summary>
        public static int AskingWage(StaffData s)
        {
            int rate = s.role switch
            {
                StaffRole.HeadCoach => BalanceConfig.HeadCoachWageBase,
                StaffRole.HeadScout or StaffRole.TeamDoctor => BalanceConfig.SeniorStaffWageBase,
                _ => BalanceConfig.StaffWageBase,
            };

            return (int)(rate * (1f + s.reputation * BalanceConfig.StaffWagePerStar));
        }

        // ── 고용 ──────────────────────────────────────────────────────────

        public enum HireVerdict { Ok, NoBudget, RoleFull, NotAvailable }

        /// <summary>한 역할에 몇 명까지 둘 수 있는지. 없으면 코치를 무한히 쌓는다.</summary>
        public static int SlotsFor(StaffRole role) => role switch
        {
            StaffRole.HeadCoach or StaffRole.HeadScout or StaffRole.TeamDoctor
                or StaffRole.YouthCoordinator or StaffRole.DataAnalyst => 1,
            _ => 2,
        };

        public static HireVerdict CanHire(GameWorld w, ClubData club, StaffData s)
        {
            if (s.clubId == club.id) return HireVerdict.NotAvailable;
            if (s.clubId > 0) return HireVerdict.NotAvailable;   // 남의 스태프는 데려올 수 없다

            if (Of(w, club.id).Count(x => x.role == s.role) >= SlotsFor(s.role))
                return HireVerdict.RoleFull;

            int after = club.finances.currentWageSpend + AskingWage(s);
            if (after > club.finances.weeklyWageBudget) return HireVerdict.NoBudget;

            return HireVerdict.Ok;
        }

        public static bool Hire(GameWorld w, ClubData club, StaffData s)
        {
            if (CanHire(w, club, s) != HireVerdict.Ok) return false;

            s.contract ??= new ContractData();
            s.contract.weeklyWage = AskingWage(s);
            s.contract.expiryDate = w.Date.AddDays(365 * 2);
            s.clubId = club.id;

            club.staffIds ??= new List<int>();
            club.staffIds.Add(s.id);
            club.finances.currentWageSpend += s.contract.weeklyWage;

            if (club.id == w.ManagedClubId)
                w.Post($"{s.FullName} 합류",
                       $"{RoleLabel(s.role)}로 계약했습니다. 주급 {s.contract.weeklyWage:N0}€.", InboxKind.Staff);

            return true;
        }

        public static bool Release(GameWorld w, ClubData club, StaffData s)
        {
            if (club.staffIds == null || !club.staffIds.Remove(s.id)) return false;

            club.finances.currentWageSpend -= WeeklyWage(s);
            s.clubId = 0;
            s.contract = null;

            if (club.id == w.ManagedClubId)
                w.Post($"{s.FullName} 계약 해지", $"{RoleLabel(s.role)} 자리가 비었습니다.", InboxKind.Staff);

            return true;
        }

        /// <summary>구할 수 있는 스태프. 어느 구단에도 없는 사람들.</summary>
        public static IEnumerable<StaffData> FreeAgents(GameWorld w, StaffRole? role = null)
            => w.Staff.Values
                .Where(s => s.clubId <= 0)
                .Where(s => role == null || s.role == role)
                .OrderByDescending(s => s.reputation);

        public static string RoleLabel(StaffRole r) => r switch
        {
            StaffRole.HeadCoach => "수석 코치",
            StaffRole.AssistantCoach => "코치",
            StaffRole.PhysicalCoach => "피지컬 코치",
            StaffRole.GoalkeeperCoach => "골키퍼 코치",
            StaffRole.YouthCoordinator => "유스 책임자",
            StaffRole.HeadScout => "수석 스카우트",
            StaffRole.RegionalScout => "스카우트",
            StaffRole.DataAnalyst => "분석가",
            StaffRole.TeamDoctor => "팀 닥터",
            StaffRole.Physio => "물리치료사",
            _ => r.ToString(),
        };

        public static string GroupLabel(StaffRole r)
            => IsCoach(r) ? "코칭" : IsScout(r) ? "스카우팅" : IsMedical(r) ? "의료" : "분석";
    }
}
