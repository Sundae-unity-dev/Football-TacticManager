using System.Collections.Generic;

namespace FootballTactician.Data
{
    public static partial class RoleDatabase
    {
        private static void BuildSt()
        {
            Add(new RoleDefinition
            {
                role = PlayerRole.AdvancedForward,
                displayName = "전진형 포워드",
                shortDescription = "전방에서 골과 어시스트를 동시에 노리는 공격수",
                fullDescription =
                    "수비 라인 뒤 공간으로 끊임없이 달리며 득점 기회를 만드는 역할. " +
                    "골 결정력뿐 아니라 패스와 연계 플레이도 소화하는 완성형 공격수. " +
                    "빠른 침투, 정교한 마무리, 동료와의 원활한 연계까지 갖춘 현대 축구의 표준적인 공격수 유형이다.",
                tacticalBehavior =
                    "수비 라인 뒤로 끊임없이 달리며 스루패스를 받는다. " +
                    "다양한 형태의 슈팅(발리, 헤딩 등)을 구사하며 동료와의 연계도 적극적이다. " +
                    "동료가 볼을 잡으면 즉각적인 침투 런으로 수비 라인을 무너뜨리는 움직임이 핵심이다.",
                strengths =
                    "다양한 슈팅 기술과 연계 플레이로 어떤 전술에서도 무난하게 득점에 기여한다. " +
                    "수비 라인 뒤 공간 침투로 팀의 빠른 역습 전환을 이끄는 역할도 한다.",
                weaknesses =
                    "타깃맨처럼 롱볼을 버티거나 폴스 나인처럼 빌드업을 조율하는 특수한 역할에 비해 차별성이 낮다. " +
                    "스루패스를 공급해 줄 미드필더가 없으면 고립되기 쉽다.",
                bestIn =
                    "스루패스와 침투 공간을 만들어줄 수 있는 어드밴스드 플레이메이커나 트레콰르티스타와 조합될 때 최적이다. " +
                    "대부분의 4-3-3, 4-2-3-1, 4-4-2 시스템에서 첫 번째 선택지가 되는 범용 공격수 역할이다.",
                applicablePositions = Positions.Forwards,
                allowedDuties = new List<RoleDuty> { RoleDuty.Support, RoleDuty.Attack },
                keyTechnicalAttributes = new List<TechnicalAttribute> { TechnicalAttribute.Finishing, TechnicalAttribute.FirstTouch, TechnicalAttribute.Dribbling, TechnicalAttribute.Heading },
                keyPhysicalAttributes  = new List<PhysicalAttribute> { PhysicalAttribute.Speed, PhysicalAttribute.Agility },
                keyMentalAttributes    = new List<MentalAttribute> { MentalAttribute.Composure, MentalAttribute.Decisions, MentalAttribute.OffTheBall },
                supportInstructions = new PlayerInstructions
                {
                    shootingFrequency  = ShootingFrequency.ShootOften,
                    offTheBallRuns     = OffTheBallRuns.MakeMoreRuns,
                },
                attackInstructions = new PlayerInstructions
                {
                    shootingFrequency  = ShootingFrequency.ShootOften,
                    offTheBallRuns     = OffTheBallRuns.MakeMoreRuns,
                    verticalMovement   = VerticalMovement.GetFurtherForward,
                    runFrequency       = RunFrequency.MoreRuns,
                }
            });

            Add(new RoleDefinition
            {
                role = PlayerRole.Poacher,
                displayName = "포처",
                shortDescription = "박스 안에서 골만 노리는 골 전문 사냥꾼",
                fullDescription =
                    "골 에어리어 근처를 떠나지 않으며 동료가 만들어준 기회를 마무리하는 역할. " +
                    "연계 플레이보다 위치 선정과 결정력에 모든 것이 달려 있다. " +
                    "화려한 플레이 없이 오로지 박스 안에서 가장 유리한 위치를 선점해 가장 높은 확률의 슈팅을 시도한다.",
                tacticalBehavior =
                    "패스를 요구하거나 빌드업에 참여하기보다 페널티 박스 안에 위치한다. " +
                    "동료의 크로스나 패스를 받아 원터치로 마무리하는 것이 주 임무다. " +
                    "수비수 등 뒤에 위치하다 마지막 순간에 나타나는 '그림자 공격수'처럼 기능한다.",
                strengths =
                    "박스 내 위치 선정과 원터치 마무리로 적은 기회에서도 높은 골 전환율을 보인다. " +
                    "전술적 부담이 없어 팀이 만들어주는 기회를 효율적으로 득점으로 연결한다.",
                weaknesses =
                    "빌드업이나 연계 플레이에 기여하지 않아 팀 포제션 시 숫자 불균형이 생긴다. " +
                    "크로스나 스루패스를 만들어줄 선수 없이는 완전히 소외될 수 있다.",
                bestIn =
                    "크로스와 스루패스를 꾸준히 공급할 수 있는 전통 윙어나 어드밴스드 플레이메이커가 있는 시스템에 최적이다. " +
                    "4-4-2에서 공격적인 파트너 공격수와 함께 배치해 연계와 마무리 역할을 분담하면 효과적이다.",
                applicablePositions = Positions.Forwards,
                allowedDuties = new List<RoleDuty> { RoleDuty.Attack },
                keyTechnicalAttributes = new List<TechnicalAttribute> { TechnicalAttribute.Finishing, TechnicalAttribute.Heading, TechnicalAttribute.FirstTouch },
                keyPhysicalAttributes  = new List<PhysicalAttribute> { PhysicalAttribute.Agility, PhysicalAttribute.Balance },
                keyMentalAttributes    = new List<MentalAttribute> { MentalAttribute.Composure, MentalAttribute.Decisions, MentalAttribute.Concentration },
                attackInstructions = new PlayerInstructions
                {
                    offTheBallRuns     = OffTheBallRuns.MakeMoreRuns,
                    shootingFrequency  = ShootingFrequency.ShootOften,
                    pressingIntensity  = PressingIntensity.PressLess,
                    verticalMovement   = VerticalMovement.StayBackMore,
                    horizontalMovement = HorizontalMovement.CutInside,
                    passingStyle       = PassingStyle.Safe,
                }
            });

            Add(new RoleDefinition
            {
                role = PlayerRole.TargetMan,
                displayName = "타깃맨",
                shortDescription = "공중볼과 몸싸움으로 팀의 공격 기점이 되는 공격수",
                fullDescription =
                    "강한 신체와 헤딩으로 롱볼을 받아 동료에게 연결하거나 직접 슈팅을 시도하는 역할. " +
                    "팀이 롱볼 축구나 크로스 전술을 쓸 때 이상적이다. " +
                    "단순히 헤딩 골을 노리는 것이 아니라 롱볼을 가슴이나 발로 받아 연결해 동료들이 달려올 공간을 창출하는 공격의 기점이 된다.",
                tacticalBehavior =
                    "수비수와 몸싸움을 마다하지 않고 높이 솟아 헤딩 또는 가슴 트래핑으로 볼을 잡는다. " +
                    "동료의 오버래핑 런을 유도하며 연계 플레이의 중심이 된다. " +
                    "Support 듀티에서 더 자주 내려와 연결 역할을 하며, Attack에서는 직접 헤딩 득점에 집중한다.",
                strengths =
                    "롱볼, 크로스, 세트피스에서 압도적인 공중볼 경합 능력으로 팀에 명확한 공격 방향성을 제공한다. " +
                    "몸싸움으로 볼을 지키며 동료들의 2차 공격 기회를 만들어낸다.",
                weaknesses =
                    "속도가 느려 빠른 역습이나 수비 라인 뒤 침투가 어렵다. " +
                    "패스 중심 포제션 전술에서는 공간 점유 외 기여가 제한된다.",
                bestIn =
                    "전통 윙어의 크로스와 결합하는 4-4-2나 4-2-3-1에서 최고의 효율을 낸다. " +
                    "GK나 CB의 롱킥을 직접 받아 연계할 수 있어 카운터어택 전술에서도 유효한 무기가 된다.",
                applicablePositions = Positions.Forwards,
                allowedDuties = new List<RoleDuty> { RoleDuty.Support, RoleDuty.Attack },
                keyTechnicalAttributes = new List<TechnicalAttribute> { TechnicalAttribute.Heading, TechnicalAttribute.FirstTouch, TechnicalAttribute.Finishing },
                keyPhysicalAttributes  = new List<PhysicalAttribute> { PhysicalAttribute.Strength, PhysicalAttribute.Jumping },
                keyMentalAttributes    = new List<MentalAttribute> { MentalAttribute.Courage, MentalAttribute.Teamwork, MentalAttribute.Decisions },
                supportInstructions = new PlayerInstructions
                {
                    verticalMovement  = VerticalMovement.StayBackMore,
                    dribblingTendency = DribblingTendency.DribbleLess,
                },
                attackInstructions = new PlayerInstructions
                {
                    shootingFrequency = ShootingFrequency.ShootOften,
                    offTheBallRuns    = OffTheBallRuns.MakeMoreRuns,
                }
            });

            Add(new RoleDefinition
            {
                role = PlayerRole.FalseNine,
                displayName = "폴스 나인",
                shortDescription = "중원으로 내려와 공간을 만들고 플레이를 조율하는 공격수",
                fullDescription =
                    "전통적 9번 위치를 벗어나 미드필드 지역으로 내려와 볼을 받고 배급하는 역할. " +
                    "이 공격수가 내려오면 뒤따라오는 수비의 공간을 윙어나 미드필더가 활용한다. " +
                    "상대 CB에게 딜레마를 안겨주는 역할로, 따라가면 수비 라인이 무너지고 무시하면 볼을 자유롭게 받아 공격을 조율한다.",
                tacticalBehavior =
                    "상대 CB를 끌어내리며 볼을 받아 창의적인 패스로 팀의 공격을 조율한다. " +
                    "슈팅보다 패스와 드리블 전진을 선호한다. " +
                    "내려오는 동작 자체가 상대 수비 구조를 흔드는 전술적 무기이므로 타이밍과 포지셔닝이 매우 중요하다.",
                strengths =
                    "상대 수비에 혼란을 주며 공격수 위치에서 미드필더처럼 플레이해 전술적 예측 불가능성을 높인다. " +
                    "윙어나 메짤라가 내려온 공격수의 자리로 침투해 대형 골 기회를 만들어낸다.",
                weaknesses =
                    "전통적인 박스 안 위협이 사라져 단순 수비적인 팀에게는 효과가 낮다. " +
                    "폴스 나인의 움직임을 이해하고 반응할 수 있는 영리한 윙어나 미드필더가 없으면 효과가 반감된다.",
                bestIn =
                    "양쪽 인사이드 포워드 또는 메짤라와 함께 3인 공격 구조를 형성하는 4-3-3에서 최고 효율을 발휘한다. " +
                    "포제션을 중시하고 빠른 패스 교환으로 공간을 만드는 티키타카 스타일 전술에 특히 적합하다.",
                applicablePositions = Positions.Forwards,
                allowedDuties = new List<RoleDuty> { RoleDuty.Support },
                keyTechnicalAttributes = new List<TechnicalAttribute> { TechnicalAttribute.Passing, TechnicalAttribute.Dribbling, TechnicalAttribute.FirstTouch, TechnicalAttribute.Finishing },
                keyPhysicalAttributes  = new List<PhysicalAttribute> { PhysicalAttribute.Agility, PhysicalAttribute.Balance },
                keyMentalAttributes    = new List<MentalAttribute> { MentalAttribute.Vision, MentalAttribute.Decisions, MentalAttribute.Composure },
                supportInstructions = new PlayerInstructions
                {
                    verticalMovement  = VerticalMovement.StayBackMore,
                    passingStyle      = PassingStyle.Risky,
                    dribblingTendency = DribblingTendency.DribbleMore,
                }
            });

            Add(new RoleDefinition
            {
                role = PlayerRole.PressingForward,
                displayName = "압박형 포워드",
                shortDescription = "전방에서 상대 빌드업을 방해하는 압박 전문 공격수",
                fullDescription =
                    "득점보다 압박을 통한 볼 탈취에 특화된 공격수. 상대 GK와 CB의 빌드업을 " +
                    "방해하고, 볼 탈취 후 2선 선수들에게 연결해 즉각적인 공격 기회를 만든다. " +
                    "팀의 전체 압박 전술의 시발점이 되는 역할로, 이 선수의 첫 번째 압박이 팀 전체의 게겐프레스 신호가 된다.",
                tacticalBehavior =
                    "상대 볼 소유 시 최전방에서 강도 높은 압박을 가한다. " +
                    "체력이 핵심이며 영리한 압박 각도로 상대의 패스 루트를 차단한다. " +
                    "압박 방향을 유도해 상대가 원하는 쪽으로 볼을 내줄 수 없도록 패스 선택지를 제한한다.",
                strengths =
                    "전방에서 볼을 탈취하거나 실수를 유도해 위험한 위치에서 즉각적인 득점 기회를 만든다. " +
                    "팀 전체의 압박 강도를 높여 상대가 후방에서 빌드업하기 어렵게 만든다.",
                weaknesses =
                    "체력 소모가 극심해 고강도 압박을 유지하기 위한 체력 관리가 필수다. " +
                    "압박이 실패하면 전방에 선수가 고립되어 팀이 역습에 노출될 수 있다.",
                bestIn =
                    "게겐프레스나 하이프레스 전술을 구사하는 팀에서 공격의 선봉이자 수비의 시작점으로 이상적이다. " +
                    "팀 전체가 함께 압박하는 구조(4-3-3 하이프레스 등)에서 이 선수의 첫 압박이 연쇄 반응을 일으킨다.",
                applicablePositions = Positions.Forwards,
                allowedDuties = new List<RoleDuty> { RoleDuty.Defend, RoleDuty.Support, RoleDuty.Attack },
                keyTechnicalAttributes = new List<TechnicalAttribute> { TechnicalAttribute.Finishing, TechnicalAttribute.FirstTouch, TechnicalAttribute.Tackling },
                keyPhysicalAttributes  = new List<PhysicalAttribute> { PhysicalAttribute.Speed, PhysicalAttribute.Stamina, PhysicalAttribute.Agility },
                keyMentalAttributes    = new List<MentalAttribute> { MentalAttribute.WorkRate, MentalAttribute.Aggression, MentalAttribute.Decisions },
                defendInstructions = new PlayerInstructions
                {
                    pressingIntensity = PressingIntensity.PressMore,
                    closingDownStyle  = ClosingDownStyle.CloseDownMore,
                    runFrequency      = RunFrequency.MoreRuns,
                },
                supportInstructions = new PlayerInstructions
                {
                    pressingIntensity = PressingIntensity.PressMore,
                    closingDownStyle  = ClosingDownStyle.CloseDownMore,
                    runFrequency      = RunFrequency.MoreRuns,
                },
                attackInstructions = new PlayerInstructions
                {
                    pressingIntensity = PressingIntensity.PressMore,
                    closingDownStyle  = ClosingDownStyle.CloseDownMore,
                    shootingFrequency = ShootingFrequency.ShootOften,
                    runFrequency      = RunFrequency.MoreRuns,
                    offTheBallRuns    = OffTheBallRuns.MakeMoreRuns,
                }
            });

            Add(new RoleDefinition
            {
                role = PlayerRole.CompleteForward,
                displayName = "완성형 포워드",
                shortDescription = "골, 어시스트, 연계, 압박 모든 것을 소화하는 완전형 공격수",
                fullDescription =
                    "현대 축구가 요구하는 모든 공격 임무를 수행할 수 있는 이상적인 공격수. " +
                    "헤딩, 발기술, 연계, 압박까지 뛰어난 능력치가 전반적으로 요구된다. " +
                    "팀의 전술이 어떻게 바뀌어도 자신의 역할을 능동적으로 조율하는 높은 축구 지능이 이 역할의 진정한 조건이다.",
                tacticalBehavior =
                    "상황에 따라 타깃맨, 어드밴스드 포워드, 폴스 나인을 넘나드는 유연한 플레이를 보인다. " +
                    "팀의 전술에 맞춰 자신의 역할을 조율하는 축구 지능이 높아야 한다. " +
                    "Attack 듀티에서는 최전방에서 적극적으로 압박하면서도 득점 기회를 놓치지 않는 완전체 공격수로 기능한다.",
                strengths =
                    "다재다능해 어떤 전술과 시스템에도 적응하며 상대 수비에게 예측 불가능한 위협을 지속적으로 제공한다. " +
                    "한 명의 선수가 여러 공격 역할을 커버할 수 있어 팀 전술의 유연성을 크게 높인다.",
                weaknesses =
                    "모든 능력치가 고르게 높아야 하므로 찾기 매우 어렵고 이적료와 연봉이 최상위권에 달한다. " +
                    "한두 가지에 특화된 역할(타깃맨, 포처 등)에 비해 극단적인 상황에서의 전문성이 부족할 수 있다.",
                bestIn =
                    "대부분의 시스템에서 최고의 단독 공격수 옵션이 되며, 특히 4-3-3이나 4-2-3-1의 최전방에서 모든 공격 흐름을 수렴하는 역할로 이상적이다. " +
                    "파트너 공격수보다 단독 스트라이커로 사용할 때 자유도가 가장 높아 능력을 최대로 발휘한다.",
                applicablePositions = Positions.Forwards,
                allowedDuties = new List<RoleDuty> { RoleDuty.Support, RoleDuty.Attack },
                keyTechnicalAttributes = new List<TechnicalAttribute> { TechnicalAttribute.Finishing, TechnicalAttribute.Heading, TechnicalAttribute.Passing, TechnicalAttribute.Dribbling, TechnicalAttribute.FirstTouch },
                keyPhysicalAttributes  = new List<PhysicalAttribute> { PhysicalAttribute.Speed, PhysicalAttribute.Strength, PhysicalAttribute.Stamina },
                keyMentalAttributes    = new List<MentalAttribute> { MentalAttribute.Composure, MentalAttribute.Vision, MentalAttribute.Decisions, MentalAttribute.WorkRate },
                supportInstructions = new PlayerInstructions
                {
                    runFrequency       = RunFrequency.MoreRuns,
                    offTheBallRuns     = OffTheBallRuns.MakeMoreRuns,
                },
                attackInstructions = new PlayerInstructions
                {
                    shootingFrequency  = ShootingFrequency.ShootOften,
                    passingStyle       = PassingStyle.Risky,
                    runFrequency       = RunFrequency.MoreRuns,
                    pressingIntensity  = PressingIntensity.PressMore,
                    offTheBallRuns     = OffTheBallRuns.MakeMoreRuns,
                    dribblingTendency  = DribblingTendency.DribbleMore,
                }
            });

            Add(new RoleDefinition
            {
                role = PlayerRole.DeepLyingForward,
                displayName = "딥 라잉 포워드",
                shortDescription = "내려와서 연결 역할을 하는 공간 창출형 공격수",
                fullDescription =
                    "전방에서 높이 있지 않고 미드필드 방향으로 내려와 볼을 받아 연결하는 공격수. " +
                    "폴스 나인보다 미드필더에 가깝지 않고 스트라이커 위치를 유지하면서도 더 낮게 움직인다. " +
                    "내려오면서 상대 수비를 당겨내 윙어나 미드필더들에게 박스 침투 공간을 만들어주는 팀 플레이어다.",
                tacticalBehavior =
                    "최전방보다 한 발 뒤에서 볼을 받아 동료에게 연결하거나 반전 드리블로 전진한다. " +
                    "파트너 공격수가 있다면 내려오는 딥 무브로 상대 CB를 끌어내 전방 공간을 만든다. " +
                    "Support 듀티에서는 빌드업 연결 역할에 집중하고, Attack에서는 연결 후 박스까지 올라가 슈팅도 시도한다.",
                strengths =
                    "상대 수비 라인과 미드필더 사이 공간에서 볼을 받아 팀의 공격 흐름을 부드럽게 연결한다. " +
                    "내려오는 움직임으로 동측 윙어나 파트너 스트라이커의 침투 공간을 직접 만들어낸다.",
                weaknesses =
                    "최전방 위협이 약해져 상대 수비가 편안하게 높은 라인을 유지할 수 있다. " +
                    "파트너 공격수 없이 단독으로 배치되면 전방에 침투할 선수가 없어 기여가 제한된다.",
                bestIn =
                    "4-4-2나 3-5-2에서 박스 침투형 파트너 공격수(포처, 어드밴스드 포워드)와 조합될 때 최고 효율을 낸다. " +
                    "메짤라나 박스투박스 미드필더가 딥 라잉 포워드의 연결 이후 박스 침투를 보완하는 구조도 효과적이다.",
                applicablePositions = Positions.Forwards,
                allowedDuties = new List<RoleDuty> { RoleDuty.Support, RoleDuty.Attack },
                keyTechnicalAttributes = new List<TechnicalAttribute> { TechnicalAttribute.Passing, TechnicalAttribute.FirstTouch, TechnicalAttribute.Dribbling, TechnicalAttribute.Finishing },
                keyPhysicalAttributes  = new List<PhysicalAttribute> { PhysicalAttribute.Strength, PhysicalAttribute.Agility },
                keyMentalAttributes    = new List<MentalAttribute> { MentalAttribute.Vision, MentalAttribute.Decisions, MentalAttribute.Teamwork },
                supportInstructions = new PlayerInstructions
                {
                    verticalMovement  = VerticalMovement.StayBackMore,
                    runFrequency      = RunFrequency.FewerRuns,
                },
                attackInstructions = new PlayerInstructions
                {
                    dribblingTendency = DribblingTendency.DribbleMore,
                    runFrequency      = RunFrequency.MoreRuns,
                    offTheBallRuns    = OffTheBallRuns.MakeMoreRuns,
                }
            });
        }
    }
}