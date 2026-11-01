using System;
using System.Collections.Generic;
using System.Linq;
using FootballTactician.Balance;
using FootballTactician.Data;
using FootballTactician.Diagnostics;
using FootballTactician.Simulation;

namespace FootballTactician.World
{
    /// <summary>
    /// 한 주를 넘긴다 — 구단 240곳의 훈련·성장·회복·부상·급여, 그리고 이적 창구.
    ///
    /// 라운드 비용의 절반이 이 한 주에서 나온다. 어디가 비싼지 보려면 한 파일에 모여 있어야 한다 —
    /// 구간 표시(<see cref="SimProfile"/>)의 나무 모양이 곧 이 파일의 순서다.
    ///
    /// 진행 엔진 안에 있던 부분이다. 엔진은 "라운드를 돌린다"는 일을 하고,
    /// 주간 처리는 그와 다른 일이다 — 섞어 두니 엔진 한 파일이 1,450줄이 됐다.
    ///
    /// 순서가 뜻을 가진다. 탈의실을 먼저 다시 재고(사기 기준선이 거기서 나온다),
    /// 선수를 돌리고, 부상을 뽑고, 급여를 낸다. 이적 창구는 구단 순회가 끝난 뒤에 열어야
    /// 방금 영입한 선수가 그 주의 성장에 두 번 들어가지 않는다.
    /// </summary>
    public static class WeekCycle
    {
        public static void Advance(GameWorld w, GameDate to)
        {
            using var weekSpan = SimProfile.Measure("주간 진행");

            w.Date = to;
            int weekKey = to.year * 1000 + to.month * 40 + to.day;
            var rng = new Random(SeedMix.Of(w.Seed, weekKey, 991));

            using (SimProfile.Measure("  ├ 스카우트"))
                Scouting.StartWeek(w);

            // 참다 못한 선수가 이적을 요청한다. 구단 순회 앞에 둬 탈의실 갱신과 섞이지 않게 한다.
            using (SimProfile.Measure("  ├ 이적 요청"))
                TransferRequests.Update(w, new Random(SeedMix.Of(w.Seed, weekKey, 3313)));

            using (SimProfile.Measure("  ├ 구단 순회 240"))
            foreach (var club in w.Clubs.Values)
            {
                // 구단마다 독립된 난수를 쓴다. 하나를 공유하면 한 구단의 추첨 횟수가
                // 다음 구단의 흐름을 밀어내, 플레이어의 훈련 선택이 남의 구단 성장까지 바꾼다.
                var clubRng = new Random(SeedMix.Of(w.Seed, weekKey * 131 + club.id, 7717));

                float coach = CoachQuality(w, club);
                float facility = FacilityQuality(club);
                var tac = club.savedTactics.FirstOrDefault(t => t.id == club.activeTacticsId);
                var starters = tac == null
                    ? new HashSet<int>()
                    : tac.slots.Select(s => s.assignedPlayerId).ToHashSet();

                // 훈련 지시는 플레이어 구단만 감독이 정한다. AI 는 기본값으로 돈다.
                var plan = club.id == w.ManagedClubId ? w.Training : DefaultPlan;

                // 평판 계수는 구단마다 하나다. 선수마다 다시 구하면 6,000번 리그를 되찾는다.
                float rep = w.RepCoefOf(club.id);

                // 명단과 계약이 바뀐 뒤의 탈의실을 먼저 다시 잰다. 사기 기준선이 여기서 나온다.
                float moraleAnchor;
                using (SimProfile.Measure("  │ ├ 탈의실"))
                {
                    DressingRoom.Update(w, club);
                    moraleAnchor = MoraleAnchorOverride?.Invoke(club) ?? DressingRoom.MoraleAnchorFor(club);
                }

                using (SimProfile.Measure("  │ ├ 선수 순회 6000"))
                foreach (int pid in club.playerIds)
                {
                    var p = w.Player(pid);
                    if (p == null) continue;

                    int age = p.GetAge(w.Date);
                    bool starting = starters.Contains(pid);
                    // 출전 시간이 시설보다 중요해야 한다. 폭이 좁으면 큰 구단 벤치에 앉혀 두는 쪽이
                    // 임대보다 낫게 나와, 어린 선수를 내보낼 이유가 사라진다.
                    // 2군에서 뛴 선수는 벤치보다 낫다. 같은 값으로 두면 1군에 들 이유가 없다.
                    float minutes = ReserveLeague.MinutesFactor(p, starting);
                    if (plan.YouthPriority && age <= 21) minutes += 0.25f;

                    PlayerDevelopment.ApplyWeek(w, p, clubRng,
                        coach * plan.GrowthFactor * plan.FocusBonusFor(p, age),
                        facility, minutes);

                    RecoverCondition(p, starting, plan.RecoveryFactor);
                    RecoverInjury(w, p);
                    DriftMorale(p, moraleAnchor);
                    // 대표 경력은 몸값에 붙는다. 같은 실력이면 국가대표가 더 비싸다.
                    p.marketValue = (int)(BalanceConfig.Value(
                        GameWorld.Ca(p), p.GetAge(w.Date), rep)
                        * NationalTeam.ValueBonus(p) * 10000f);
                }

                // 의료진이 좋으면 덜 다친다.
                using (SimProfile.Measure("  │ ├ 부상 추첨"))
                    RollInjuries(w, club, clubRng, plan.InjuryFactor * StaffRoom.InjuryFactor(w, club.id));
                using (SimProfile.Measure("  │ └ 급여"))
                    PayWages(w, club);
            }

            using (SimProfile.Measure("  ├ 이적 시장"))
                RunTransferWindow(w, rng);
            using (SimProfile.Measure("  ├ 계약 만료 경고"))
                ContractTalks.WarnExpiring(w);

            using (SimProfile.Measure("  └ 목표·시즌끝"))
            {
                if (w.ManagedClubId > 0) SeasonReview.EvaluateObjectives(w);
                SeasonReview.CheckSeasonEnd(w);
            }
        }

