using System;
using System.Collections.Generic;
using System.Linq;
using FootballTactician.Balance;
using FootballTactician.Data;

namespace FootballTactician.World
{
    /// <summary>
    /// 경기장과 스폰서.
    ///
    /// <c>StadiumData</c> 와 <c>SponsorContract</c> 는 처음부터 있었지만 아무도 읽지 않았다.
    /// 수입은 평판에서만 나왔고, 경기장을 아무리 키워도, 성적을 아무리 내도 한 푼도 늘지 않았다.
    ///
    /// 그래서 잘해도 구단이 커지는 느낌이 없었다 — 평판이 오를 때만 계단처럼 뛰었다.
    ///
    /// 두 가지를 붙인다. <b>둘 다 감독이 한 일의 결과여야 한다.</b>
    /// 저절로 불어나면 재정이 시간의 함수가 되고, 아무 판단도 들어가지 않는다.
    /// </summary>
    public static class ClubBusiness
    {
        // ── 수입 ──────────────────────────────────────────────────────────

        /// <summary>
        /// 평판이 깔아 놓은 바탕 수입에 경기장과 스폰서가 얹힌다.
        ///
        /// 배수로 둔다. 더하기로 두면 작은 구단에서는 경기장이 수입의 절반이 되고
        /// 큰 구단에서는 반올림 오차가 된다.
        /// </summary>
        public static float RevenueMultiplier(ClubData club)
            => StadiumFactor(club) * SponsorFactor(club);

        /// <summary>
        /// 관중 수입. 좌석이 바탕이고 분위기가 조금 거든다.
        /// 기준 좌석에서 1.0 이 되게 맞춘다 — 그래야 경기장을 키우지 않은 구단이 손해를 보지 않는다.
        /// </summary>
        public static float StadiumFactor(ClubData club)
        {
            var s = club.stadium;
            if (s == null || s.capacity <= 0) return 1f;

            float seats = s.capacity / (float)ExpectedCapacity(club.reputation);
            // 기준은 그 평판대의 생성값이다. 세계 평균을 쓰면 별점 분포에 따라
            // 모든 구단이 함께 덤을 받거나 함께 벌을 받는다 — 스태프에서 겪은 자리와 같다.
            float mood = 1f + (s.atmosphereRating - ExpectedAtmosphere(club.reputation)) / 100f
                            * BalanceConfig.AtmosphereRevenueSwing;

            return Math.Clamp(1f + (seats - 1f) * BalanceConfig.StadiumRevenueShare, 0.80f, 1.45f) * mood;
        }

        /// <summary>그 평판대에서 보통 갖는 좌석 수. 세계 생성과 같은 식이라야 기준이 맞는다.</summary>
        public static int ExpectedCapacity(int reputation)
            => 8000 + reputation * reputation * 2200 + 2000;

        /// <summary>그 평판대의 보통 분위기. 생성식 45 + 별×9 + rng(0..7) 의 기대값이다.</summary>
        public static float ExpectedAtmosphere(int reputation)
            => 45f + reputation * 9f + 3.5f;

        /// <summary>
        /// 스폰서가 수입을 얼마나 바꾸는가.
        ///
        /// 금액을 그대로 더하면 안 된다. 결국 모든 구단이 스폰서를 갖게 되므로
        /// 세계 전체의 수입이 통째로 밀려 올라간다 — 실제로 8시즌 만에 7% 가 밀렸다.
        /// 재야 할 것은 "스폰서가 있는가" 가 아니라 <b>"보통보다 좋은 계약인가"</b> 다.
        /// </summary>
        public static float SponsorFactor(ClubData club)
        {
            var sp = club.finances?.mainSponsor;
            if (sp == null || sp.annualFee <= 0) return 1f;

            float typical = Math.Max(1f, BaseRevenue(club) * BalanceConfig.SponsorBaseShare);
            float lean = (sp.annualFee - typical) / typical;

            return 1f + Math.Clamp(lean * BalanceConfig.SponsorRevenueShare,
                                   -BalanceConfig.SponsorRevenueCap, BalanceConfig.SponsorRevenueCap);
        }

        /// <summary>
        /// 배수를 먹이기 전의 수입. 스폰서 금액을 실제 수입에서 뽑으면
        /// 수입 → 스폰서 → 배수 → 수입 으로 되먹임이 생겨 기준선이 천천히 밀린다.
        /// </summary>
        static float BaseRevenue(ClubData club)
            => BalanceConfig.AnnualRevenue(GameWorld.RepCoef(club.reputation)) * 10000f;

        // ── 경기장 ────────────────────────────────────────────────────────

        public enum BuildVerdict { Ok, AtMax, NoFunds }

        public static int ExpansionSeats(ClubData club)
            => Math.Max(2000, (int)(club.stadium.capacity * BalanceConfig.StadiumExpandShare));

        public static long ExpansionCost(ClubData club)
            => (long)(ExpansionSeats(club) * BalanceConfig.StadiumCostPerSeat);

        public static BuildVerdict CanExpand(ClubData club)
        {
            if (club.stadium == null) return BuildVerdict.AtMax;
            if (club.stadium.capacity >= BalanceConfig.StadiumMaxCapacity) return BuildVerdict.AtMax;
            if (club.finances.balance < ExpansionCost(club)) return BuildVerdict.NoFunds;
            return BuildVerdict.Ok;
        }

