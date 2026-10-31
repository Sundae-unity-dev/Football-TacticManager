using System.Collections.Generic;

namespace FootballTactician.Data
{
    public static partial class RoleDatabase
    {
        private static void BuildCb()
        {
            Add(new RoleDefinition
            {
                role = PlayerRole.CentralDefender,
                displayName = "중앙 수비수",
                shortDescription = "균형 잡힌 기본형 CB",
                fullDescription =
                    "수비와 빌드업 모두 무난하게 소화하는 표준형 중앙 수비수. " +
                    "적절한 위치 선정과 헤딩, 태클 능력이 요구된다. " +
                    "특정 전술에 의존하지 않고 어떤 시스템에서도 안정적으로 기능하는 믿음직한 역할이다.",
                tacticalBehavior =
                    "수비 라인을 유지하며 중앙 공간을 커버한다. 빌드업 시 안전한 횡패스·백패스를 선호하며, " +
                    "위협적인 상황에서는 클리어런스를 우선시한다.",
                strengths =
                    "어떤 전술 시스템에서도 무난하게 적용할 수 있어 범용성이 높다. " +
                    "수비·빌드업을 모두 기본 이상 수행해 팀의 안정적인 기반을 제공한다.",
                weaknesses =
                    "특화된 역할(스토퍼, 빌드업 수비수 등)에 비해 전술적 기여도가 평범하다. " +
                    "특출난 장점이 없어 고급 전술에서 활용도가 다소 떨어질 수 있다.",
                bestIn =
                    "4-4-2, 4-2-3-1 등 전통적인 4백 시스템의 중앙 수비로 이상적이다. " +
                    "파트너 CB의 역할에 따라 Defend 또는 Support 듀티를 유연하게 선택할 수 있다.",
                applicablePositions = Position.CB,
                allowedDuties = new List<RoleDuty> { RoleDuty.Defend, RoleDuty.Support },
                keyTechnicalAttributes = new List<TechnicalAttribute> { TechnicalAttribute.Tackling, TechnicalAttribute.Heading, TechnicalAttribute.Marking, TechnicalAttribute.Passing },
                keyPhysicalAttributes  = new List<PhysicalAttribute> { PhysicalAttribute.Strength, PhysicalAttribute.Jumping },
                keyMentalAttributes    = new List<MentalAttribute> { MentalAttribute.Positioning, MentalAttribute.Concentration, MentalAttribute.Decisions },
                defendInstructions = new PlayerInstructions
                {
                    passingStyle      = PassingStyle.Safe,
                    verticalMovement  = VerticalMovement.StayBackMore,
                    markingStyle      = MarkingStyle.TightMarking,
                },
            });

            Add(new RoleDefinition
            {
                role = PlayerRole.BallPlayingDefender,
                displayName = "공격형 수비수",
                shortDescription = "패스로 빌드업을 시작하는 CB",
                fullDescription =
                    "수비 능력에 더해 패스 능력이 뛰어나 후방 빌드업의 출발점 역할을 한다. " +
                    "압박을 받는 상황에서도 침착하게 패스를 연결하는 능력이 필요하다. " +
                    "상대의 전방 압박을 회피하면서 볼을 전진시키는 팀에게 필수적인 역할이다.",
                tacticalBehavior =
                    "상대 압박을 인식하면 드리블 전진이나 전방 라인 사이를 뚫는 패스를 시도한다. " +
                    "짧은 패스를 선호하지만 공간이 열리면 적극적으로 롱패스를 구사한다. " +
                    "미드필드와의 삼각 패스 구성으로 압박을 탈출하는 데 능하다.",
                strengths =
                    "후방 빌드업의 퀄리티를 높여 팀 전체의 포제션 축구를 가능하게 한다. " +
                    "상대 전방 압박을 능동적으로 돌파해 빠른 전진 플레이를 만들어낸다.",
                weaknesses =
                    "볼을 잡고 패스를 시도하는 과정에서 실수 시 위험한 위치에서 볼을 잃을 수 있다. " +
                    "단순 수비보다 인지 능력·기술 수준이 높은 선수가 필요하다.",
                bestIn =
                    "포제션 기반 전술, 특히 4-3-3이나 3-4-3처럼 CB가 빌드업에 적극 참여하는 시스템에 이상적이다. " +
                    "딥 라잉 플레이메이커나 스위퍼 키퍼와 함께 사용하면 빌드업 삼각형이 더욱 견고해진다.",
                applicablePositions = Position.CB,
                allowedDuties = new List<RoleDuty> { RoleDuty.Defend, RoleDuty.Support },
                keyTechnicalAttributes = new List<TechnicalAttribute> { TechnicalAttribute.Passing, TechnicalAttribute.LongPassing, TechnicalAttribute.FirstTouch, TechnicalAttribute.Tackling },
                keyPhysicalAttributes  = new List<PhysicalAttribute> { PhysicalAttribute.Strength },
                keyMentalAttributes    = new List<MentalAttribute> { MentalAttribute.Composure, MentalAttribute.Vision, MentalAttribute.Decisions },
                defendInstructions = new PlayerInstructions
                {
                    verticalMovement = VerticalMovement.StayBackMore,
                },
                supportInstructions = new PlayerInstructions
                {
                    passingStyle    = PassingStyle.Risky,
                    dribblingTendency = DribblingTendency.DribbleMore,
                }
            });

            Add(new RoleDefinition
            {
                role = PlayerRole.LimitedDefender,
                displayName = "단순 수비수",
                shortDescription = "수비만 집중하는 직선적 CB",
                fullDescription =
                    "기술적 능력보다 신체 능력과 투쟁심으로 수비에 올인하는 타입. " +
                    "빌드업 기여는 최소화하고 클리어런스와 태클에 집중한다. " +
                    "화려함은 없지만 상대 공격수와의 신체 대결에서 꿋꿋이 버티는 능력으로 팀을 지탱한다.",
                tacticalBehavior =
                    "볼을 잡으면 가장 단순하고 안전한 클리어 또는 짧은 패스를 선택한다. " +
                    "전방으로의 드리블이나 위험한 패스는 거의 시도하지 않는다. " +
                    "상대 공격수에게 헤딩과 몸싸움으로 맞서며 박스 수비에 집중한다.",
                strengths =
                    "수비에만 집중하므로 실수로 인한 위험한 볼 손실이 거의 없다. " +
                    "강한 신체와 헤딩으로 상대의 롱볼·크로스 공격을 효과적으로 차단한다.",
                weaknesses =
                    "빌드업 참여가 없어 팀이 포제션을 원할 때 부담이 될 수 있다. " +
                    "기술적 압박 탈출 능력이 없어 전방 압박 전술을 구사하는 팀에 취약하다.",
                bestIn =
                    "롱볼 전술이나 카운터어택 기반의 직선적 축구에 적합하다. " +
                    "빌드업 수비수나 기술이 뛰어난 파트너 CB와 함께 배치해 단점을 보완하는 것이 이상적이다.",
                applicablePositions = Position.CB,
                allowedDuties = new List<RoleDuty> { RoleDuty.Defend },
                keyTechnicalAttributes = new List<TechnicalAttribute> { TechnicalAttribute.Tackling, TechnicalAttribute.Heading, TechnicalAttribute.Marking },
                keyPhysicalAttributes  = new List<PhysicalAttribute> { PhysicalAttribute.Strength, PhysicalAttribute.Jumping, PhysicalAttribute.Stamina },
                keyMentalAttributes    = new List<MentalAttribute> { MentalAttribute.Courage, MentalAttribute.Concentration, MentalAttribute.Aggression },
                defendInstructions = new PlayerInstructions
                {
                    passingStyle      = PassingStyle.Safe,
                    passingRange      = PassingRange.Short,
                    verticalMovement  = VerticalMovement.StayBackMore,
                    dribblingTendency = DribblingTendency.DribbleLess,
                    markingStyle      = MarkingStyle.TightMarking,
                }
            });

            Add(new RoleDefinition
            {
                role = PlayerRole.StopperCB,
                displayName = "스토퍼",
                shortDescription = "전방으로 나가 상대 공격수를 차단하는 CB",
                fullDescription =
                    "수비 블록에 안주하지 않고 위협적인 상대 공격수에게 직접 다가가 차단하는 적극적 CB. " +
                    "1대1 태클과 압박 능력이 뛰어나야 하며, 뒤 공간 관리가 약점이 될 수 있다. " +
                    "상대 공격수가 볼을 받기 전에 선제적으로 압박해 위협을 차단하는 공격적인 수비 스타일이다.",
                tacticalBehavior =
                    "볼을 가진 상대 공격수에게 신속히 접근해 태클을 시도한다. " +
                    "수비 라인보다 한 발 앞으로 나오는 경향이 있어 커버 CB와 함께 쓰는 게 이상적이다. " +
                    "전진 압박으로 상대의 빌드업 흐름을 끊어 빠른 역습 기회를 만들기도 한다.",
                strengths =
                    "상대 공격수를 수비 라인 깊숙이 끌어들이지 않고 전방에서 미리 차단한다. " +
                    "적극적인 압박으로 상대의 볼 컨트롤과 전진 플레이를 방해한다.",
                weaknesses =
                    "전진 후 파트너 CB가 커버하지 못하면 뒷공간이 위험하게 열린다. " +
                    "커버 수비수가 없는 단독 CB 포메이션에서는 치명적인 약점이 될 수 있다.",
                bestIn =
                    "커버 CB와 짝을 이루는 4백 시스템(4-4-2, 4-2-3-1)에서 최고의 효율을 발휘한다. " +
                    "3백에서 중앙 CB 자리에 배치하면 양쪽 CB가 공간을 커버할 수 있어 효과적이다.",
                applicablePositions = Position.CB,
                allowedDuties = new List<RoleDuty> { RoleDuty.Defend },
                keyTechnicalAttributes = new List<TechnicalAttribute> { TechnicalAttribute.Tackling, TechnicalAttribute.Marking },
                keyPhysicalAttributes  = new List<PhysicalAttribute> { PhysicalAttribute.Strength, PhysicalAttribute.Speed, PhysicalAttribute.Agility },
                keyMentalAttributes    = new List<MentalAttribute> { MentalAttribute.Aggression, MentalAttribute.Courage, MentalAttribute.Decisions },
                defendInstructions = new PlayerInstructions
                {
                    pressingIntensity = PressingIntensity.PressMore,
                    tacklingStyle     = TacklingStyle.TackleHarder,
                    closingDownStyle  = ClosingDownStyle.CloseDownMore,
                    markingStyle      = MarkingStyle.TightMarking,
                }
            });

            Add(new RoleDefinition
            {
                role = PlayerRole.CoverCB,
                displayName = "커버 수비수",
                shortDescription = "뒷공간을 커버하며 라인을 지키는 CB",
                fullDescription =
                    "파트너 CB가 전방으로 나갔을 때 생기는 공간을 채우는 역할. " +
                    "위치 선정과 예측 능력이 뛰어나야 하며, 스토퍼 CB와 짝을 이룰 때 시너지가 극대화된다. " +
                    "항상 라인 뒤에 위치하며 '보험' 역할을 자처해 팀 수비의 견고함을 보장한다.",
                tacticalBehavior =
                    "라인 뒤에서 공간을 읽고 스위칭 무브를 통해 위협을 사전에 차단한다. " +
                    "직접 태클보다 포지셔닝으로 위협을 줄이는 접근법을 취한다. " +
                    "파트너가 압박을 나간 틈에 생기는 대각선 공간을 예측해 이동하는 것이 핵심이다.",
                strengths =
                    "스토퍼 CB 또는 전진하는 리베로의 뒷공간을 효과적으로 메워 팀 구조를 유지한다. " +
                    "침착한 포지셔닝으로 패닉 없이 위기 상황을 정리한다.",
                weaknesses =
                    "항상 뒤에 머무르므로 상대 공격이 전방에서 시작될 때 직접 차단하기 어렵다. " +
                    "단독 스토퍼 없이 커버 CB 혼자 배치되면 수비 전체가 소극적으로 흐를 수 있다.",
                bestIn =
                    "스토퍼+커버 CB 조합을 사용하는 모든 4백 시스템에서 필수 파트너다. " +
                    "3백에서 중앙에 배치하면 양쪽 CB 또는 윙백이 전진해도 안전하게 뒷공간을 관리한다.",
                applicablePositions = Position.CB,
                allowedDuties = new List<RoleDuty> { RoleDuty.Defend },
                keyTechnicalAttributes = new List<TechnicalAttribute> { TechnicalAttribute.Tackling, TechnicalAttribute.Marking },
                keyPhysicalAttributes  = new List<PhysicalAttribute> { PhysicalAttribute.Speed, PhysicalAttribute.Stamina },
                keyMentalAttributes    = new List<MentalAttribute> { MentalAttribute.Positioning, MentalAttribute.Concentration, MentalAttribute.Vision },
                defendInstructions = new PlayerInstructions
                {
                    pressingIntensity = PressingIntensity.PressLess,
                    verticalMovement  = VerticalMovement.StayBackMore,
                    markingStyle      = MarkingStyle.LooseMarking,
                    closingDownStyle  = ClosingDownStyle.CloseDownLess,
                }
            });

            Add(new RoleDefinition
            {
                role = PlayerRole.LiberoDefender,
                displayName = "리베로",
                shortDescription = "공격에 가담하는 자유형 CB",
                fullDescription =
                    "기본적으로 CB이지만 공격 시 미드필드 라인까지 올라와 빌드업에 참여한다. " +
                    "수비 능력과 함께 패스, 드리블, 시야까지 갖춘 올라운드 수비수여야 한다. " +
                    "현대 축구에서 부활한 전술적 개념으로, 수비에서 잉여 인원을 전방으로 전개해 수적 우위를 만든다.",
                tacticalBehavior =
                    "팀이 볼을 점유하면 미드필더처럼 올라와 플레이에 가담한다. " +
                    "수비 전환 시 빠르게 복귀해야 하므로 속도와 체력이 중요하다. " +
                    "중원 과부하를 만들어 상대의 압박 구조를 흐트러뜨리는 것이 핵심 전술적 의도다.",
                strengths =
                    "공격 시 중원 과부하를 만들어 수적 우위를 창출한다. " +
                    "수비 숫자가 충분할 때 미드필더 역할을 겸해 전술적 유연성을 높인다.",
                weaknesses =
                    "전진 후 수비 전환이 늦어지면 뒷공간이 위험에 노출된다. " +
                    "체력 소모가 매우 크며, 고강도 경기에서 후반에 효율이 떨어질 수 있다.",
                bestIn =
                    "3백 시스템(3-4-3, 3-5-2)에서 중앙 CB가 리베로 역할을 할 때 최대 효율을 발휘한다. " +
                    "2명의 CB 또는 커버 수비수가 남아 뒷공간을 책임질 수 있는 구조가 필수다.",
                applicablePositions = Position.CB,
                allowedDuties = new List<RoleDuty> { RoleDuty.Support, RoleDuty.Attack },
                keyTechnicalAttributes = new List<TechnicalAttribute> { TechnicalAttribute.Passing, TechnicalAttribute.Dribbling, TechnicalAttribute.FirstTouch },
                keyPhysicalAttributes  = new List<PhysicalAttribute> { PhysicalAttribute.Speed, PhysicalAttribute.Stamina },
                keyMentalAttributes    = new List<MentalAttribute> { MentalAttribute.Vision, MentalAttribute.Decisions, MentalAttribute.Composure },
                supportInstructions = new PlayerInstructions
                {
                    verticalMovement  = VerticalMovement.GetFurtherForward,
                    runFrequency      = RunFrequency.MoreRuns,
                },
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