        /// <summary>AI 구단이 쓰는 기본 훈련 지시.</summary>
        static readonly TrainingPlan DefaultPlan = new();

        // 큰 구단의 훈련이 낫긴 하지만 두 배는 아니다. 격차가 크면 큰 구단 벤치에 앉혀 두는 쪽이
        // 하위 구단에서 매주 뛰는 것보다 낫게 나와, 임대라는 선택이 사라진다.
        // 시설과 평판이 바탕을 깔고, 코치진이 그 위를 올리거나 내린다.
        static float CoachQuality(GameWorld w, ClubData c)
            => (0.80f + c.trainingFacilitiesRating * 0.07f + c.reputation * 0.05f)
             * StaffRoom.CoachingFactor(w, c.id);

        static float FacilityQuality(ClubData c) => 0.85f + c.trainingFacilitiesRating * 0.05f;

        /// <summary>
        /// 회복은 남은 폭에 비례한다. 고정값으로 두면 시즌 내내 바닥으로 흐르거나 늘 100 이 된다.
        /// 컨디션은 골키퍼 전력에도 곱해지므로, 바닥으로 흐르면 시즌 후반 득점이 부풀어 오른다.
        /// </summary>
        static void RecoverCondition(PlayerData p, bool played, float trainingFactor = 1f)
        {
            float rate = ((played ? 0.35f : 0.50f) + p.hidden.recoveryRate / 20f * 0.25f) * trainingFactor;
            rate = Math.Clamp(rate, 0.05f, 0.95f);
            p.condition += (100f - p.condition) * rate;
            p.condition = Math.Clamp(p.condition, 0f, 100f);
        }

        /// <summary>
        /// 측정용. 분위기만 따로 떼어 성적에 미치는 영향을 재려고 둔다.
        /// 게임에서는 null 이고, 하네스가 잠깐 끼워 넣었다가 되돌린다.
        /// </summary>
        public static Func<ClubData, float> MoraleAnchorOverride;

        static void DriftMorale(PlayerData p, float anchor)
        {
            // 사기는 가만히 두면 기준선으로 돌아간다. 복원이 약하면 연패가 회복 불가능해진다.
            // 기준선은 탈의실 분위기가 정한다 — 불만이 쌓인 팀은 낮은 곳으로 돌아간다.
            p.morale += (anchor - p.morale) * 0.20f;
        }

        static void RecoverInjury(GameWorld w, PlayerData p)
        {
            if (p.injury == null || !p.injury.isActive) return;
            if (w.Date >= p.injury.expectedReturnDate)
            {
                p.injury.isActive = false;
                p.status = PlayerStatus.Active;
                p.condition = Math.Min(p.condition, 70f);
            }
        }

        static readonly (string name, InjurySeverity sev, int weeksMin, int weeksMax)[] InjuryTable =
        {
            ("타박상",       InjurySeverity.Minor,    1,  2),
            ("햄스트링 염좌", InjurySeverity.Moderate, 3,  6),
            ("발목 인대",     InjurySeverity.Moderate, 4,  8),
            ("무릎 연골",     InjurySeverity.Serious,  10, 20),
            ("피로 골절",     InjurySeverity.Serious,  8,  16),
        };

