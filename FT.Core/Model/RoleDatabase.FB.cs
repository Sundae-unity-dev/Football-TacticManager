using System.Collections.Generic;

namespace FootballTactician.Data
{
    public static partial class RoleDatabase
    {
        private static void BuildFb()
        {
            Add(new RoleDefinition
            {
                role = PlayerRole.Fullback,
                displayName = "풀백",
                shortDescription = "측면 수비와 기본 공격 참여를 겸하는 풀백",
                fullDescription =
                    "측면 수비의 기본 역할. 상대 윙어를 막고, 공격 시 오버래핑으로 측면 폭을 제공한다. " +
                    "수비 안정성을 유지하면서 필요 시 크로스를 올리는 균형형이다. " +
                    "측면의 공수 균형을 책임지는 가장 전통적인 풀백 역할로, 어떤 시스템에도 무리 없이 적용된다.",
                tacticalBehavior =
                    "수비 시 상대 윙어와 1대1 대응하며 라인을 유지한다. " +
                    "공격 시 오버래핑 런을 통해 크로스를 공급하되, 무리한 전진은 자제한다. " +
                    "Attack 듀티에서는 측면 라인을 따라 더 높이 올라가 크로스와 연계 플레이 빈도가 늘어난다.",
                strengths =
                    "수비와 공격 모두 적절히 소화해 측면 구조의 균형을 유지한다. " +
                    "다양한 듀티(Defend/Support/Attack)를 통해 전술적 요구에 맞게 역할을 조절할 수 있다.",
                weaknesses =
                    "공격적으로 올라갈수록 뒷공간이 노출되어 역습에 취약해질 수 있다. " +
                    "인버티드 풀백이나 윙백에 비해 전술적 특수성이 낮다.",
                bestIn =
                    "4-4-2, 4-2-3-1 등 4백 기반의 모든 전술 시스템에서 기본 측면 수비로 활용된다. " +
                    "같은 쪽 윙어가 공격에 집중하는 구조라면 Defend 듀티를 유지해 균형을 잡는 것이 중요하다.",
                applicablePositions = Positions.Fullbacks,
                allowedDuties = new List<RoleDuty> { RoleDuty.Defend, RoleDuty.Support, RoleDuty.Attack },
                keyTechnicalAttributes = new List<TechnicalAttribute> { TechnicalAttribute.Tackling, TechnicalAttribute.Crossing, TechnicalAttribute.Marking },
                keyPhysicalAttributes  = new List<PhysicalAttribute> { PhysicalAttribute.Speed, PhysicalAttribute.Stamina },
                keyMentalAttributes    = new List<MentalAttribute> { MentalAttribute.Positioning, MentalAttribute.Teamwork, MentalAttribute.WorkRate },
                defendInstructions = new PlayerInstructions
                {
                    verticalMovement   = VerticalMovement.StayBackMore,
                    crossingTendency   = CrossingTendency.CrossLessOften,
                    markingStyle       = MarkingStyle.TightMarking,
                },
                attackInstructions = new PlayerInstructions
                {
                    verticalMovement   = VerticalMovement.GetFurtherForward,
                    crossingTendency   = CrossingTendency.CrossMoreOften,
                    runFrequency       = RunFrequency.MoreRuns,
                    offTheBallRuns     = OffTheBallRuns.MakeMoreRuns,
                    horizontalMovement = HorizontalMovement.HugTouchline,
                }
            });

            Add(new RoleDefinition
            {
                role = PlayerRole.InvertedFullback,
                displayName = "인버티드 풀백",
                shortDescription = "안쪽으로 들어와 중원 과부하를 만드는 현대적 풀백",
                fullDescription =
                    "공격 시 오버래핑 대신 중앙으로 이동해 미드필드 삼각형을 형성하는 현대적 풀백. " +
                    "동측 윙어가 터치라인을 넓게 활용하는 동안 이 선수는 내측으로 들어와 CDM과 짝을 이루거나 " +
                    "볼을 순환시키는 '가짜 미드필더'로 기능한다. " +
                    "포제션 전술에서 중원 수적 우위를 만들어 상대 압박을 무력화하는 전술적 핵심 개념이다.",
                tacticalBehavior =
                    "공격 시 내측으로 이동해 CDM과 2대1 구도를 만들거나 박스 앞 공간으로 전진한다. " +
                    "동측 윙어에게 외측 공간을 열어주어 1대1 상황을 만드는 효과도 얻는다. " +
                    "수비 전환 시에는 빠르게 측면으로 복귀해 수비 블록을 형성한다.",
                strengths =
                    "중원 숫자를 늘려 포제션 축구를 강화하고 상대의 전방 압박을 숫자로 돌파한다. " +
                    "동측 윙어가 터치라인을 넓게 활용할 수 있도록 공간을 창출하는 이중 역할을 한다.",
                weaknesses =
                    "측면 수비력이 약해지므로 상대 윙어에게 오버래핑 공간이 열릴 수 있다. " +
                    "크로스 빈도가 낮아 크로스 기반 전술에는 맞지 않는다.",
                bestIn =
                    "포제션 기반 전술에서 같은 쪽에 인사이드 포워드 또는 터치라인 윙어가 폭을 담당할 때 이상적이다. " +
                    "CDM이나 딥 라잉 플레이메이커가 뒤를 받쳐주는 구조에서 내측 이동이 더 안전하고 효과적이다.",
                applicablePositions = Positions.Fullbacks,
                allowedDuties = new List<RoleDuty> { RoleDuty.Support, RoleDuty.Attack },
                keyTechnicalAttributes = new List<TechnicalAttribute> { TechnicalAttribute.Passing, TechnicalAttribute.FirstTouch, TechnicalAttribute.Tackling },
                keyPhysicalAttributes  = new List<PhysicalAttribute> { PhysicalAttribute.Speed, PhysicalAttribute.Agility, PhysicalAttribute.Stamina },
                keyMentalAttributes    = new List<MentalAttribute> { MentalAttribute.Vision, MentalAttribute.Decisions, MentalAttribute.Positioning },
                supportInstructions = new PlayerInstructions
                {
                    horizontalMovement = HorizontalMovement.CutInside,
                    crossingTendency   = CrossingTendency.CrossLessOften,
                },
                attackInstructions = new PlayerInstructions
                {
                    horizontalMovement = HorizontalMovement.CutInside,
                    verticalMovement   = VerticalMovement.GetFurtherForward,
                    passingStyle       = PassingStyle.Risky,
                    runFrequency       = RunFrequency.MoreRuns,
                }
            });

            Add(new RoleDefinition
            {
                role = PlayerRole.Wingback,
                displayName = "윙백",
                shortDescription = "측면을 종횡무진 누비는 공격적 윙백",
                fullDescription =
                    "3백 또는 5백 시스템에서 측면 전체를 커버하는 역할. " +
                    "수비에서는 상대 윙어를 막고, 공격에서는 윙어처럼 전방에서 크로스와 돌파를 시도한다. " +
                    "높은 체력과 속도가 필수다. " +
                    "풀백과 윙어의 역할을 동시에 수행하므로, 팀의 측면 공격 전반을 혼자 책임지는 고강도 포지션이다.",
                tacticalBehavior =
                    "측면 라인을 따라 상하 이동을 반복하며 수비와 공격 모두에 적극 참여한다. " +
                    "크로스 빈도가 매우 높으며 때로는 박스 안까지 침투한다. " +
                    "Attack 듀티에서는 높이 올라가 사실상 윙어로 기능하며, 수비 부담은 3백이 흡수한다.",
                strengths =
                    "측면의 공수 균형을 완전히 책임져 3백/5백 시스템에서 윙어가 없는 공백을 메운다. " +
                    "높은 크로스 빈도로 박스 안 타깃맨에게 꾸준히 찬스를 공급한다.",
                weaknesses =
                    "체력 소모가 극심해 체력이 떨어지면 수비와 공격 모두 효율이 크게 낮아진다. " +
                    "윙백의 수비 복귀가 늦어지면 측면이 완전히 뚫려 위험해진다.",
                bestIn =
                    "3-5-2, 5-3-2, 3-4-3처럼 명시적으로 3백을 사용하는 시스템에서 필수 역할이다. " +
                    "앵커맨이나 하프백이 중앙을 안정시켜 줄 때 더 공격적으로 움직일 수 있다.",
                applicablePositions = Positions.Wingbacks,
                allowedDuties = new List<RoleDuty> { RoleDuty.Support, RoleDuty.Attack },
                keyTechnicalAttributes = new List<TechnicalAttribute> { TechnicalAttribute.Crossing, TechnicalAttribute.Tackling, TechnicalAttribute.Dribbling },
                keyPhysicalAttributes  = new List<PhysicalAttribute> { PhysicalAttribute.Speed, PhysicalAttribute.Stamina, PhysicalAttribute.Agility },
                keyMentalAttributes    = new List<MentalAttribute> { MentalAttribute.WorkRate, MentalAttribute.Teamwork },
                supportInstructions = new PlayerInstructions
                {
                    verticalMovement   = VerticalMovement.GetFurtherForward,
                    crossingTendency   = CrossingTendency.CrossMoreOften,
                    horizontalMovement = HorizontalMovement.HugTouchline,
                    runFrequency       = RunFrequency.MoreRuns,
                },
                attackInstructions = new PlayerInstructions
                {
                    verticalMovement   = VerticalMovement.GetFurtherForward,
                    crossingTendency   = CrossingTendency.CrossMoreOften,
                    horizontalMovement = HorizontalMovement.HugTouchline,
                    runFrequency       = RunFrequency.MoreRuns,
                    offTheBallRuns     = OffTheBallRuns.MakeMoreRuns,
                    dribblingTendency  = DribblingTendency.DribbleMore,
                }
            });

            Add(new RoleDefinition
            {
                role = PlayerRole.InvertedWingback,
                displayName = "인버티드 윙백",
                shortDescription = "안쪽으로 들어와 득점·어시스트를 노리는 윙백",
                fullDescription =
                    "반대발 윙백이 안쪽으로 커팅인하며 슈팅 또는 키패스를 시도하는 역할. " +
                    "오른발잡이가 왼쪽, 왼발잡이가 오른쪽에 배치되는 것이 일반적이다. " +
                    "3백 시스템의 측면을 담당하면서도 중앙 공격에 직접 가담하는 공격적이고 창의적인 역할이다.",
                tacticalBehavior =
                    "공격 시 중앙으로 이동해 슈팅 또는 스루패스를 시도한다. " +
                    "수비 시에는 라인으로 복귀해 수비 블록을 형성한다. " +
                    "Attack 듀티에서는 페널티 박스 근처까지 침투해 직접 득점을 노리기도 한다.",
                strengths =
                    "수비 포지션이지만 득점과 어시스트에 직접 기여해 예측하기 어려운 위협을 만든다. " +
                    "반대발 커팅인으로 강한 발로 슈팅을 시도하는 장점이 있다.",
                weaknesses =
                    "측면 공간을 비워 동측 상대 공격수에게 공간이 열릴 수 있다. " +
                    "수비 복귀가 느리면 역습 시 측면 수비가 무너질 위험이 있다.",
                bestIn =
                    "3백 시스템에서 3명의 CB가 측면을 커버할 수 있을 때 공격적 역할에 집중할 수 있다. " +
                    "카리예로나 하프백이 측면 커버를 지원해 주는 시스템에서 더욱 효과적이다.",
                applicablePositions = Positions.Wingbacks,
                allowedDuties = new List<RoleDuty> { RoleDuty.Support, RoleDuty.Attack },
                keyTechnicalAttributes = new List<TechnicalAttribute> { TechnicalAttribute.Shooting, TechnicalAttribute.Dribbling, TechnicalAttribute.Crossing, TechnicalAttribute.Tackling },
                keyPhysicalAttributes  = new List<PhysicalAttribute> { PhysicalAttribute.Speed, PhysicalAttribute.Agility, PhysicalAttribute.Stamina },
                keyMentalAttributes    = new List<MentalAttribute> { MentalAttribute.Decisions, MentalAttribute.Vision },
                supportInstructions = new PlayerInstructions
                {
                    horizontalMovement = HorizontalMovement.CutInside,
                    crossingTendency   = CrossingTendency.CrossLessOften,
                },
                attackInstructions = new PlayerInstructions
                {
                    horizontalMovement = HorizontalMovement.CutInside,
                    crossingTendency   = CrossingTendency.CrossLessOften,
                    shootingFrequency  = ShootingFrequency.ShootOften,
                    runFrequency       = RunFrequency.MoreRuns,
                    longShotTendency   = LongShotTendency.ShootFromDistance,
                }
            });

            Add(new RoleDefinition
            {
                role = PlayerRole.CompleteWingback,
                displayName = "완성형 윙백",
                shortDescription = "수비와 공격을 완벽하게 소화하는 만능 윙백",
                fullDescription =
                    "수비에서 측면을 철저히 막고 공격에서는 윙어처럼 전진해 크로스·드리블·슈팅까지 가능한 완전형 윙백. " +
                    "3백/5백 시스템에서 측면 전체를 혼자 책임지는 고강도 포지션이다. " +
                    "일반 윙백보다 기술·체력·전술 이해도가 한 단계 높아야 하며, 경기 내내 팀의 측면 공격과 수비를 모두 수행한다.",
                tacticalBehavior =
                    "수비 시 터치라인을 따라 내려와 상대 윙어와 풀백을 동시에 견제한다. " +
                    "공격 시 오버래핑으로 폭을 만들거나 안쪽으로 커팅인해 직접 슈팅을 시도하기도 한다. " +
                    "팀의 전술 요구에 따라 크로스·드리블·패스를 상황에 맞게 선택하는 높은 판단력이 요구된다.",
                strengths =
                    "측면의 공수 균형을 완벽하게 책임져 팀 전체의 전술 자유도를 높인다. " +
                    "다양한 공격 옵션(크로스, 커팅인, 연계)으로 상대 수비가 예측하기 어렵다.",
                weaknesses =
                    "체력 소모가 극심해 후반에 효율이 떨어지면 공수 모두 문제가 생긴다. " +
                    "능력치가 전반적으로 높아야 하므로 적합한 선수를 찾기 어렵고 육성 비용도 크다.",
                bestIn =
                    "3-5-2나 3-4-3처럼 3백을 기반으로 측면을 윙백에 완전히 위임하는 시스템에서 최대 효율을 발휘한다. " +
                    "중앙 미드필더가 수비에 집중하고 양쪽 컴플리트 윙백이 측면 전체를 담당하는 구조에 이상적이다.",
                applicablePositions = Positions.Wingbacks,
                allowedDuties = new List<RoleDuty> { RoleDuty.Defend, RoleDuty.Support, RoleDuty.Attack },
                keyTechnicalAttributes = new List<TechnicalAttribute> { TechnicalAttribute.Crossing, TechnicalAttribute.Tackling, TechnicalAttribute.Dribbling, TechnicalAttribute.Passing },
                keyPhysicalAttributes  = new List<PhysicalAttribute> { PhysicalAttribute.Speed, PhysicalAttribute.Stamina, PhysicalAttribute.Agility },
                keyMentalAttributes    = new List<MentalAttribute> { MentalAttribute.WorkRate, MentalAttribute.Decisions, MentalAttribute.Teamwork },
                defendInstructions = new PlayerInstructions
                {
                    verticalMovement   = VerticalMovement.StayBackMore,
                    crossingTendency   = CrossingTendency.CrossLessOften,
                    markingStyle       = MarkingStyle.TightMarking,
                },
                supportInstructions = new PlayerInstructions
                {
                    verticalMovement   = VerticalMovement.GetFurtherForward,
                    crossingTendency   = CrossingTendency.CrossMoreOften,
                    runFrequency       = RunFrequency.MoreRuns,
                },
                attackInstructions = new PlayerInstructions
                {
                    verticalMovement   = VerticalMovement.GetFurtherForward,
                    crossingTendency   = CrossingTendency.CrossMoreOften,
                    runFrequency       = RunFrequency.MoreRuns,
                    offTheBallRuns     = OffTheBallRuns.MakeMoreRuns,
                    dribblingTendency  = DribblingTendency.DribbleMore,
                }
            });
        }
    }
}