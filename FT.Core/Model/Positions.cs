namespace FootballTactician.Data
{
    /// <summary>
    /// 자주 쓰이는 Position 비트마스크 조합 상수.
    /// RoleDatabase 역할 정의 및 포지션 확인 로직에서 사용한다.
    /// </summary>
    public static class Positions
    {
        public const Position Fullbacks     = Position.LB  | Position.RB;
        public const Position Wingbacks     = Position.LWB | Position.RWB;
        public const Position WideMids      = Position.LM  | Position.RM;
        public const Position WideForwards  = Position.LW  | Position.RW;
        public const Position WideAttackers = WideForwards | WideMids;
        public const Position AllWide       = Fullbacks | Wingbacks | WideAttackers;

        public const Position DefMids       = Position.CDM;
        public const Position CentMids      = Position.CM;
        public const Position AttMids       = Position.CAM;
        public const Position AllMids       = DefMids | CentMids | AttMids;

        public const Position Forwards      = Position.ST | Position.CF;
        public const Position AllOutfield   = AllWide | AllMids | Position.CB | Forwards;
    }
}
