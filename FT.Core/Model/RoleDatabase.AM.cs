using System.Collections.Generic;

namespace FootballTactician.Data
{
    public static partial class RoleDatabase
    {
        private static void BuildAm()
        {
            Add(new RoleDefinition
            {
                role = PlayerRole.ShadowStriker,
                displayName = "섀도우 스트라이커",
                shortDescription = "스트라이커 뒤 사각지대에서 나타나 박스를 침투하는 2선 득점원",
                fullDescription =
                    "CAM 포지션이지만 실질적으로 두 번째 공격수처럼 기능하는 역할. " +
                    "전방 공격수가 상대 수비를 끌어당기면 생기는 사각지대에 타이밍 좋게 침투해 슈팅을 완성한다. " +
                    "상대 미드필더와 수비 라인 사이에서 '그림자처럼' 움직여 마크가 어렵고, " +
                    "볼을 자주 만지기보다 결정적 순간에 나타나 득점하는 오프더볼 능력이 핵심이다.",
                tacticalBehavior =
                    "볼이 없을 때 끊임없이 침투 런으로 수비 라인을 흔든다. " +
                    "전방 공격수가 볼을 끌어당기거나 측면에서 공격이 전개될 때 박스 안으로 파고든다. " +
                    "원터치 마무리 능력이 중요하며, 볼을 잡으면 주저 없이 슈팅을 시도한다.",
                strengths =
                    "수비가 전방 공격수에 집중하는 사이 마크가 풀린 상태로 슈팅 기회를 만든다. " +
                    "사실상 투톱처럼 기능해 단일 공격수 시스템에서 상대에게 두 가지 위협을 동시에 제공한다.",
                weaknesses =
                    "수비 의무가 거의 없어 팀이 볼을 잃으면 중원에 빈 공간이 생길 수 있다. " +
                    "전방 공격수가 공간을 만들어주지 않으면 침투 런이 무의미해진다.",
                bestIn =
                    "4-2-3-1이나 4-4-1-1에서 CF/ST 바로 뒤에 위치해 2선 득점원으로 최대 효율을 발휘한다. " +
                    "상대 CB를 끌어낼 타깃맨이나 프레싱 포워드와 조합하면 침투 공간이 자동으로 만들어진다.",
                applicablePositions = Position.CAM,
                allowedDuties = new List<RoleDuty> { RoleDuty.Attack },
                keyTechnicalAttributes = new List<TechnicalAttribute> { TechnicalAttribute.Finishing, TechnicalAttribute.FirstTouch, TechnicalAttribute.Dribbling },
                keyPhysicalAttributes  = new List<PhysicalAttribute> { PhysicalAttribute.Speed, PhysicalAttribute.Agility },
                keyMentalAttributes    = new List<MentalAttribute> { MentalAttribute.Decisions, MentalAttribute.Composure, MentalAttribute.Vision },
                attackInstructions = new PlayerInstructions
                {
                    offTheBallRuns    = OffTheBallRuns.MakeMoreRuns,
                    runFrequency      = RunFrequency.MoreRuns,
                    verticalMovement  = VerticalMovement.GetFurtherForward,
                    shootingFrequency = ShootingFrequency.ShootOften,
                }
            });

            Add(new RoleDefinition
            {
                role = PlayerRole.Trequartista,
                displayName = "트레콰르티스타",
                shortDescription = "완전한 창의적 자유를 가진 공격형 미드필더",
                fullDescription =
                    "전술적 제약 없이 자유롭게 움직이며 팀의 창의성을 책임지는 역할. " +
                    "수비 의무가 거의 없으며 오직 공격적 플레이에만 집중한다. " +
                    "팀의 나머지 선수들이 이 선수를 위해 뛰는 구조가 필요하다. " +
                    "이탈리아어로 '4분의 3 자리'를 뜻하며, 전통적인 10번보다 더 자유롭고 예측 불가능한 플레이로 상대를 혼란에 빠뜨린다.",
                tacticalBehavior =
                    "포지션에 구애받지 않고 자유롭게 이동하며 볼을 받는다. " +
                    "드리블, 힐패스, 칩킥 등 창의적인 플레이를 즐기며 수비는 거의 하지 않는다. " +
                    "낮게 내려와 볼을 받기도 하고 공격수 뒤로 침투하기도 해 상대 수비의 위치 선정을 끊임없이 흔든다.",
                strengths =
                    "예측 불가능한 움직임과 창의적 플레이로 상대 수비 구조를 완전히 무너뜨릴 수 있다. " +
                    "팀에서 가장 재능 있는 선수에게 완전한 자유를 줘 그 창의성을 최대로 끌어낸다.",
                weaknesses =
                    "수비 기여가 사실상 0에 가까워 팀의 나머지 10명이 훨씬 더 많은 수비 부담을 진다. " +
                    "전술적 규율이 없어 고압적인 상대를 만나면 완전히 봉쇄될 수 있다.",
                bestIn =
                    "수비적으로 견고한 두 CDM이 뒤를 받쳐주는 4-3-1-2나 3-5-2에서 최고의 효율을 발휘한다. " +
                    "전술보다 개인 창의성을 우선시하는 팀에서만 제대로 기능하며, 규율 중심 전술에는 적합하지 않다.",
                applicablePositions = Position.CAM,
                allowedDuties = new List<RoleDuty> { RoleDuty.Attack },
                keyTechnicalAttributes = new List<TechnicalAttribute> { TechnicalAttribute.Passing, TechnicalAttribute.Dribbling, TechnicalAttribute.FirstTouch, TechnicalAttribute.LongPassing },
                keyPhysicalAttributes  = new List<PhysicalAttribute> { PhysicalAttribute.Agility, PhysicalAttribute.Balance },
                keyMentalAttributes    = new List<MentalAttribute> { MentalAttribute.Vision, MentalAttribute.Composure, MentalAttribute.Decisions },
                attackInstructions = new PlayerInstructions
                {
                    passingStyle      = PassingStyle.Risky,
                    dribblingTendency = DribblingTendency.DribbleMore,
                    pressingIntensity = PressingIntensity.PressLess,
                    runFrequency      = RunFrequency.MoreRuns,
                    offTheBallRuns    = OffTheBallRuns.MakeMoreRuns,
                }
            });

            Add(new RoleDefinition
            {
                role = PlayerRole.Enganche,
                displayName = "엔간체",
                shortDescription = "팀 전체의 연결 고리 역할을 하는 플레이메이커",
                fullDescription =
                    "남미 축구에서 유래한 역할로, 팀의 모든 공격이 이 선수를 통해 흘러가는 구조. " +
                    "트레콰르티스타보다 정적이며 포지션을 지키며 주변 선수들에게 볼을 공급한다. " +
                    "스페인어로 '고리'를 뜻하며 수비와 공격을 잇는 중심 연결자로서 모든 공격적 흐름의 시작점이 된다.",
                tacticalBehavior =
                    "공격 3분의 1 구역의 중앙에 위치하며 볼을 요구한다. " +
                    "빠르고 정확한 패스로 공격 흐름을 만들며 직접 슈팅은 거의 하지 않는다. " +
                    "볼을 받는 즉시 짧고 정확한 원터치 배급으로 팀의 빠른 공격 전개를 돕는다.",
                strengths =
                    "중앙에 위치해 좌우 모든 방향으로 패스를 공급하며 팀 공격의 흐름을 총괄한다. " +
                    "트레콰르티스타보다 안정적이어서 전술적 일관성이 높다.",
                weaknesses =
                    "이동 범위가 좁아 상대가 이 선수를 마크해버리면 팀 전체가 공격 옵션을 잃는다. " +
                    "역습 전환 시 수비 기여가 없어 중원에 빈 공간이 자주 생긴다.",
                bestIn =
                    "두 CDM이 수비를 책임지는 4-3-1-2나 3-4-1-2처럼 중앙 공격형 미드필더 위치가 확보된 시스템에 최적이다. " +
                    "측면 공격수나 스트라이커가 직접 공간을 만들어줄 때 이 선수의 배급 능력이 더욱 빛난다.",
                applicablePositions = Position.CAM,
                allowedDuties = new List<RoleDuty> { RoleDuty.Support },
                keyTechnicalAttributes = new List<TechnicalAttribute> { TechnicalAttribute.Passing, TechnicalAttribute.FirstTouch, TechnicalAttribute.LongPassing },
                keyPhysicalAttributes  = new List<PhysicalAttribute> { PhysicalAttribute.Agility, PhysicalAttribute.Balance },
                keyMentalAttributes    = new List<MentalAttribute> { MentalAttribute.Vision, MentalAttribute.Composure, MentalAttribute.Decisions },
                supportInstructions = new PlayerInstructions
                {
                    passingStyle      = PassingStyle.Risky,
                    passingRange      = PassingRange.Short,
                    pressingIntensity = PressingIntensity.PressLess,
                    runFrequency      = RunFrequency.FewerRuns,
                }
            });

            Add(new RoleDefinition
            {
                role = PlayerRole.AttackingMidfielder,
                displayName = "공격형 미드필더",
                shortDescription = "공격 3선에서 득점과 어시스트를 균형 있게 노리는 10번",
                fullDescription =
                    "공격과 미드필드 사이 공간에서 볼을 받아 패스·드리블·슈팅으로 공격 기회를 만드는 전통적 '10번' 역할. " +
                    "트레콰르티스타보다 전술적으로 규율이 있고, 엔간체보다 직접 위협을 더 많이 가한다. " +
                    "공격 3선에서 가장 균형 잡힌 창의적 역할로, 어시스트와 직접 득점을 모두 노리는 만능 공격형 미드필더다.",
                tacticalBehavior =
                    "공격 3분의 1 구역의 하프 스페이스를 주로 활용하며 수비 블록 사이에서 볼을 받는다. " +
                    "직접 슈팅과 창의적 스루패스를 모두 구사하며 상황에 맞게 선택한다. " +
                    "수비 기여는 낮지만 볼을 잃으면 즉각 압박으로 재탈취를 노리며 조직 수비에도 최소한 가담한다.",
                strengths =
                    "공격 3선과 최전방을 연결하며 팀 공격의 두뇌 역할을 한다. " +
                    "득점과 어시스트 모두 기대할 수 있어 상대 수비가 집중 마크해야 하는 핵심 위협이 된다.",
                weaknesses =
                    "수비 가담이 낮아 이 선수를 보호할 CDM이 필수적으로 필요하다. " +
                    "상대가 집중 마크로 봉쇄하면 팀 전체의 창의성이 크게 감소할 수 있다.",
                bestIn =
                    "4-2-3-1에서 두 CDM의 보호를 받으며 공격 전체를 조율하는 역할로 가장 이상적이다. " +
                    "양쪽 윙어가 측면을 담당하고 이 선수가 중앙 하프 스페이스를 자유롭게 활용하는 구조에서 최대 효율을 낸다.",
                applicablePositions = Position.CAM,
                allowedDuties = new List<RoleDuty> { RoleDuty.Support, RoleDuty.Attack },
                keyTechnicalAttributes = new List<TechnicalAttribute> { TechnicalAttribute.Passing, TechnicalAttribute.Dribbling, TechnicalAttribute.Shooting, TechnicalAttribute.FirstTouch },
                keyPhysicalAttributes  = new List<PhysicalAttribute> { PhysicalAttribute.Agility, PhysicalAttribute.Balance },
                keyMentalAttributes    = new List<MentalAttribute> { MentalAttribute.Vision, MentalAttribute.Decisions, MentalAttribute.Composure },
                attackInstructions = new PlayerInstructions
                {
                    passingStyle      = PassingStyle.Risky,
                    verticalMovement  = VerticalMovement.GetFurtherForward,
                    dribblingTendency = DribblingTendency.DribbleMore,
                    runFrequency      = RunFrequency.MoreRuns,
                    offTheBallRuns    = OffTheBallRuns.MakeMoreRuns,
                }
            });
        }
    }
}
