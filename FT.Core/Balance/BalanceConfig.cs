namespace FootballTactician.Balance
{
    /// <summary>
    /// 밸런스 수치의 단일 출처. 코드 곳곳에 상수를 흩어놓지 않는다.
    /// 값은 FT.Sim 측정으로 정했고, FT.Core.Tests 가 회귀를 막는다.
    /// </summary>
    public static class BalanceConfig
    {
        // ── 성장 ──────────────────────────────────────────────────────────
        /// <summary>주간 CA 변화 기본 계수. 0.6~1.2 구간을 20시즌 돌려 드리프트가 가장 작은 값.</summary>
        public const float GrowthBase = 0.6f;

        /// <summary>주간 변화 상한. 현재 계수에서는 작동하지 않으며, 극단 조합을 막는 안전장치.</summary>
        public const float GrowthWeeklyCap = 0.8f;

        public const int WeeksPerSeason = 40;

        /// <summary>나이별 성장 계수. 31세부터 음수로 돌아선다.</summary>
        public static float AgeGrowth(int age)
        {
            if (age <= 17) return 1.6f;
            if (age <= 21) return 1.4f;
            if (age <= 24) return 1.0f;
            if (age <= 27) return 0.6f;
            if (age <= 30) return 0.25f;
            if (age <= 33) return -0.10f;
            return -0.30f;
        }

        /// <summary>
        /// 나이별 잠재력 실현율. 선수 생성에 쓴다.
        /// CA 를 먼저 뽑으면 PA 가 뒤따라오므로, PA 를 먼저 뽑고 이 비율로 CA 를 유도한다.
        /// </summary>
        public static float Realized(int age)
        {
            if (age <= 16) return 0.52f;
            if (age <= 29)
            {
                float t = (age - 16) / 13f;
                return 0.52f + (0.90f - 0.52f) * (1f - MathF.Pow(1f - t, 1.7f));
            }
            float v = 0.90f - (age - 29) * 0.025f;
            return v < 0.70f ? 0.70f : v;
        }

        /// <summary>주간 CA 변화량. gap 은 (PA-CA)/PA, 하락 구간에서는 1 로 둔다.</summary>
        public static float WeeklyGrowth(
            int age, float ca, float pa,
            float coach, float facility, float professionalism, float minutes)
        {
            float g   = AgeGrowth(age);
            float gap = g < 0f ? 1f : MathF.Max(0f, (pa - ca) / pa);
            float w   = GrowthBase * g * coach * facility * professionalism * minutes * gap;
            return w > GrowthWeeklyCap ? GrowthWeeklyCap : w;
        }

        // ── 가치와 주급 ───────────────────────────────────────────────────
        /// <summary>나이별 가치 계수.</summary>
        public static float AgeValue(int age)
        {
            if (age <= 21) return 1.30f;
            if (age <= 26) return 1.00f;
            if (age <= 29) return 0.75f;
            if (age <= 32) return 0.45f;
            return 0.20f;
        }

        /// <summary>선수 가치(만 유로). CA 에 대해 지수적으로 오른다.</summary>
        public static float Value(float ca, int age, float leagueCoef)
            => MathF.Pow(ca / 100f, 4.5f) * 800f * AgeValue(age) * leagueCoef;

        /// <summary>주급(유로). 계수는 급여 비중이 30~65% 에 들도록 맞춘 값.</summary>
        public const float WageCoef = 0.0032f;

        public static float WeeklyWage(float ca, int age, float leagueCoef)
            => Value(ca, age, leagueCoef) * 10000f * WageCoef;

        // ── 감독 ─────────────────────────────────────────────────────────
        /// <summary>
        /// 새 감독의 평판(0~100). 맡을 수 있는 구단의 수준을 정한다.
        /// 커리어 쪽에 상수로 두고 있었는데, 세계가 그 값을 쓰려고 커리어 모듈을
        /// 되참조하게 됐다. 수치는 전부 여기 있어야 한다.
        /// </summary>
        public const int ManagerStartingReputation = 20;

        // ── 구단 재정 ─────────────────────────────────────────────────────
        /// <summary>연 수입(만 유로). 평판에 선형으로 두면 상위 구단이 선수를 감당하지 못한다.</summary>
        public const float RevenueBase = 8000f;
        public const float RevenueExponent = 3.4f;

        public static float AnnualRevenue(float reputation)
            => RevenueBase * MathF.Pow(reputation, RevenueExponent);

        public const float WageWarnRatio = 0.60f;
        public const float WageSanctionRatio = 0.70f;

        /// <summary>
        /// AI 구단이 스스로 지키는 급여 비중 상한. 제재선보다 낮아야 한다.
        /// 제재선까지 꽉 채우면 리그 전체가 한계선에 눌러앉아 시장이 멈춘다.
        /// </summary>
        public const float AiWageCeiling = 0.58f;

        /// <summary>
        /// 급여를 뺀 연간 운영비가 수입에서 차지하는 비율. 경기장·스탭·유스·원정·세금.
        /// 이것이 없으면 구단이 수입의 절반을 매년 쌓아 몇 시즌 만에 자금이 수백 M€ 가 된다.
        ///
        /// 제재선(급여 비중 상한)과 더해 1.0 을 넘으면 안 된다. 넘으면 규정을 다 지킨 구단도
        /// 구조적으로 적자가 되어 영영 회복하지 못한다. 0.28 + 0.70 = 0.98 로 여유를 둔다.
        /// </summary>
        /// 스태프 주급은 2026-10-29 에 이 묶음에서 빠져나와 급여 지출로 명시됐다.
        /// 둘 다에 넣으면 같은 돈을 두 번 센다.
        public const float OverheadRatio = 0.255f;

        /// <summary>
        /// 급여 예산에서 스태프 몫으로 따로 잡아 주는 비율.
        ///
        /// 없으면 구단이 시작부터 예산을 꽉 채우고 있어 스태프를 한 명도 데려올 수 없다 —
        /// 기능이 화면에만 있고 죽는다. 이 몫을 넘겨 쓰면 그때부터 선수 몫을 깎는다.
        /// </summary>
        public const float StaffWageAllowance = 0.045f;

        /// <summary>
        /// 시즌 이적 예산 = 수입 × 이 비율 + 자금 × BudgetFromBalance. 상한은 보유 자금.
        /// 수입만으로 잡으면 스쿼드가 강해졌을 때 아무도 더 나은 선수를 살 수 없어 시장이 포화된다.
        /// </summary>
        public const float TransferBudgetRatio = 0.30f;

        /// <summary>쌓아 둔 자금 중 이적에 쓸 수 있는 몫.</summary>
        public const float BudgetFromBalance = 0.35f;

        /// <summary>시설 한 단계를 올리는 데 드는 돈. 연 수입 대비 비율.</summary>
        public const float FacilityUpgradeCost = 0.55f;

        /// <summary>구단이 유지하려는 스쿼드 인원.</summary>
        public const int SquadTarget = 25;
        public const int SquadMin = 20;
        public const int SquadMax = 28;

        // ── 이사회 ────────────────────────────────────────────────────────
        /// <summary>무승부를 0 으로 두면 안정이 누적되어 감독이 거의 해고되지 않는다.</summary>
        public const int ConfWin = 3;
        public const int ConfDraw = -1;
        public const int ConfLoss = -3;
        public const int ConfObjectiveMet = 20;
        public const int ConfObjectiveExceeded = 30;
        public const int ConfObjectiveMissed = -25;
        public const int SackThreshold = 20;

        // ── 능력치와 매치 엔진 전력 ───────────────────────────────────────
        /// <summary>
        /// 밸런스는 CA 1~200, 매치 엔진은 능력치 평균 1~20 을 쓴다. 둘을 잇는 환산.
        /// CA 110 → 14.05, CA 150 → 16.25.
        /// </summary>
        public static float CaToPower(float ca) => 8.0f + ca * 0.055f;

        public static float PowerToCa(float power) => (power - 8.0f) / 0.055f;

        // ── 매치 엔진 ─────────────────────────────────────────────────────
        /// <summary>블록당 공격 시도 기본 확률. 측정으로 정한다.</summary>
        // 세트피스가 공격 시도의 30% 를 가져가는데 기대 득점이 열린 플레이보다 낮다.
        // 총 득점을 2.55 로 유지하려면 그만큼 시도를 늘려야 한다 (0.52 → 0.575).
        public static float MatchAttackBase = 0.575f;

        /// <summary>홈 팀 전력 보정. 엔진에 홈 어드밴티지가 없어 홈 승률이 원정보다 낮았다.</summary>
        public static float HomeAdvantage = 1.05f;

        /// <summary>
        /// 전력 비율 증폭 지수. 전력은 능력치(1~20) 평균이라 강팀과 약팀의 비율 차이가
        /// 13% 남짓에 그친다. 그대로 쓰면 실력이 결과로 전달되지 않는다.
        /// </summary>
        public static float QualityExponent = 2.4f;

        /// <summary>점유율도 같은 이유로 증폭이 필요하다.</summary>
        public static float PossessionExponent = 5.0f;

        /// <summary>
        /// 동급 팀 공격 대 수비의 기준 비율. 수비에 가중치가 붙어 1.0 이 아니다.
        /// 지수를 적용하기 전에 이 값으로 나눠 정규화해야 지수가 득점 수준을 끌어내리지 않는다.
        /// FT.Sim diag 로 실측한다.
        /// </summary>
        public static float QualityReference = 0.705f;

        // ── 슈팅과 득점 ───────────────────────────────────────────────────
        /// <summary>한 번의 공격이 빅찬스가 될 확률 계수. 동급 경기에서 슈팅의 약 12% 가 빅찬스가 된다.</summary>
        public static float BigChanceRate = 0.18f;

        public static float BigChanceXgCoef = 0.478f;
        public static float BigChanceXgMin = 0.12f;
        public static float BigChanceXgMax = 0.62f;

        public static float NormalShotXgCoef = 0.1195f;
        public static float NormalShotXgMin = 0.03f;
        public static float NormalShotXgMax = 0.24f;

        /// <summary>
        /// 이 골키퍼 전력에서 보정이 1.0 이 된다. gkPower 는 골키퍼 능력치 평균(1~20)에
        /// 컨디션과 사기를 곱한 값이므로 1부 리그 주전 수준을 기준으로 삼는다.
        /// </summary>
        public static float GkNeutralPower = 15f;
        public static float GkSwing = 0.60f;

        // ── 전술 지시 ─────────────────────────────────────────────────────
        // 모든 항목은 거래다. 올라가는 값 옆에 내려가는 값이 반드시 있어야 하고,
        // 어떤 설정도 모든 상대에게 이득이면 안 된다. FT.Sim 의 tactics 모드가 이를 검사한다.

        /// <summary>수비 라인 한 단계가 점유율에 더하는 값.</summary>
        public const float LinePossessionStep = 0.022f;

        /// <summary>수비 라인 한 단계가 내 찬스의 질에 더하는 비율.</summary>
        public const float LineQualityStep = 0.030f;

        /// <summary>상대 수비 라인 한 단계가 내 빅찬스 확률에 더하는 비율.</summary>
        public const float LineBigChanceStep = 0.16f;

        /// <summary>템포 한 단계가 슈팅 확률에 더하는 값.</summary>
        public const float TempoShotStep = 0.035f;

        /// <summary>템포 한 단계가 찬스의 질에서 깎는 비율.</summary>
        public const float TempoQualityStep = 0.032f;

        /// <summary>템포 한 단계가 블록마다 깎는 체력.</summary>
        public const float TempoFatigueStep = 0.22f;

        /// <summary>압박 한 단계가 블록마다 깎는 체력.</summary>
        public const float PressFatigueStep = 0.26f;

        /// <summary>상대 압박이 내 찬스의 질에서 깎을 수 있는 최대 비율(압박 저항 0 기준).</summary>
        public const float PressDisruption = 0.20f;

        public const float CounterPressPossession = 0.025f;
        public const float CounterPressFatigue = 0.30f;

        public const float ShortPassPossession = 0.035f;
        public const float ShortPassQuality = 0.035f;

        public const float LongBallPossession = 0.045f;
        public const float LongBallQuality = 0.040f;
        public const float LongBallBigChance = 0.07f;

        public const float DirectPossession = 0.030f;
        public const float DirectShotRate = 0.030f;
        public const float DirectQuality = 0.045f;
        public const float DirectBigChance = 0.14f;

        /// <summary>역습 지시가 상대의 높은 라인에서 얻는 빅찬스 증가분.</summary>
        public const float CounterVsHighLine = 0.42f;

        /// <summary>뒷공간이 없는데 역습을 지시했을 때 잃는 빅찬스.</summary>
        public const float CounterWasted = 0.06f;

        /// <summary>폭 한 단계 × 상대 라인 한 단계가 찬스의 질에 더하는 비율.</summary>
        public const float WidthMatchStep = 0.012f;

        /// <summary>오프사이드 트랩이 본전이 되는 수비진 평균 능력치(20 만점).</summary>
        public const float TrapBreakEven = 13f;

        /// <summary>트랩 숙련이 본전에서 100% 벗어났을 때 상대 빅찬스가 움직이는 폭.</summary>
        public const float TrapSwing = 0.55f;

        /// <summary>상대가 수비적으로 내려섰을 때 내 슈팅 확률에서 깎이는 값(한 단계).</summary>
        public const float DefensiveShotDenial = 0.035f;

        /// <summary>상대 수비 라인이 낮을 때 내 슈팅 확률에서 깎이는 값(한 단계).</summary>
        public const float LowLineShotDenial = 0.022f;

        /// <summary>경기 중 체력이 경기력에 미치는 폭. 90분 내내 전력으로 뛰면 이만큼 떨어진다.</summary>
        public const float InMatchFatigueSwing = 0.14f;

        // ── 역할 ──────────────────────────────────────────────────────────

        /// <summary>
        /// 역할 적합도가 그 선수의 기여에 미치는 폭.
        /// 적합도 0 이면 1-이 값, 100 이면 1+이 값이 곱해진다.
        /// </summary>
        public const float RoleFitSwing = 0.12f;

        /// <summary>임무가 공격·수비 기여를 옮기는 폭. 총량은 변하지 않는다.</summary>
        public const float DutyShift = 0.18f;

        /// <summary>
        /// 만드는 역할과 넣는 역할이 한쪽으로 완전히 쏠렸을 때 공격에서 깎이는 비율.
        /// 이게 없으면 "전원 최고 적합도"가 늘 정답이라 역할 고르기가 작업이 된다.
        /// </summary>
        public const float RoleBalancePenalty = 0.14f;

        // ── 선수 개인 지시 ────────────────────────────────────────────────
        // 팀 지시와 같은 원칙이다. 모든 항목은 거래이고, Default 는 중립이다.

        public const float ShootOftenShare = 0.35f;
        public const float ShootOftenQuality = 0.12f;
        public const float LongShotShare = 0.25f;
        public const float LongShotQuality = 0.20f;

        public const float DribbleMoreCreate = 0.20f;
        public const float DribbleMorePossession = 0.006f;

        public const float RiskyPassQuality = 0.020f;
        public const float RiskyPassPossession = 0.008f;

        public const float PlayerPressPossession = 0.005f;
        public const float PlayerPressFatigue = 0.06f;
        public const float PlayerPressCard = 0.10f;

        public const float HardTackleDefence = 0.05f;
        public const float HardTackleCard = 0.30f;

        public const float MoreRunsShare = 0.22f;
        public const float MoreRunsDefence = 0.12f;
        public const float MoreRunsFatigue = 0.05f;

        // ── 홈그로운 ──────────────────────────────────────────────────────

        /// <summary>이만큼 머물면 홈그로운이 된다.</summary>
        public const int HomegrownYears = 3;

        /// <summary>이 나이까지 머문 것만 센다. 서른에 와서 3년을 채워도 홈그로운이 아니다.</summary>
        public const int HomegrownMaxAge = 21;

        /// <summary>세계를 만들 때 구단마다 심는 아카데미 출신 수.</summary>
        public const int HomegrownSeedAcademy = 6;

        /// <summary>리그 규정이 없을 때의 최소 인원.</summary>
        public const int HomegrownDefaultMin = 4;

        // ── 경기장·스폰서 ─────────────────────────────────────────────────

        /// <summary>좌석이 기준보다 많거나 적을 때 수입에 반영되는 몫.</summary>
        public const float StadiumRevenueShare = 0.35f;

        /// <summary>분위기가 수입에 주는 폭.</summary>
        public const float AtmosphereRevenueSwing = 0.10f;

        /// <summary>분위기의 기준. 세계 평균이 50 보다 높아 그대로 두면 모두가 덤을 받는다.</summary>
        public const float AtmospherePar = 70f;

        /// <summary>구단이 커질 때 경기장이 저절로 따라가는 몫. 한 번에 따라잡으면 증축할 이유가 없다.</summary>
        public const float StadiumDriftShare = 0.30f;
        public const float AtmosphereDriftShare = 0.35f;

        /// <summary>한 번에 늘리는 좌석 비율과 좌석당 공사비.</summary>
        public const float StadiumExpandShare = 0.25f;
        public const float StadiumCostPerSeat = 2800f;
        public const int StadiumMaxCapacity = 90000;

        /// <summary>스폰서 기본 금액(연 수입 대비)과 성적이 흔드는 폭.</summary>
        public const float SponsorBaseShare = 0.09f;
        public const float SponsorFormSwing = 0.45f;
        public const float SponsorBonusShare = 0.20f;
        public const int SponsorYears = 3;
        public const int SponsorFormSeasons = 3;

        /// <summary>스폰서가 수입을 끌어올릴 수 있는 한도.</summary>
        /// <summary>보통보다 좋은(나쁜) 계약이 수입에 반영되는 몫과 한도.</summary>
        public const float SponsorRevenueShare = 0.30f;
        public const float SponsorRevenueCap = 0.12f;

        // ── 시상 ──────────────────────────────────────────────────────────

        /// <summary>최우수 선수 후보가 되려면 리그 경기의 이만큼은 뛰어야 한다.</summary>
        public const float AwardMinGamesShare = 0.55f;

        /// <summary>영플레이어 나이 상한.</summary>
        public const int AwardYoungMaxAge = 21;

        /// <summary>보관하는 시상 기록 수. 끝없이 쌓으면 세이브가 불어난다.</summary>
        public const int AwardsKept = 120;

        // ── 이사회 협상 ───────────────────────────────────────────────────

        /// <summary>한 번 조르면 이만큼 지나야 다시 조를 수 있다.</summary>
        public const int BoardAskCooldownDays = 60;

        public const float BoardOddsFromConfidence = 0.85f;
        public const float BoardOddsPerPlaceAhead = 0.035f;
        public const float BoardOddsFromFunds = 0.20f;
        public const float BoardOddsFloor = 0.03f;
        public const float BoardOddsCeiling = 0.88f;

        /// <summary>요청마다 문턱이 다르다. 같으면 늘 가장 비싼 것만 조른다. Ask 순서와 같다.</summary>
        public static readonly float[] BoardAskDifficulty = { 0.30f, 0.34f, 0.46f, 0.40f, 0.50f };

        /// <summary>들어줬을 때와 거절당했을 때 깎이는 신뢰도. 거절이 더 아프다.</summary>
        public static readonly int[] BoardCostGranted = { 4, 4, 7, 5, 8 };
        public static readonly int[] BoardCostRefused = { 6, 6, 9, 7, 10 };

        /// <summary>들어줬을 때 얼마나 주는가.</summary>
        public const float BoardTransferGrant = 0.07f;    // 연 수입 대비
        public const float BoardWageGrant = 0.08f;        // 기존 급여 예산 대비
        public const float BoardObjectiveEase = 0.80f;    // 목표를 이 비율로 낮춘다
        public const float BoardFacilityCost = 0.22f;     // 연 수입 대비 공사비

        // ── 명단 보충 ─────────────────────────────────────────────────────

        /// <summary>급히 명단을 채울 때 데려오는 저니맨의 나이 범위.</summary>
        public const int FillerAgeMin = 22;
        public const int FillerAgeMax = 30;

        // ── 은퇴 ──────────────────────────────────────────────────────────

        /// <summary>이 나이부터 그만둘 수 있다.</summary>
        public const int RetireMinAge = 31;

        /// <summary>나이 한 살이 올리는 확률.</summary>
        public const float RetirePerYear = 0.085f;

        /// <summary>한 경기 출전이 깎는 확률과, 세어 주는 최대 경기 수.</summary>
        public const float RetirePerGame = 0.013f;
        public const int RetireGamesCap = 25;

        /// <summary>또래보다 잘하면 그만큼 더 뛴다. 절대 수준이 아니라 또래 대비다.</summary>
        public const float RetirePerCaOverPeers = 0.010f;

        /// <summary>소속이 없으면 더 빨리 접는다.</summary>
        public const float RetireFreeAgentPenalty = 0.25f;

        /// <summary>골키퍼는 오래 한다.</summary>
        public const float RetireKeeperBonus = 0.10f;

        public const float RetireChanceCap = 0.85f;

        /// <summary>이보다 얇은 명단에서는 더 내보내지 않는다.</summary>
        public const int RetireSquadFloor = 16;

        /// <summary>이 나이가 되면 무조건 그만둔다. 확률만으로는 꼬리가 길어 마흔넷이 남는다.</summary>
        public const int RetireHardAge = 40;

        // ── 스태프 ────────────────────────────────────────────────────────

        /// <summary>기대 점수의 바탕과 평판당 증가. 평판대로 갖추면 힘이 1.0 이 된다.</summary>
        public const float StaffParBase = 16f;
        public const float StaffParPerStar = 12f;

        /// <summary>둘째 이후 스태프가 보태는 몫. 머릿수 싸움이 되지 않게 작다.</summary>
        public const float StaffExtraWeight = 0.35f;

        /// <summary>코칭이 성장에 주는 폭과 한도.</summary>
        public const float CoachGrowthSwing = 0.22f;
        public const float CoachGrowthCap = 0.18f;

        /// <summary>의료진이 부상 빈도와 회복에 주는 폭과 한도.</summary>
        public const float MedicalInjurySwing = 0.30f;
        public const float MedicalInjuryCap = 0.25f;
        public const float MedicalRecoverySwing = 0.22f;
        public const float MedicalRecoveryCap = 0.20f;

        /// <summary>보고서 한 번의 값어치. 스카우트가 없으면 바닥값만 받는다.</summary>
        public const float ReportValueFloor = 0.35f;
        public const float ReportValuePerPower = 0.80f;
        public const float ReportValueCap = 1.45f;

        /// <summary>스카우트 힘이 더해 주는 주당 관찰 횟수.</summary>
        public const float ScoutTripsPerPower = 2.0f;

        /// <summary>분석가가 상대 색깔을 읽어 내는 문턱.</summary>
        public const float AnalystStyleThreshold = 0.55f;

        /// <summary>스태프 주급.</summary>
        public const int HeadCoachWageBase = 4000;
        public const int SeniorStaffWageBase = 2600;
        public const int StaffWageBase = 1500;
        public const float StaffWagePerStar = 0.55f;

        /// <summary>역할마다 시장에 나와 있는 사람 수.</summary>
        public const int FreeStaffPerRole = 14;

        // ── 2군 ───────────────────────────────────────────────────────────

        public const int ReserveSquadMax = 16;
        public const int ReserveSquadMin = 7;

        public const float ReserveConditionCost = 7f;
        public const float ReserveMorale = 2f;
        public const float ReserveInjuryChance = 0.012f;

        /// <summary>성장에 쓰는 출전 배수. 2군은 벤치보다 낫고 1군만 못해야 한다.</summary>
        public const float FirstTeamMinutes = 1.30f;
        public const float ReserveMinutes = 0.70f;
        public const float BenchMinutes = 0.25f;

        // ── 스카우트 ──────────────────────────────────────────────────────

        /// <summary>이만큼 보고서를 받으면 내 선수처럼 알게 된다.</summary>
        public const int ScoutReportsForFull = 4;

        /// <summary>같은 리그 선수는 보고서 없이도 이만큼 안다.</summary>
        public const float SameLeagueKnowledge = 0.55f;

        /// <summary>국가대표는 누구나 이만큼 안다.</summary>
        public const float CappedKnowledge = 0.40f;

        /// <summary>아무것도 모를 때 추정 구간의 반폭(CA 기준).</summary>
        public const float ScoutMaxError = 22f;

        /// <summary>구간 중심이 참값에서 빗나가는 정도. 1.0 이면 구간 반폭만큼 빗나갈 수 있다.</summary>
        public const float ScoutBiasShare = 0.55f;

        /// <summary>주당 스카우트 횟수.</summary>
        public const int ScoutsBase = 1;
        public const int ScoutsPerStar = 1;

        // ── AI 영입 안목 ──────────────────────────────────────────────────

        /// <summary>전술 색깔이 요구하는 능력치의 기준값(20 만점).</summary>
        public const float StyleFitBase = 13f;

        /// <summary>핵심 능력치 한 점당 색깔 적합도가 움직이는 비율.</summary>
        public const float StyleFitPerPoint = 0.090f;

        /// <summary>색깔 적합도의 한계. 전술이 실력을 덮으면 안 된다.</summary>
        public const float StyleFitCap = 0.15f;

        /// <summary>영입 안목. 1성 구단과 5성 구단의 차이를 만든다.</summary>
        public const float RecruitSkillBase = 0.05f;
        public const float RecruitSkillPerStar = 0.17f;

        // ── 선수 개인 목표 ────────────────────────────────────────────────
        // 선수마다 처지에 맞는 것을 바란다. 들어주지 못하면 불만이 쌓이고 끝내 떠나려 한다.

        public const int YouthAppearanceGoal = 12;
        public const int StarterAppearanceGoal = 28;
        public const int SquadAppearanceGoal = 14;

        /// <summary>이만큼 차이 안에 들면 주전 자리를 요구할 만하다고 본다.</summary>
        public const float StarterClaimGap = 10f;

        /// <summary>팀 경기의 이 비율 이상 뛰었으면 주전으로 본다. 오늘 명단에서 빠져도 마찬가지다.</summary>
        public const float StarterPlayedShare = 0.55f;

        /// <summary>CA 를 이 값으로 나눈 만큼 골을 바란다. 140 이면 11골.</summary>
        public const float GoalGoalDivisor = 13f;
        public const float AssistGoalDivisor = 18f;

        // 목표마다 못 이뤘을 때의 무게. 주전 자리가 가장 크다.
        public const float StarterGoalWeight = 7f;
        public const float AppearanceGoalWeight = 5f;
        public const float OutputGoalWeight = 3f;
        public const float SilverwareGoalWeight = 2f;

        /// <summary>4성 구단 선수가 바라는 리그 순위.</summary>
        public const int BigClubTargetPosition = 3;
        public const float CallUpGoalWeight = 2f;

        // ── 이적 요청 ─────────────────────────────────────────────────────

        /// <summary>이 압력을 넘겨야 요청이 올라오기 시작한다.</summary>
        public const float RequestThreshold = 4.5f;

        public const float HarmonyRequestWeight = 3f;
        public const float MoraleRequestWeight = 3f;
        public const float RejectionRequestWeight = 2.5f;

        /// <summary>임계를 1 넘길 때마다 주당 요청 확률이 이만큼 는다.</summary>
        public const float RequestChancePerPoint = 0.055f;

        /// <summary>한 주에 한 선수가 요청할 최대 확률. 없으면 한 주에 전원이 반란을 일으킨다.</summary>
        public const float RequestChanceCap = 0.22f;

        /// <summary>동시에 답을 기다릴 수 있는 요청 수. 감독이 미루는 사이 쌓이는 것을 막는다.</summary>
        public const int MaxPendingRequests = 4;

        /// <summary>거절당한 선수가 다시 요구하기까지 참는 기간.</summary>
        public const int RequestCooldownDays = 70;

        public const float RequestAcceptMorale = 12f;
        public const float RequestRejectMorale = 10f;

        // ── 국가대표 ──────────────────────────────────────────────────────

        public const int NationalSquadSize = 23;
        public const int NationalKeepers = 3;

        /// <summary>발탁선에서 이만큼 아래면 아직 대표를 바란다.</summary>
        public const float CallUpHopeGap = 8f;

        /// <summary>A매치 주간에 깎이는 컨디션.</summary>
        public const float CallUpConditionCost = 14f;

        public const float CallUpMorale = 4f;
        public const float CallUpGoalChance = 0.22f;
        public const float CallUpInjuryChance = 0.035f;

        /// <summary>대표 경력이 몸값에 붙이는 최대 비율과, 그 한도에 닿는 출전 수.</summary>
        public const float CapValueBonus = 0.18f;
        public const int CapValueCeiling = 40;

        // ── 세트피스 ──────────────────────────────────────────────────────
        // 공격 시도 중 이 비율이 열린 플레이 대신 세트피스로 끝난다.
        // 더해 주는 것이 아니라 대체하는 것이다 — 얹으면 리그 득점이 통째로 올라간다.

        /// <summary>세트피스로 끝나는 공격 시도의 비율(페널티·프리킥 포함).</summary>
        public const float SetPieceShare = 0.30f;

        /// <summary>그중 페널티. 실제로 경기당 0.2회 안팎이 나오도록 잡는다.</summary>
        public const float PenaltyShare = 0.012f;

        /// <summary>그중 직접 프리킥.</summary>
        public const float FreeKickShare = 0.055f;

        public const float PenaltyXg = 0.76f;
        public const float FreeKickXg = 0.085f;
        public const float CornerXg = 0.031f;

        /// <summary>제공권 우위가 코너 기대 득점을 증폭하는 지수.</summary>
        public const float AerialExponent = 1.6f;

        /// <summary>
        /// 코너에서 골키퍼 실력이 차지하는 비중. 열린 플레이(GkSwing)보다 크다 —
        /// 박스를 장악하는 골키퍼는 코너를 통째로 지운다.
        /// </summary>
        public const float CornerGkSwing = 0.80f;

        /// <summary>달려드는 선수들의 움직임 우위가 코너 기대 득점을 증폭하는 지수.</summary>
        public const float MovementExponent = 1.3f;

        /// <summary>평범한 1부 주전 키커의 능력치 기준.</summary>
        public const float SetPieceTakerBase = 13f;

        /// <summary>키커 능력치가 기준에서 100% 벗어났을 때 배달 품질이 움직이는 폭.</summary>
        public const float SetPieceTakerSwing = 0.55f;

        // 배달 방식 — 위협을 올리는 쪽은 골키퍼에게 걷어내일 위험도 함께 올린다.
        public const float InswingerThreat = 0.16f;
        public const float InswingerGkRisk = 0.14f;
        public const float OutswingerThreat = 0.08f;
        public const float OutswingerGkRisk = 0.12f;
        public const float DrivenThreat = 0.05f;
        public const float DrivenGkRisk = 0.06f;
        public const float LoftedThreat = 0.22f;
        public const float LoftedGkRisk = 0.26f;
        public const float ShortThreat = 0.55f;
        public const float ShortGkRisk = 0.40f;

        /// <summary>짧게 처리한 코너가 공을 지켜 열린 찬스로 이어질 확률.</summary>
        public const float ShortRetainChance = 0.45f;

        /// <summary>세트피스 득점 비율의 허용 범위. 실제 축구는 25~33% 쯤이다.</summary>
        public const float SetPieceGoalShareMin = 0.18f;
        public const float SetPieceGoalShareMax = 0.36f;

        // ── 회귀 테스트 허용 범위 ─────────────────────────────────────────
        public const float DriftLimit = 5.0f;          // 20시즌 전력 변화
        public const float WageRatioMin = 0.30f;
        public const float WageRatioMax = 0.65f;
        public const float CaPaMin = 0.75f;
        public const float CaPaMax = 0.85f;
        /// <summary>동급 전력 두 팀 경기의 합계 득점. 홈이 원정보다 높은 것이 정상이므로 폭으로 본다.</summary>
        public const float GoalsTotalMin = 2.40f;
        public const float GoalsTotalMax = 2.80f;
        public const float GoalsPerTeamMin = 1.00f;
        public const float GoalsPerTeamMax = 1.60f;
        public const float TenureMin = 2.0f;           // 감독 평균 재직 시즌
        public const float TenureMax = 3.2f;
        public const float HomeWinRateMin = 0.42f;
        public const float HomeWinRateMax = 0.50f;
        public const float ShotsPerMatchMin = 16f;
        public const float ShotsPerMatchMax = 24f;
    }
}
