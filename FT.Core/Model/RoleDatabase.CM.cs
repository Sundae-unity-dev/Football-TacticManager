using System.Collections.Generic;

namespace FootballTactician.Data
{
    public static partial class RoleDatabase
    {
        private static void BuildCm()
        {
            Add(new RoleDefinition
            {
                role = PlayerRole.BoxToBox,
                displayName = "박스투박스 미드필더",
                shortDescription = "양쪽 페널티 박스를 오가는 올라운더",
                fullDescription =
                    "공수 양면에서 팀의 엔진 역할을 하는 미드필더. 수비 시 태클과 압박으로 " +
                    "볼을 탈취하고, 공격 시 전방까지 달려가 슈팅이나 어시스트를 노린다. " +
                    "높은 체력이 필수다. " +
                    "전술적 제약보다 본능적인 기여를 중심으로 뛰며, 어디서든 나타나 팀에 힘을 보태는 미드필드의 심장이다.",
                tacticalBehavior =
                    "볼이 있는 곳이라면 어디든 달려가 기여한다. " +
                    "공격 시 박스 침투, 수비 시 중원 압박 모두 적극적으로 수행한다. " +
                    "세컨드볼 경쟁에 강하며 공수 전환 시 반응 속도가 빨라 팀의 기세를 이어주는 역할을 한다.",
                strengths =
                    "체력과 전투력으로 중원 전체를 지배하며 공수 모두에서 꾸준한 기여를 한다. " +
                    "득점과 도움 모두 가능해 세트피스·세컨드볼 상황에서 큰 위협이 된다.",
                weaknesses =
                    "특화된 역할(플레이메이커, 볼 탈취 미드필더)에 비해 개별 역할에서의 전문성이 낮다. " +
                    "90분 내내 높은 강도로 뛰어야 해 체력 관리에 실패하면 후반에 급격히 효율이 떨어진다.",
                bestIn =
                    "4-3-3이나 4-2-3-1의 중원에서 CDM과 공격형 미드필더 사이를 자유롭게 채우는 역할로 이상적이다. " +
                    "딥 라잉 플레이메이커나 앵커맨과 함께 배치해 창의성과 수비 안정을 보완하면 더욱 효과적이다.",
                applicablePositions = Position.CM,
                allowedDuties = new List<RoleDuty> { RoleDuty.Support },
                keyTechnicalAttributes = new List<TechnicalAttribute> { TechnicalAttribute.Passing, TechnicalAttribute.Tackling, TechnicalAttribute.Shooting },
                keyPhysicalAttributes  = new List<PhysicalAttribute> { PhysicalAttribute.Stamina, PhysicalAttribute.Speed },
                keyMentalAttributes    = new List<MentalAttribute> { MentalAttribute.WorkRate, MentalAttribute.Decisions, MentalAttribute.Teamwork },
                supportInstructions = new PlayerInstructions
                {
                    runFrequency       = RunFrequency.MoreRuns,
                    verticalMovement   = VerticalMovement.GetFurtherForward,
                    pressingIntensity  = PressingIntensity.PressMore,
                    offTheBallRuns     = OffTheBallRuns.MakeMoreRuns,
                }
            });

            Add(new RoleDefinition
            {
                role = PlayerRole.Mezzala,
                displayName = "메짤라",
                shortDescription = "측면에서 전방 공간을 침투하는 미드필더",
                fullDescription =
                    "중앙 미드필더가 측면으로 이동해 전방 공간으로 침투하는 역할. " +
                    "윙어보다 안쪽에서 움직이며 상대 수비 사이의 공간을 공략한다. " +
                    "이탈리아어로 '절반'을 의미하는 이 역할은 미드필더와 공격수 사이 공간을 귀신같이 파고드는 현대 축구의 핵심 역할 중 하나다.",
                tacticalBehavior =
                    "볼이 반대편으로 이동하면 전방 공간으로 침투 런을 시도한다. " +
                    "전방에서 패스를 받아 슈팅이나 키패스를 노린다. " +
                    "Attack 듀티에서는 사실상 두 번째 공격수처럼 기능하며 득점 기여도가 높다.",
                strengths =
                    "중원에서 전방으로 움직이며 상대 수비가 예측하기 어려운 3번째 공격 옵션을 만든다. " +
                    "전방 공간 침투로 수비 라인을 끌어내려 팀의 공격 공간을 넓힌다.",
                weaknesses =
                    "전진 시 뒤를 커버하는 선수가 없으면 중원이 비어 역습에 취약해진다. " +
                    "앵커맨이나 딥 라잉 플레이메이커가 없으면 팀이 공수 균형을 잃기 쉽다.",
                bestIn =
                    "앵커맨이 뒤를 받쳐주는 3인 미드필드(4-3-3, 3-5-2)에서 한쪽 CM으로 이상적이다. " +
                    "반대편에 카리예로나 딥 라잉 플레이메이커를 두어 균형을 맞추는 비대칭 미드필드 구성이 효과적이다.",
                applicablePositions = Position.CM,
                allowedDuties = new List<RoleDuty> { RoleDuty.Support, RoleDuty.Attack },
                keyTechnicalAttributes = new List<TechnicalAttribute> { TechnicalAttribute.Dribbling, TechnicalAttribute.Passing, TechnicalAttribute.Shooting },
                keyPhysicalAttributes  = new List<PhysicalAttribute> { PhysicalAttribute.Speed, PhysicalAttribute.Agility, PhysicalAttribute.Stamina },
                keyMentalAttributes    = new List<MentalAttribute> { MentalAttribute.Vision, MentalAttribute.Decisions, MentalAttribute.OffTheBall },
                supportInstructions = new PlayerInstructions
                {
                    runFrequency      = RunFrequency.MoreRuns,
                    offTheBallRuns    = OffTheBallRuns.MakeMoreRuns,
                    verticalMovement  = VerticalMovement.GetFurtherForward,
                    dribblingTendency = DribblingTendency.DribbleMore,
                },
                attackInstructions = new PlayerInstructions
                {
                    runFrequency      = RunFrequency.MoreRuns,
                    offTheBallRuns    = OffTheBallRuns.MakeMoreRuns,
                    verticalMovement  = VerticalMovement.GetFurtherForward,
                    shootingFrequency = ShootingFrequency.ShootOften,
                    longShotTendency  = LongShotTendency.ShootFromDistance,
                }
            });

            Add(new RoleDefinition
            {
                role = PlayerRole.Carrilero,
                displayName = "카리예로",
                shortDescription = "측면을 커버하며 빌드업을 돕는 미드필더",
                fullDescription =
                    "윙백이 전진했을 때 생기는 측면 공간을 메우는 역할. " +
                    "공격적이지 않지만 수비 커버와 짧은 패스 연결로 팀의 구조를 유지한다. " +
                    "스페인 축구 용어로 '복도를 달리는 자'라는 뜻이며, 팀의 공수 연결 통로를 유지하는 묵묵한 역할이다.",
                tacticalBehavior =
                    "같은 쪽 윙백이 올라가면 측면으로 이동해 수비 공간을 메운다. " +
                    "패스 연결과 위치 선정이 핵심이며 전방 침투보다 안전한 플레이를 선호한다. " +
                    "팀이 볼을 잃었을 때 빠르게 4-4-2형 블록을 형성하기 위해 즉각 복귀한다.",
                strengths =
                    "공격적 윙백이 전진해도 측면 커버를 제공해 팀의 수비 구조가 무너지지 않도록 한다. " +
                    "짧고 안전한 패스로 팀의 포제션을 유지하며 리듬을 잃지 않게 한다.",
                weaknesses =
                    "공격 기여가 낮아 팀 전체의 공격 화력을 제한할 수 있다. " +
                    "창의성이 없어 상대 수비가 내려앉으면 돌파구를 만들지 못한다.",
                bestIn =
                    "공격적 윙백을 사용하는 3-5-2나 5-3-2에서 미드필드 측면 커버로 이상적이다. " +
                    "반대쪽 메짤라나 박스투박스 미드필더의 공격적 움직임을 수비적으로 균형 잡아주는 역할로 활용된다.",
                applicablePositions = Position.CM,
                allowedDuties = new List<RoleDuty> { RoleDuty.Support },
                keyTechnicalAttributes = new List<TechnicalAttribute> { TechnicalAttribute.Passing, TechnicalAttribute.Tackling, TechnicalAttribute.FirstTouch },
                keyPhysicalAttributes  = new List<PhysicalAttribute> { PhysicalAttribute.Stamina },
                keyMentalAttributes    = new List<MentalAttribute> { MentalAttribute.Teamwork, MentalAttribute.Positioning, MentalAttribute.WorkRate },
                supportInstructions = new PlayerInstructions
                {
                    passingStyle      = PassingStyle.Safe,
                }
            });

            Add(new RoleDefinition
            {
                role = PlayerRole.AdvancedPlaymaker,
                displayName = "전진형 플레이메이커",
                shortDescription = "공격 3선에서 창의적인 패스로 기회를 만드는 플레이메이커",
                fullDescription =
                    "중원 혹은 공격형 미드필더 위치에서 팀의 공격을 지휘하는 역할. " +
                    "뛰어난 시야와 패스로 공격수에게 결정적인 기회를 제공한다. " +
                    "수비 기여는 낮지만 창의성과 볼 처리 능력이 탁월해야 한다. " +
                    "딥 라잉 플레이메이커보다 전방에서 활동하며 공격 최전선과 중원 사이의 매개자 역할을 한다.",
                tacticalBehavior =
                    "자유롭게 움직이며 볼을 요구하고 수비 블록 사이에서 공간을 찾는다. " +
                    "단순한 옵션이 있어도 더 위험한 패스를 시도하는 경향이 있다. " +
                    "볼을 잡으면 주변을 재빠르게 스캔해 최적의 수신자에게 결정적인 패스를 공급한다.",
                strengths =
                    "상대 수비 블록 사이에서 창의적인 스루패스와 킬 패스로 결정적 기회를 만든다. " +
                    "자유로운 움직임으로 상대 수비 구조를 혼란스럽게 만들고 동료의 공간을 창출한다.",
                weaknesses =
                    "수비 기여가 낮아 팀이 이 선수 주변에 수비적 보호막을 두어야 한다. " +
                    "볼을 자주 요구하므로 팀의 전술이 이 선수 중심으로 설계되어야 효과가 극대화된다.",
                bestIn =
                    "4-2-3-1에서 CAM 포지션으로 두 CDM의 보호를 받으며 공격을 조율할 때 최적이다. " +
                    "공수 균형을 잡아주는 박스투박스 미드필더나 볼 탈취 미드필더와 함께 배치하면 효과적이다.",
                applicablePositions = Position.CM | Position.CAM,
                allowedDuties = new List<RoleDuty> { RoleDuty.Support, RoleDuty.Attack },
                keyTechnicalAttributes = new List<TechnicalAttribute> { TechnicalAttribute.Passing, TechnicalAttribute.LongPassing, TechnicalAttribute.FirstTouch, TechnicalAttribute.Dribbling },
                keyPhysicalAttributes  = new List<PhysicalAttribute> { PhysicalAttribute.Agility },
                keyMentalAttributes    = new List<MentalAttribute> { MentalAttribute.Vision, MentalAttribute.Decisions, MentalAttribute.Composure },
                supportInstructions = new PlayerInstructions
                {
                    passingStyle      = PassingStyle.Risky,
                    verticalMovement  = VerticalMovement.GetFurtherForward,
                    dribblingTendency = DribblingTendency.DribbleMore,
                    runFrequency      = RunFrequency.MoreRuns,
                },
                attackInstructions = new PlayerInstructions
                {
                    passingStyle      = PassingStyle.Risky,
                    verticalMovement  = VerticalMovement.GetFurtherForward,
                    dribblingTendency = DribblingTendency.DribbleMore,
                    offTheBallRuns    = OffTheBallRuns.MakeMoreRuns,
                }
            });

            Add(new RoleDefinition
            {
                role = PlayerRole.CentralMidfielder,
                displayName = "중앙 미드필더",
                shortDescription = "수비와 공격을 균형 있게 소화하는 기본 CM",
                fullDescription =
                    "중앙 미드필드에서 공수를 균형 있게 소화하는 표준형 역할. " +
                    "특정 전술에 치우치지 않고 패스 연결, 공간 커버, 세컨드볼 경쟁을 모두 무난하게 수행한다. " +
                    "어떤 전술 시스템에서도 안정적으로 기능하는 믿을 수 있는 미드필드 자원이다.",
                tacticalBehavior =
                    "볼을 잡으면 가장 안전하고 효율적인 패스를 선택한다. " +
                    "수비 전환 시 포지션으로 복귀해 중원 구조를 유지한다. " +
                    "Defend 듀티에서는 더 뒤에 머물며, Attack 듀티에서는 전방 침투 빈도를 높인다.",
                strengths =
                    "다양한 전술과 파트너 조합에 유연하게 적응해 팀의 중원 구조를 안정시킨다. " +
                    "공수 균형이 잡혀 있어 특화된 미드필더들 사이에서 균형추 역할을 한다.",
                weaknesses =
                    "특화된 역할(플레이메이커, 박스투박스 등)에 비해 두드러지는 강점이 없다. " +
                    "고급 전술에서 특수한 역할을 원할 때 선택지에서 밀릴 수 있다.",
                bestIn =
                    "4-3-3, 4-2-3-1 등 대부분의 시스템에서 중원을 안정시키는 기본 자원으로 활용된다. " +
                    "더 공격적이거나 수비적인 미드필더들 옆에 배치해 전체적인 균형을 제공한다.",
                applicablePositions = Position.CM,
                allowedDuties = new List<RoleDuty> { RoleDuty.Defend, RoleDuty.Support, RoleDuty.Attack },
                keyTechnicalAttributes = new List<TechnicalAttribute> { TechnicalAttribute.Passing, TechnicalAttribute.Tackling, TechnicalAttribute.FirstTouch },
                keyPhysicalAttributes  = new List<PhysicalAttribute> { PhysicalAttribute.Stamina },
                keyMentalAttributes    = new List<MentalAttribute> { MentalAttribute.Positioning, MentalAttribute.Decisions, MentalAttribute.Teamwork },
                defendInstructions = new PlayerInstructions
                {
                    verticalMovement  = VerticalMovement.StayBackMore,
                    passingStyle      = PassingStyle.Safe,
                    runFrequency      = RunFrequency.FewerRuns,
                },
                attackInstructions = new PlayerInstructions
                {
                    verticalMovement  = VerticalMovement.GetFurtherForward,
                    runFrequency      = RunFrequency.MoreRuns,
                    offTheBallRuns    = OffTheBallRuns.MakeMoreRuns,
                }
            });

            Add(new RoleDefinition
            {
                role = PlayerRole.RoamingPlaymaker,
                displayName = "로밍 플레이메이커",
                shortDescription = "경기장 전체를 자유롭게 누비며 게임을 만드는 플레이메이커",
                fullDescription =
                    "특정 포지션에 고정되지 않고 경기장 전체를 누비며 볼을 요구하고 팀의 공격을 조율하는 역할. " +
                    "딥 라잉 플레이메이커보다 전방에서도 활동하고 어드밴스드 플레이메이커보다 후방에서도 내려온다. " +
                    "팀의 어느 위치에서든 창의적인 패스와 드리블로 상대 수비 구조에 혼란을 주는 자유형 플레이메이커다.",
                tacticalBehavior =
                    "볼의 흐름을 읽고 필요한 위치로 자유롭게 이동해 빌드업을 지원하거나 공격을 완성한다. " +
                    "때로는 수비 라인까지 내려와 볼을 받고, 때로는 박스 근처까지 올라가 결정적 패스를 시도한다. " +
                    "팀 전체의 움직임을 머릿속에 그리며 항상 가장 효율적인 위치에 나타나는 '두뇌형' 플레이어다.",
                strengths =
                    "자유로운 움직임으로 상대 수비가 마크 위치를 잡기 어려워 항상 볼을 유리하게 받을 수 있다. " +
                    "후방과 전방을 모두 지원해 팀의 빌드업과 공격 연결을 동시에 강화한다.",
                weaknesses =
                    "포지션 이탈이 잦아 수비 전환 시 중원에 빈 공간이 자주 생긴다. " +
                    "이 선수의 자유로운 움직임을 팀원들이 이해하고 반응해야 효과가 극대화되어 팀워크가 중요하다.",
                bestIn =
                    "중원에 앵커맨이나 볼 탈취 미드필더가 수비를 커버해주는 4-3-3이나 3-5-2에서 자유롭게 로밍할 수 있다. " +
                    "전술적 규율보다 창의성과 유연성을 중시하는 팀에서 가장 효과적으로 기능한다.",
                applicablePositions = Position.CM | Position.CDM,
                allowedDuties = new List<RoleDuty> { RoleDuty.Support, RoleDuty.Attack },
                keyTechnicalAttributes = new List<TechnicalAttribute> { TechnicalAttribute.Passing, TechnicalAttribute.LongPassing, TechnicalAttribute.Dribbling, TechnicalAttribute.FirstTouch },
                keyPhysicalAttributes  = new List<PhysicalAttribute> { PhysicalAttribute.Stamina, PhysicalAttribute.Agility },
                keyMentalAttributes    = new List<MentalAttribute> { MentalAttribute.Vision, MentalAttribute.Decisions, MentalAttribute.Composure },
                supportInstructions = new PlayerInstructions
                {
                    passingStyle      = PassingStyle.Risky,
                    runFrequency      = RunFrequency.MoreRuns,
                    offTheBallRuns    = OffTheBallRuns.MakeMoreRuns,
                },
                attackInstructions = new PlayerInstructions
                {
                    passingStyle      = PassingStyle.Risky,
                    runFrequency      = RunFrequency.MoreRuns,
                    verticalMovement  = VerticalMovement.GetFurtherForward,
                    dribblingTendency = DribblingTendency.DribbleMore,
                    offTheBallRuns    = OffTheBallRuns.MakeMoreRuns,
                }
            });
        }
    }
}
