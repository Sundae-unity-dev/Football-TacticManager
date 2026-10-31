using System.Collections.Generic;

namespace FootballTactician.Data
{
    public static partial class RoleDatabase
    {
        private static void BuildWide()
        {
            Add(new RoleDefinition
            {
                role = PlayerRole.Winger,
                displayName = "윙어",
                shortDescription = "측면 폭을 유지하며 크로스를 올리는 전통형 윙어",
                fullDescription =
                    "측면 라인에 위치해 공간을 벌리고 크로스와 드리블 돌파로 공격 기회를 만드는 역할. " +
                    "속도와 드리블, 크로스 정확도가 핵심이다. " +
                    "팀의 폭을 최대한 활용해 상대 수비를 측면으로 끌어당기고 중앙 공격수에게 크로스를 공급하는 전통적이고 효과적인 역할이다.",
                tacticalBehavior =
                    "터치라인을 따라 전진하며 수비수와 1대1 돌파 후 크로스를 올린다. " +
                    "수비 시에는 상대 풀백의 전진을 견제한다. " +
                    "Attack 듀티에서는 수비 가담을 최소화하고 오로지 측면 공격에만 집중한다.",
                strengths =
                    "측면 폭을 활용해 상대 수비를 넓히고 중앙 공격수에게 크로스를 꾸준히 공급한다. " +
                    "1대1 돌파 능력으로 상대 풀백에게 지속적인 위협을 제공한다.",
                weaknesses =
                    "안쪽 공간을 활용하지 않아 상대가 터치라인 쪽으로 몰면 효과가 감소한다. " +
                    "직접 슈팅보다 크로스에 의존하므로 헤더가 약한 팀에서는 효율이 떨어진다.",
                bestIn =
                    "타깃맨이나 어드밴스드 포워드처럼 크로스를 받을 공격수가 있는 시스템에 이상적이다. " +
                    "4-4-2나 4-2-3-1에서 측면 공격의 핵심 역할을 담당하며 전통적인 크로스 전술을 완성한다.",
                applicablePositions = Positions.WideAttackers,
                allowedDuties = new List<RoleDuty> { RoleDuty.Support, RoleDuty.Attack },
                keyTechnicalAttributes = new List<TechnicalAttribute> { TechnicalAttribute.Crossing, TechnicalAttribute.Dribbling, TechnicalAttribute.FirstTouch },
                keyPhysicalAttributes  = new List<PhysicalAttribute> { PhysicalAttribute.Speed, PhysicalAttribute.Agility },
                keyMentalAttributes    = new List<MentalAttribute> { MentalAttribute.WorkRate, MentalAttribute.Decisions },
                supportInstructions = new PlayerInstructions
                {
                    horizontalMovement = HorizontalMovement.HugTouchline,
                    crossingTendency   = CrossingTendency.CrossMoreOften,
                    dribblingTendency  = DribblingTendency.DribbleMore,
                },
                attackInstructions = new PlayerInstructions
                {
                    horizontalMovement = HorizontalMovement.HugTouchline,
                    crossingTendency   = CrossingTendency.CrossMoreOften,
                    dribblingTendency  = DribblingTendency.DribbleMore,
                    verticalMovement   = VerticalMovement.GetFurtherForward,
                    runFrequency       = RunFrequency.MoreRuns,
                }
            });

            Add(new RoleDefinition
            {
                role = PlayerRole.InsideForward,
                displayName = "인사이드 포워드",
                shortDescription = "측면에서 안으로 커팅인해 강발 슈팅으로 골을 노리는 공격적 윙어",
                fullDescription =
                    "측면에 배치되지만 본질은 골게터인 역할. 넓게 출발해 상대 풀백을 잡아당긴 뒤 " +
                    "빠르게 안쪽으로 파고들어 강발로 강력한 슈팅을 시도한다. " +
                    "오른발잡이는 왼쪽, 왼발잡이는 오른쪽에 배치해 커팅인 후 강발 슈팅 각도를 극대화한다. " +
                    "인버티드 윙어보다 득점 지향적이며, 박스 안 득점 기회 창출이 최우선 임무다.",
                tacticalBehavior =
                    "초반에는 터치라인 부근에 머물러 상대 수비를 측면으로 끌어낸 뒤 안으로 빠르게 커팅인한다. " +
                    "Support 듀티에서는 연계 플레이와 침투를 병행하고, Attack 듀티에서는 박스 근처 침투와 원거리 슈팅에 집중한다. " +
                    "커팅인 후 측면이 비워져 같은 쪽 풀백이 오버래핑 공간을 자동으로 확보한다.",
                strengths =
                    "전통 윙어보다 골 기여도가 훨씬 높으며, 박스 근처에서 강발로 강력한 슈팅 위협을 제공한다. " +
                    "커팅인 움직임으로 동측 풀백·윙백에게 측면 공간을 열어주는 이중 전술 효과를 만든다.",
                weaknesses =
                    "득점에 집중하다 보면 팀의 측면 폭 활용이 줄어 상대 수비가 중앙을 더 촘촘하게 내려앉을 수 있다. " +
                    "반대발 선수가 필요하므로 오른발잡이는 좌측, 왼발잡이는 우측에만 배치해야 효과가 극대화된다.",
                bestIn =
                    "스트라이커가 중앙을 점거하고 양쪽 인사이드 포워드가 안으로 파고드는 4-3-3의 3인 공격 구조에서 가장 위협적이다. " +
                    "같은 쪽에 오버래핑을 지원하는 공격적 풀백·윙백이 있을 때 시너지가 극대화된다.",
                applicablePositions = Positions.WideForwards,
                allowedDuties = new List<RoleDuty> { RoleDuty.Support, RoleDuty.Attack },
                keyTechnicalAttributes = new List<TechnicalAttribute> { TechnicalAttribute.Dribbling, TechnicalAttribute.Shooting, TechnicalAttribute.Finishing, TechnicalAttribute.FirstTouch },
                keyPhysicalAttributes  = new List<PhysicalAttribute> { PhysicalAttribute.Speed, PhysicalAttribute.Agility, PhysicalAttribute.Balance },
                keyMentalAttributes    = new List<MentalAttribute> { MentalAttribute.Decisions, MentalAttribute.Composure, MentalAttribute.Vision },
                supportInstructions = new PlayerInstructions
                {
                    horizontalMovement = HorizontalMovement.CutInside,
                    crossingTendency   = CrossingTendency.CrossLessOften,
                },
                attackInstructions = new PlayerInstructions
                {
                    horizontalMovement = HorizontalMovement.CutInside,
                    shootingFrequency  = ShootingFrequency.ShootOften,
                    longShotTendency   = LongShotTendency.ShootFromDistance,
                    crossingTendency   = CrossingTendency.CrossLessOften,
                    runFrequency       = RunFrequency.MoreRuns,
                }
            });

            Add(new RoleDefinition
            {
                role = PlayerRole.Raumdeuter,
                displayName = "라움도이터",
                shortDescription = "공간을 찾아 침투하는 움직임의 달인",
                fullDescription =
                    "독일어로 '공간 해석가'. 드리블이나 크로스보다 빈 공간으로의 정확한 침투 런이 특기. " +
                    "볼을 잡는 빈도는 낮지만 결정적인 순간에 나타나 골을 넣는다. " +
                    "볼을 처리하는 능력보다 언제, 어디로 달려야 하는지를 본능적으로 파악하는 축구 지능이 핵심이다.",
                tacticalBehavior =
                    "측면에서 출발해 상대 수비의 약점이 되는 공간으로 끊임없이 이동한다. " +
                    "볼보다 먼저 위치를 잡는 오프더볼 능력이 이 역할의 전부다. " +
                    "상대 수비가 볼을 쫓는 사이 빈 공간으로 파고들어 최소한의 터치로 득점을 완성한다.",
                strengths =
                    "볼을 거의 만지지 않고도 상대 수비 뒷공간을 지속적으로 위협해 득점 기회를 만든다. " +
                    "수비가 마크하기 어려운 '보이지 않는 위협'으로 팀의 공격 다양성을 높인다.",
                weaknesses =
                    "볼 관여율이 낮아 팀이 포제션을 잃으면 경기에서 완전히 사라질 수 있다. " +
                    "정확한 타이밍과 공간 인식 없이는 그저 측면을 맴도는 존재가 되어버린다.",
                bestIn =
                    "볼이 빠르게 이동하는 트랜지션 전술이나 패스 컴비네이션이 발달한 시스템에서 빛난다. " +
                    "정밀한 스루패스를 공급할 수 있는 플레이메이커나 딥 라잉 플레이메이커와 함께할 때 최고의 효율을 발휘한다.",
                applicablePositions = Positions.WideForwards,
                allowedDuties = new List<RoleDuty> { RoleDuty.Attack },
                keyTechnicalAttributes = new List<TechnicalAttribute> { TechnicalAttribute.Finishing, TechnicalAttribute.FirstTouch },
                keyPhysicalAttributes  = new List<PhysicalAttribute> { PhysicalAttribute.Speed, PhysicalAttribute.Agility },
                keyMentalAttributes    = new List<MentalAttribute> { MentalAttribute.Decisions, MentalAttribute.Composure, MentalAttribute.Vision },
                attackInstructions = new PlayerInstructions
                {
                    offTheBallRuns     = OffTheBallRuns.MakeMoreRuns,
                    runFrequency       = RunFrequency.MoreRuns,
                    crossingTendency   = CrossingTendency.CrossLessOften,
                    dribblingTendency  = DribblingTendency.DribbleLess,
                    verticalMovement   = VerticalMovement.GetFurtherForward,
                    shootingFrequency  = ShootingFrequency.ShootOften,
                }
            });

            Add(new RoleDefinition
            {
                role = PlayerRole.WideMidfielder,
                displayName = "와이드 미드필더",
                shortDescription = "측면을 수비하면서 공격 지원도 하는 균형형 사이드 미드필더",
                fullDescription =
                    "LM·RM 위치에서 수비와 공격을 모두 소화하는 균형형 측면 미드필더. " +
                    "전통 4-4-2에서 측면을 담당하며 윙어보다 수비 의무가 크고 풀백보다는 전진 빈도가 높다. " +
                    "측면의 공수 균형을 유지하며 팀 전체 구조가 무너지지 않도록 지원하는 역할이다.",
                tacticalBehavior =
                    "공격 시 측면에서 크로스나 연계 패스를 공급하고 수비 시 빠르게 내려와 풀백을 지원한다. " +
                    "볼이 반대쪽에 있을 때 압축적으로 중앙으로 이동해 팀 형태를 유지한다. " +
                    "Attack 듀티에서는 오버래핑 빈도를 높여 윙어에 가까운 역할을 수행한다.",
                strengths =
                    "측면 수비와 공격을 모두 수행해 팀의 전술 구조를 안정시킨다. " +
                    "다양한 듀티로 전술적 요구에 맞게 역할을 조정할 수 있다.",
                weaknesses =
                    "전문 윙어만큼 공격적이지 않고 전문 풀백만큼 수비적이지도 않아 극단적인 전술에서 효율이 낮다. " +
                    "측면 폭 활용이 제한되어 크로스 전술에서 윙어보다 위협도가 낮다.",
                bestIn =
                    "4-4-2 또는 4-4-1-1처럼 측면 미드필더가 공수 균형을 책임지는 전통적 시스템에 이상적이다. " +
                    "측면 공격은 풀백이 주도하고 와이드 미드필더는 수비와 연계를 담당하는 구조에서 잘 기능한다.",
                applicablePositions = Positions.WideMids,
                allowedDuties = new List<RoleDuty> { RoleDuty.Defend, RoleDuty.Support, RoleDuty.Attack },
                keyTechnicalAttributes = new List<TechnicalAttribute> { TechnicalAttribute.Passing, TechnicalAttribute.Tackling, TechnicalAttribute.Crossing },
                keyPhysicalAttributes  = new List<PhysicalAttribute> { PhysicalAttribute.Stamina, PhysicalAttribute.Speed },
                keyMentalAttributes    = new List<MentalAttribute> { MentalAttribute.WorkRate, MentalAttribute.Teamwork, MentalAttribute.Positioning },
                defendInstructions = new PlayerInstructions
                {
                    verticalMovement   = VerticalMovement.StayBackMore,
                    crossingTendency   = CrossingTendency.CrossLessOften,
                    runFrequency       = RunFrequency.FewerRuns,
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
                role = PlayerRole.InvertedWinger,
                displayName = "인버티드 윙어",
                shortDescription = "하프 스페이스에서 창의적인 패스·크로스·슈팅으로 기회를 만드는 반대발 윙어",
                fullDescription =
                    "반대발로 측면에서 시작해 하프 스페이스로 이동하며 상황에 맞게 패스·크로스·슈팅을 선택하는 다재다능한 공격 자원. " +
                    "인사이드 포워드보다 득점보다 기회 창출에 비중을 두며, 박스 안 공격수를 위한 어시스트 공급이 주된 임무다. " +
                    "드리블로 수비를 끌어낸 뒤 컷백 크로스·원거리 슈팅·스루패스 등 다양한 선택지를 활용하는 유연성이 핵심이다.",
                tacticalBehavior =
                    "터치라인에서 출발해 상대 풀백을 고정시킨 뒤 하프 스페이스로 이동해 볼을 받는다. " +
                    "강발 크로스로 박스 안 공격수에게 정확한 기회를 만들거나, 공간이 열리면 직접 슈팅도 선택한다. " +
                    "Attack 듀티에서는 슈팅 빈도가 증가하지만 기본적으로 어시스트 창출을 우선시한다.",
                strengths =
                    "하프 스페이스 활용으로 상대 수비가 예측하기 어려운 다양한 공격 옵션을 동시에 제공한다. " +
                    "안쪽으로 이동해 동측 풀백·윙백에게 측면 공간을 열어주면서 중앙 위협도 유지한다.",
                weaknesses =
                    "중앙으로 이동 후 측면이 비어 상대 풀백의 오버래핑 공간이 열린다. " +
                    "팀에 인사이드 포워드처럼 높은 득점력을 기대한다면 기대치에 미치지 못할 수 있다.",
                bestIn =
                    "박스 안 타깃맨이나 어드밴스드 포워드가 있어 인버티드 윙어의 크로스를 마무리해줄 때 이상적이다. " +
                    "같은 쪽 풀백이 오버래핑으로 측면 공간을 채워주는 4-3-3·4-2-3-1 구조에서 최대 효율을 발휘한다.",
                applicablePositions = Positions.WideAttackers,
                allowedDuties = new List<RoleDuty> { RoleDuty.Support, RoleDuty.Attack },
                keyTechnicalAttributes = new List<TechnicalAttribute> { TechnicalAttribute.Crossing, TechnicalAttribute.Dribbling, TechnicalAttribute.Shooting, TechnicalAttribute.FirstTouch },
                keyPhysicalAttributes  = new List<PhysicalAttribute> { PhysicalAttribute.Speed, PhysicalAttribute.Agility },
                keyMentalAttributes    = new List<MentalAttribute> { MentalAttribute.Decisions, MentalAttribute.Vision },
                supportInstructions = new PlayerInstructions
                {
                    horizontalMovement = HorizontalMovement.CutInside,
                    dribblingTendency  = DribblingTendency.DribbleMore,
                },
                attackInstructions = new PlayerInstructions
                {
                    horizontalMovement = HorizontalMovement.CutInside,
                    dribblingTendency  = DribblingTendency.DribbleMore,
                    shootingFrequency  = ShootingFrequency.ShootOften,
                    runFrequency       = RunFrequency.MoreRuns,
                    longShotTendency   = LongShotTendency.ShootFromDistance,
                }
            });

            Add(new RoleDefinition
            {
                role = PlayerRole.WidePlaymaker,
                displayName = "와이드 플레이메이커",
                shortDescription = "측면에서 게임을 조율하는 창의적 플레이메이커형 윙어",
                fullDescription =
                    "측면 포지션에 있지만 중앙으로 들어와 팀의 빌드업과 공격을 조율하는 역할. " +
                    "일반 윙어처럼 크로스에 의존하지 않고 패스와 드리블로 공격 흐름을 만든다. " +
                    "측면이라는 공간을 활용해 마크를 피하면서 전체 경기장을 시야에 담고 플레이메이킹을 펼치는 특수한 역할이다.",
                tacticalBehavior =
                    "측면에서 볼을 받은 뒤 중앙으로 이동해 패스 옵션을 만들거나 직접 드리블로 전진한다. " +
                    "내측으로 들어와 공격형 미드필더처럼 플레이하며 스루패스로 공격수에게 기회를 제공한다. " +
                    "크로스보다 패스와 드리블을 통한 빌드업 기여를 우선시하며 볼 소유를 안정시킨다.",
                strengths =
                    "측면에서 시작해 마크를 벗어나 중앙의 밀집 지역을 피해 볼을 받는 이점이 있다. " +
                    "윙어와 플레이메이커의 역할을 동시에 수행해 상대 수비의 집중 마크를 분산시킨다.",
                weaknesses =
                    "측면 폭 활용이 적어 상대 수비가 쉽게 내측으로 밀어낼 수 있다. " +
                    "크로스 기여가 낮아 크로스 기반 전술에서는 적합하지 않다.",
                bestIn =
                    "포제션 기반 전술에서 측면을 넓게 활용하면서 중앙 빌드업도 강화하고 싶을 때 유용하다. " +
                    "4-3-3이나 4-2-3-1에서 한쪽은 전통 윙어, 반대쪽은 와이드 플레이메이커로 비대칭 전술을 구사할 수 있다.",
                applicablePositions = Positions.WideAttackers,
                allowedDuties = new List<RoleDuty> { RoleDuty.Support, RoleDuty.Attack },
                keyTechnicalAttributes = new List<TechnicalAttribute> { TechnicalAttribute.Passing, TechnicalAttribute.LongPassing, TechnicalAttribute.Dribbling, TechnicalAttribute.FirstTouch },
                keyPhysicalAttributes  = new List<PhysicalAttribute> { PhysicalAttribute.Agility, PhysicalAttribute.Stamina },
                keyMentalAttributes    = new List<MentalAttribute> { MentalAttribute.Vision, MentalAttribute.Decisions, MentalAttribute.Composure },
                supportInstructions = new PlayerInstructions
                {
                    horizontalMovement = HorizontalMovement.CutInside,
                    passingStyle       = PassingStyle.Risky,
                    crossingTendency   = CrossingTendency.CrossLessOften,
                },
                attackInstructions = new PlayerInstructions
                {
                    horizontalMovement = HorizontalMovement.CutInside,
                    passingStyle       = PassingStyle.Risky,
                    crossingTendency   = CrossingTendency.CrossLessOften,
                    dribblingTendency  = DribblingTendency.DribbleMore,
                    runFrequency       = RunFrequency.MoreRuns,
                    offTheBallRuns     = OffTheBallRuns.MakeMoreRuns,
                }
            });

            Add(new RoleDefinition
            {
                role = PlayerRole.WideTargetForward,
                displayName = "와이드 타깃 포워드",
                shortDescription = "측면에서 신체적 위협으로 공간을 열고 볼을 잡아주는 타깃형 윙어",
                fullDescription =
                    "기술적 드리블보다 신체·공중볼로 상대 풀백을 압도하는 독특한 측면 공격수. " +
                    "롱볼이나 대각선 크로스를 측면에서 가슴·헤딩으로 받아 내려 동료에게 연결하거나 직접 중앙으로 파고든다. " +
                    "상대 풀백에게 지속적인 신체적 부담을 주면서 중앙 공격수나 박스 침투 미드필더를 위한 공간을 창출하는 것이 핵심이다.",
                tacticalBehavior =
                    "터치라인 근처에 넓게 위치해 롱볼이나 대각선 크로스를 경합한다. " +
                    "볼을 잡으면 동료의 오버래핑을 기다리거나 반전 드리블로 중앙을 공략한다. " +
                    "박스 안으로 침투해 헤딩 슈팅을 시도하기도 하며, Attack 듀티에서 크로스 공급 빈도가 높아진다.",
                strengths =
                    "상대 풀백이 키·몸싸움에 약할 때 측면에서 압도적인 경합을 제공해 팀에 독특한 공격 옵션을 만든다. " +
                    "롱볼·세트피스 상황에서 측면 제공점이 되어 직선적 공격 루트를 다양화한다.",
                weaknesses =
                    "스피드가 빠른 상대 풀백에게는 1대1 돌파가 제한되며 공중전 우위가 없으면 역할이 크게 제한된다. " +
                    "포제션 기반의 기술적 시스템에서는 드리블·패스 중심 윙어보다 활용도가 낮다.",
                bestIn =
                    "롱볼이나 크로스 전술을 사용하는 팀에서 측면 타깃맨으로 배치해 독특한 공격 옵션을 추가한다. " +
                    "반대쪽에 빠른 기술형 윙어를 두고 이 선수가 신체적 위협을 분담하는 비대칭 전술에 가장 잘 맞는다.",
                applicablePositions = Positions.WideForwards,
                allowedDuties = new List<RoleDuty> { RoleDuty.Support, RoleDuty.Attack },
                keyTechnicalAttributes = new List<TechnicalAttribute> { TechnicalAttribute.Heading, TechnicalAttribute.FirstTouch, TechnicalAttribute.Crossing },
                keyPhysicalAttributes  = new List<PhysicalAttribute> { PhysicalAttribute.Strength, PhysicalAttribute.Jumping, PhysicalAttribute.Stamina },
                keyMentalAttributes    = new List<MentalAttribute> { MentalAttribute.Teamwork, MentalAttribute.Decisions, MentalAttribute.Courage },
                supportInstructions = new PlayerInstructions
                {
                    horizontalMovement = HorizontalMovement.HugTouchline,
                    dribblingTendency  = DribblingTendency.DribbleLess,
                },
                attackInstructions = new PlayerInstructions
                {
                    horizontalMovement = HorizontalMovement.HugTouchline,
                    crossingTendency   = CrossingTendency.CrossMoreOften,
                    dribblingTendency  = DribblingTendency.DribbleLess,
                    offTheBallRuns     = OffTheBallRuns.MakeMoreRuns,
                }
            });
        }
    }
}