using System;
using System.Text.Json.Serialization;

namespace FootballTactician.Data
{
    /// <summary>
    /// 선수 한 명에게 부여되는 개별 지시사항.
    /// RoleDefinition.defaultInstructions 를 기본값으로 쓰고,
    /// 감독이 수동으로 오버라이드할 수 있다.
    ///
    /// 거의 모든 항목이 { XMore, Default, XLess } 꼴이라 <b>0 번이 Default 가 아니다</b>.
    /// 세이브는 기본값인 항목을 빼고 쓰므로, 감독이 ShootOften(0) 을 고르면 저장에서
    /// 빠지고 불러올 때 초기값 Default 로 되살아난다. 그래서 전부 항상 쓰도록 표시한다.
    /// </summary>
    [Serializable]
    public class PlayerInstructions
    {
        // ── 슈팅 ──────────────────────────────────────────────
        [JsonIgnore(Condition = JsonIgnoreCondition.Never)]
        public ShootingFrequency shootingFrequency  = ShootingFrequency.Default;
        [JsonIgnore(Condition = JsonIgnoreCondition.Never)]
        public LongShotTendency  longShotTendency   = LongShotTendency.Default;
        [JsonIgnore(Condition = JsonIgnoreCondition.Never)]
        public ShotPlacement     shotPlacement      = ShotPlacement.Default;

        // ── 패스 ──────────────────────────────────────────────
        [JsonIgnore(Condition = JsonIgnoreCondition.Never)]
        public PassingStyle      passingStyle       = PassingStyle.Default;
        [JsonIgnore(Condition = JsonIgnoreCondition.Never)]
        public PassingRange      passingRange       = PassingRange.Mixed;

        // ── 크로스 ────────────────────────────────────────────
        [JsonIgnore(Condition = JsonIgnoreCondition.Never)]
        public CrossingTendency  crossingTendency   = CrossingTendency.Default;
        [JsonIgnore(Condition = JsonIgnoreCondition.Never)]
        public CrossType         crossType          = CrossType.Mixed;
        [JsonIgnore(Condition = JsonIgnoreCondition.Never)]
        public CrossFromSide     crossFromSide      = CrossFromSide.Mixed;

        // ── 드리블 ────────────────────────────────────────────
        [JsonIgnore(Condition = JsonIgnoreCondition.Never)]
        public DribblingTendency dribblingTendency  = DribblingTendency.Default;

        // ── 수비 ──────────────────────────────────────────────
        [JsonIgnore(Condition = JsonIgnoreCondition.Never)]
        public PressingIntensity pressingIntensity  = PressingIntensity.Default;
        [JsonIgnore(Condition = JsonIgnoreCondition.Never)]
        public TacklingStyle     tacklingStyle      = TacklingStyle.Default;
        [JsonIgnore(Condition = JsonIgnoreCondition.Never)]
        public MarkingStyle      markingStyle       = MarkingStyle.Default;
        [JsonIgnore(Condition = JsonIgnoreCondition.Never)]
        public ClosingDownStyle  closingDownStyle   = ClosingDownStyle.Default;

        // ── 움직임 ────────────────────────────────────────────
        [JsonIgnore(Condition = JsonIgnoreCondition.Never)]
        public RunFrequency      runFrequency       = RunFrequency.Default;
        [JsonIgnore(Condition = JsonIgnoreCondition.Never)]
        public VerticalMovement  verticalMovement   = VerticalMovement.Default;
        [JsonIgnore(Condition = JsonIgnoreCondition.Never)]
        public HorizontalMovement horizontalMovement = HorizontalMovement.Default;
        [JsonIgnore(Condition = JsonIgnoreCondition.Never)]
        public OffTheBallRuns    offTheBallRuns     = OffTheBallRuns.Default;

        // ── 세트피스 ──────────────────────────────────────────
        public SetPieceRole cornerRole             = SetPieceRole.None;
        public SetPieceRole freeKickRole           = SetPieceRole.None;
        public bool         takePenalties          = false;

        /// <summary>
        /// 다른 PlayerInstructions 의 값을 그대로 복사한다.
        /// </summary>
        public static PlayerInstructions CopyFrom(PlayerInstructions src)
            => (PlayerInstructions)src.MemberwiseClone();
    }
}