        static void RollInjuries(GameWorld w, ClubData club, Random rng, float trainingFactor = 1f)
        {
            foreach (int pid in club.playerIds)
            {
                var p = w.Player(pid);
                if (p == null || p.IsInjured) continue;

                // 주당 기본 1.2%, 부상 성향과 피로가 올린다.
                float risk = 0.012f
                           + p.hidden.injuryProneness / 20f * 0.018f
                           + Math.Max(0f, (70f - p.condition) / 70f) * 0.02f;
                risk *= trainingFactor;

                if (rng.NextDouble() >= risk) continue;

                var e = InjuryTable[rng.Next(InjuryTable.Length)];
                // 의료진이 좋으면 빨리 돌아온다. 최소 1주는 둔다.
                int weeks = Math.Max(1, (int)MathF.Round(
                    rng.Next(e.weeksMin, e.weeksMax + 1) * StaffRoom.RecoveryFactor(w, club.id)));
                p.injury = new InjuryData
                {
                    injuryType = e.name,
                    severity = e.sev,
                    injuryDate = w.Date,
                    expectedReturnDate = w.Date.AddDays(weeks * 7),
                    isActive = true,
                };
                p.status = PlayerStatus.Injured;

                if (club.id == w.ManagedClubId)
                    w.Post($"{p.FullName} 부상",
                           $"{e.name}. 복귀까지 약 {weeks}주로 보입니다.", InboxKind.Injury);
            }
        }

        static void PayWages(GameWorld w, ClubData club)
        {
            // 선수와 스태프 둘 다 나간다. 예전에는 선수만 나가서, 좋은 스태프를
            // 아무리 모아도 장부가 꿈쩍하지 않았다.
            long wages = ClubWages.Of(w, club);
            long income = club.finances.annualRevenue / 52;

            // 팬이 등을 돌리면 입장 수입이 줄고, 돌아서면 는다. 내 구단만 해당한다 —
            // 기자회견은 플레이어만 치르므로 AI 구단에 걸면 아무 근거 없는 편차가 된다.
            if (club.id == w.ManagedClubId)
                income = (long)(income * PressRoom.RevenueMultiplier(w.Manager));

            // 급여 외 운영비 — 경기장, 스탭, 유스, 원정, 세금.
            // 이게 없으면 구단이 수입의 절반씩 영원히 쌓아 몇 시즌 만에 자금이 수백 M€ 가 된다.
            long overhead = (long)(club.finances.annualRevenue * BalanceConfig.OverheadRatio / 52);

            club.finances.currentWageSpend = (int)Math.Min(int.MaxValue, wages);
            club.finances.annualExpenditure = (long)(wages * 52 + club.finances.annualRevenue * BalanceConfig.OverheadRatio);
            club.finances.balance += income - wages - overhead;

            float ratio = club.finances.annualRevenue == 0
                ? 0f
                : wages * 52f / club.finances.annualRevenue;

            club.finances.status =
                club.finances.balance < 0 ? FinancialStatus.Crisis
              : ratio > BalanceConfig.WageSanctionRatio ? FinancialStatus.Crisis
              : ratio > BalanceConfig.WageWarnRatio ? FinancialStatus.Tight
              : club.reputation >= 4 ? FinancialStatus.Excellent
              : FinancialStatus.Stable;
        }

        /// <summary>
        /// 창구가 열려 있으면 AI 구단끼리 이적을 진행한다.
        /// 세계가 멈춰 있으면 플레이어만 전력을 키우게 되고 리그 균형이 무너진다.
        /// </summary>
        static void RunTransferWindow(GameWorld w, Random rng)
        {
            if (!TransferMarket.IsWindowOpen(w))
            {
                w.TransferListsReady = false;     // 다음 창구가 열리면 목록을 다시 정리한다
                return;
            }

            if (!w.TransferListsReady)
            {
                using (SimProfile.Measure("  │ ├ 이적 목록 정리"))
                    TransferMarket.UpdateTransferLists(w, rng);
                w.TransferListsReady = true;
            }

            int done;
            using (SimProfile.Measure("  │ ├ AI 영입"))
                done = TransferMarket.RunAiWindow(w, rng);
            using (SimProfile.Measure("  │ └ AI 임대"))
                LoanMarket.RunAiLoans(w, rng);

            if (done > 0 && w.ManagedClubId > 0)
            {
                var notable = w.Transfers.Take(done)
                    .OrderByDescending(t => t.fee)
                    .FirstOrDefault();
                if (notable != null && notable.fee > 15_000_000)
                    w.Post("이적 소식",
                           $"{notable.playerName} 이 {w.Club(notable.fromClubId)?.name} 에서 " +
                           $"{w.Club(notable.toClubId)?.name} 으로 이적했습니다. " +
                           $"이적료 {notable.fee / 1_000_000d:F1}M€.",
                           InboxKind.Transfer);
            }
        }
    }
}
