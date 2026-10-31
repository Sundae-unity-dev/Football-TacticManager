namespace FootballTactician.Data
{
    public enum ShootingFrequency  { ShootOften, Default, RarelyShoot }
    public enum LongShotTendency   { ShootFromDistance, Default, AvoidLongShots }
    public enum ShotPlacement      { PlaceShots, Default, DrivenShots }

    public enum PassingStyle       { Risky, Default, Safe }
    public enum PassingRange       { Short, Mixed, Long }

    public enum CrossingTendency   { CrossMoreOften, Default, CrossLessOften }
    public enum CrossType          { Whipped, Mixed, Floated, DriveIntoBox }
    public enum CrossFromSide      { FromRight, Mixed, FromLeft }

    public enum DribblingTendency  { DribbleMore, Default, DribbleLess }

    public enum PressingIntensity  { PressMore, Default, PressLess }
    public enum TacklingStyle      { TackleHarder, Default, EasyTackles }
    public enum MarkingStyle       { TightMarking, Default, LooseMarking }
    public enum ClosingDownStyle   { CloseDownMore, Default, CloseDownLess }

    public enum RunFrequency       { MoreRuns, Default, FewerRuns }
    public enum VerticalMovement   { GetFurtherForward, Default, StayBackMore }
    public enum HorizontalMovement { HugTouchline, Default, CutInside }
    public enum OffTheBallRuns     { MakeMoreRuns, Default, MakeFewerRuns }

    public enum SetPieceRole
    {
        None,
        Taker,
        Assister,
        NearPost,
        FarPost,
        EdgeOfBox,
        StayBack
    }
}
