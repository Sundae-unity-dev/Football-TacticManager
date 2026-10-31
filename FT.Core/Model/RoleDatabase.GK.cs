using System.Collections.Generic;

namespace FootballTactician.Data
{
    public static partial class RoleDatabase
    {
        private static void BuildGk()
        {
            Add(new RoleDefinition
            {
                role = PlayerRole.Goalkeeper,
                displayName = "골키퍼",
                shortDescription = "골문을 지키는 기본 수문장",
                fullDescription =
                    "팀의 마지막 수비 라인을 책임지는 포지션. 슈팅 차단과 크로스볼 처리에 집중하며, " +
                    "빌드업 기여보다는 안정적인 위치 선정과 반사 신경이 핵심이다. " +
                    "박스 안에 머물며 예측 가능하고 신뢰할 수 있는 골문 수비를 제공한다.",
                tacticalBehavior =
                    "골문 앞을 지키며 적극적인 박스 이탈을 자제한다. 배급은 짧은 롤링 또는 롱킥으로 " +
                    "상황에 맞게 선택한다. 상대 크로스 상황에서 과감히 박스를 장악하는 능력이 요구된다.",
                strengths =
                    "어떤 전술 시스템에서도 안정적으로 기능하며 수비 라인에 신뢰를 부여한다. " +
                    "박스 이탈을 자제하므로 실수로 인한 위험 노출이 최소화된다.",
                weaknesses =
                    "빌드업 참여가 제한되어 상대의 높은 압박에 대응하는 빌드업 옵션이 부족하다. " +
                    "수비 라인 뒤 공간이 생겼을 때 적극적으로 차단하지 못한다.",
                bestIn =
                    "로우블록 또는 미드블록 수비 전술에서 가장 효과적이다. " +
                    "빌드업보다 조직적 수비를 우선시하는 모든 시스템에 적합하다.",
                applicablePositions = Position.GK,
                allowedDuties = new List<RoleDuty> { RoleDuty.Defend },
                keyGkAttributes = new List<GkAttribute> { GkAttribute.Handling, GkAttribute.Reflexes, GkAttribute.Kicking },
                keyPhysicalAttributes  = new List<PhysicalAttribute> { PhysicalAttribute.Agility },
                keyMentalAttributes    = new List<MentalAttribute> { MentalAttribute.Concentration, MentalAttribute.Positioning },
                defendInstructions = new PlayerInstructions
                {
                    pressingIntensity  = PressingIntensity.PressLess,
                    verticalMovement   = VerticalMovement.StayBackMore,
                }
            });

            Add(new RoleDefinition
            {
                role = PlayerRole.SweeperKeeper,
                displayName = "스위퍼 키퍼",
                shortDescription = "라인 뒤 공간을 스스로 지우는 공격적 GK",
                fullDescription =
                    "수비 라인이 높을 때 뒷공간을 직접 커버하기 위해 박스 밖으로 과감히 나오는 GK. " +
                    "발 기술이 뛰어나 빌드업의 시작점 역할도 한다. 반응 속도와 판단력이 매우 중요하다. " +
                    "하이프레스 전술의 완성을 위해 반드시 필요한 역할로, 뒷공간을 커버하지 않으면 " +
                    "전술 자체가 무너질 위험이 있다.",
                tacticalBehavior =
                    "수비진과 함께 높은 라인을 유지하며 뒷공간 침투에 적극 대응한다. " +
                    "짧은 패스로 빌드업을 시작하며, 상황에 따라 드리블 전진도 시도한다. " +
                    "박스 외부에서 루스볼이나 스루패스를 먼저 처리해 상대 공격을 차단한다.",
                strengths =
                    "하이라인 수비를 가능하게 해 팀 전체의 압박 효율을 높인다. " +
                    "발 기술로 빌드업에 직접 참여해 수적 우위를 만들 수 있다.",
                weaknesses =
                    "박스 밖으로 나갔다가 판단 실수 시 빈 골문이 노출되는 치명적 위험이 있다. " +
                    "뛰어난 발 기술, 예측 능력, 위치 선정을 동시에 갖춘 선수가 필요해 육성 난이도가 높다.",
                bestIn =
                    "하이프레스·게겐프레스 전술, 4-3-3이나 3-4-3처럼 하이라인을 기반으로 하는 시스템에 필수적이다. " +
                    "낮은 블록을 유지하는 전술에서는 능력을 충분히 발휘하기 어렵다.",
                applicablePositions = Position.GK,
                allowedDuties = new List<RoleDuty> { RoleDuty.Support, RoleDuty.Attack },
                keyTechnicalAttributes = new List<TechnicalAttribute> { TechnicalAttribute.FirstTouch, TechnicalAttribute.Passing },
                keyGkAttributes = new List<GkAttribute> { GkAttribute.Handling, GkAttribute.Kicking },
                keyPhysicalAttributes  = new List<PhysicalAttribute> { PhysicalAttribute.Speed, PhysicalAttribute.Agility },
                keyMentalAttributes    = new List<MentalAttribute> { MentalAttribute.Decisions, MentalAttribute.Concentration, MentalAttribute.Positioning },
                supportInstructions = new PlayerInstructions
                {
                    pressingIntensity  = PressingIntensity.PressMore,
                    verticalMovement   = VerticalMovement.GetFurtherForward,
                    passingRange       = PassingRange.Short,
                },
                attackInstructions = new PlayerInstructions
                {
                    pressingIntensity  = PressingIntensity.PressMore,
                    verticalMovement   = VerticalMovement.GetFurtherForward,
                    passingRange       = PassingRange.Short,
                    closingDownStyle   = ClosingDownStyle.CloseDownMore,
                }
            });
        }
    }
}