        /// <summary>
        /// 좌석을 늘린다. 돈이 바로 나가고 수입은 다음 시즌부터 들어온다 —
        /// 그래야 "지금 선수를 살 것인가, 몇 해 뒤를 살 것인가"가 판단이 된다.
        /// </summary>
        public static bool Expand(GameWorld w, ClubData club)
        {
            if (CanExpand(club) != BuildVerdict.Ok) return false;

            int seats = ExpansionSeats(club);
            long cost = ExpansionCost(club);

            club.finances.balance -= cost;
            club.stadium.capacity += seats;

            if (club.id == w.ManagedClubId)
                w.Post("경기장 증축",
                       $"{seats:N0}석을 더해 {club.stadium.capacity:N0}석이 됩니다. "
                     + $"{cost / 1_000_000.0:F1}M€ 가 나갔고, 수입은 다음 시즌부터 늘어납니다.",
                       InboxKind.Board);

            return true;
        }

        /// <summary>
        /// 구단이 커지면 경기장도 따라 자란다.
        ///
        /// 없으면 승격으로 평판만 오르고 경기장은 생성값에 묶여,
        /// 기준(그 평판대의 보통 좌석)에서 해마다 멀어진다 — 실제로 좌석비가
        /// 여섯 시즌 만에 0.95 에서 0.90 으로 내려가 세계 수입이 4% 밀렸다.
        ///
        /// 간극의 일부만 메운다. 한 번에 따라잡으면 감독이 직접 증축할 이유가 없다.
        /// </summary>
        public static void DriftTowardExpected(ClubData club)
        {
            var s = club.stadium;
            if (s == null) return;

            int want = ExpectedCapacity(club.reputation);
            if (s.capacity < want)
                s.capacity += Math.Max(500, (int)((want - s.capacity) * BalanceConfig.StadiumDriftShare));

            float moodWant = ExpectedAtmosphere(club.reputation);
            s.atmosphereRating += (moodWant - s.atmosphereRating) * BalanceConfig.AtmosphereDriftShare;
        }

        // ── 스폰서 ────────────────────────────────────────────────────────

        /// <summary>
        /// 스폰서 계약을 새로 맺는다. 금액은 평판과 <b>최근 성적</b>이 정한다 —
        /// 평판만 보면 감독이 한 일이 값에 들어가지 않는다.
        /// </summary>
        public static SponsorContract Negotiate(GameWorld w, ClubData club, Random rng)
        {
            float form = RecentForm(club);

            long fee = (long)(BaseRevenue(club)
                            * BalanceConfig.SponsorBaseShare
                            * (1f + form * BalanceConfig.SponsorFormSwing));

            return new SponsorContract
            {
                sponsorName = SponsorForge.Name(club, rng),
                annualFee = (int)Math.Clamp(fee, 0, int.MaxValue),
                expiryDate = w.Date.AddDays(365 * BalanceConfig.SponsorYears),
                performanceBonus = (int)(fee * BalanceConfig.SponsorBonusShare),
            };
        }

        /// <summary>최근 시즌들의 순위를 -1 ~ +1 로. 상위권이면 양수.</summary>
        public static float RecentForm(ClubData club)
        {
            var recent = club.history?.recentSeasons;
            if (recent == null || recent.Count == 0) return 0f;

            var last = recent.TakeLast(BalanceConfig.SponsorFormSeasons).ToList();
            float avgPlace = (float)last.Average(s => s.finalPosition);

            // 20팀 리그에서 1위면 +1, 20위면 -1.
            return Math.Clamp(1f - (avgPlace - 1f) / 9.5f, -1f, 1f);
        }

        /// <summary>계약이 끝난 구단에 새 스폰서를 붙인다. 시즌 넘김에서 돈다.</summary>
        public static void RenewExpired(GameWorld w, Random rng)
        {
            foreach (var club in w.Clubs.Values)
            {
                var sp = club.finances?.mainSponsor;
                if (sp != null && sp.expiryDate > w.Date) continue;

                var fresh = Negotiate(w, club, rng);
                club.finances.mainSponsor = fresh;

                if (club.id == w.ManagedClubId)
                    w.Post("새 스폰서 계약",
                           $"{fresh.sponsorName} 와 연 {fresh.annualFee / 1_000_000.0:F1}M€ 에 "
                         + $"{BalanceConfig.SponsorYears}년 계약했습니다.",
                           InboxKind.Board);
            }
        }
    }

    /// <summary>스폰서 이름. 실존 기업과 겹치지 않도록 지어낸 조각을 엮는다.</summary>
    static class SponsorForge
    {
        static readonly string[] Heads =
        {
            "노르딕", "벨마르", "할브", "소란", "아우렌", "키르스", "벤토", "모르가",
            "레이든", "팔드", "스카이", "티렌", "오스베", "그란디", "벨루토", "카스텔",
        };

        static readonly string[] Tails =
        {
            "에너지", "보험", "텔레콤", "항공", "은행", "자동차", "식품", "물류",
            "건설", "해운", "전자", "화학", "제약", "리조트", "미디어", "스포츠",
        };

        public static string Name(ClubData club, Random rng)
        {
            // 구단마다 고정되게 섞는다. 해마다 이름이 춤추면 계약이 아니라 잡음이다.
            int h = club.id * 2654435761u > int.MaxValue ? club.id * 31 : club.id * 17;
            return $"{Heads[Math.Abs(h + rng.Next(3)) % Heads.Length]} "
                 + $"{Tails[Math.Abs(h * 7 + rng.Next(3)) % Tails.Length]}";
        }
    }
}
