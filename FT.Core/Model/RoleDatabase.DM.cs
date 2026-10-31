using System.Collections.Generic;

namespace FootballTactician.Data
{
    public static partial class RoleDatabase
    {
        private static void BuildDm()
        {
            Add(new RoleDefinition
            {
                role = PlayerRole.Anchor,
                displayName = "앵커맨",
                shortDescription = "포지션을 고수하며 중원 수비 구조를 지키는 닻",
                fullDescription =
                    "CDM 중 가장 포지셔닝에 집중하는 역할. 위치를 지키며 공격 차단과 볼 탈취를 최우선으로 하고, " +
                    "공격 가담은 극도로 최소화해 팀의 구조적 균형을 유지한다. " +
                    "볼 탈취 미드필더가 적극적으로 압박하는 것과 달리 앵커맨은 포지션으로 위협을 차단하며, " +
                    "파트너 미드필더들이 자유롭게 전진할 수 있도록 뒤를 받쳐주는 '닻' 역할에 특화되어 있다.",
                tacticalBehavior =
                    "두 CB 앞에 고정되어 중앙 공간을 막는다. 볼 탈취 후 단순한 패스로 빠르게 " +
                    "볼을 배급하며 전방으로의 모험적 플레이는 피한다. " +
                    "파트너 미드필더가 전진한 공간을 채우며 중원의 중심을 잡는다.",
                strengths =
                    "중원 중앙을 고정적으로 지켜 박스투박스 미드필더나 메짤라의 전진을 안전하게 지원한다. " +
                    "볼 탈취 후 빠른 배급으로 역습 전환 속도를 높인다.",
                weaknesses =
                    "공격 기여가 거의 없어 상대가 앵커맨을 무시하고 수비 라인으로 압박할 수 있다. " +
                    "창의성이 부족해 팀의 빌드업이 단조로워질 수 있다.",
                bestIn =
                    "공격적인 미드필더(박스투박스, 메짤라 등) 1~2명을 뒤에서 받쳐주는 4-2-3-1이나 4-3-3에 이상적이다. " +
                    "딥 라잉 플레이메이커와 달리 빌드업보다 수비 안전망이 우선인 전술에 적합하다.",
                applicablePositions = Position.CDM,
                allowedDuties = new List<RoleDuty> { RoleDuty.Defend },
                keyTechnicalAttributes = new List<TechnicalAttribute> { TechnicalAttribute.Tackling, TechnicalAttribute.Marking, TechnicalAttribute.Passing },
                keyPhysicalAttributes  = new List<PhysicalAttribute> { PhysicalAttribute.Strength, PhysicalAttribute.Stamina },
                keyMentalAttributes    = new List<MentalAttribute> { MentalAttribute.Positioning, MentalAttribute.Concentration, MentalAttribute.Teamwork },
                defendInstructions = new PlayerInstructions
                {
                    verticalMovement   = VerticalMovement.StayBackMore,
                    runFrequency       = RunFrequency.FewerRuns,
                    passingStyle       = PassingStyle.Safe,
                }
            });

            Add(new RoleDefinition
            {
                role = PlayerRole.DeepLyingPlaymaker,
                displayName = "딥 라잉 플레이메이커",
                shortDescription = "후방에서 정확한 배급으로 팀 공격을 설계하는 레지스타",
                fullDescription =
                    "수비형 미드필더 위치에서 팀의 빌드업 전체를 지휘하는 역할. 이탈리아 '레지스타(regista)'와 동일한 개념으로, " +
                    "높은 패스 능력과 시야로 전방 선수들에게 최적의 볼을 공급한다. " +
                    "상대 전방 압박을 받아도 침착하게 볼을 처리하는 능력이 필수이며, " +
                    "팀의 모든 공격이 이 선수를 경유하도록 설계된 전술의 핵심 엔진이다.",
                tacticalBehavior =
                    "볼을 받아 전방으로 연결하는 게임 메이킹을 담당한다. " +
                    "팀이 공격할 때 뒤에서 지원하며 수비 전환 시 포지션을 유지해 안전망 역할을 한다. " +
                    "Support 듀티에서는 보다 적극적으로 롱패스와 체인지오브플레이를 시도한다.",
                strengths =
                    "후방에서 정확하고 빠른 배급으로 팀의 공격 리듬을 만들고 포제션을 유지한다. " +
                    "볼 탈취 후 즉각적인 배급으로 역습 전환 속도를 높인다.",
                weaknesses =
                    "볼 탈취 능력이 약해 상대의 전방 압박에 자주 타깃이 된다. " +
                    "이 선수가 봉쇄되면 팀 전체의 빌드업이 막히는 '단일 실패 지점'이 될 수 있다.",
                bestIn =
                    "포제션 기반 전술, 특히 4-1-4-1이나 4-3-3에서 최고의 효율을 발휘한다. " +
                    "볼 탈취 미드필더나 앵커맨이 옆에서 수비를 커버할 때 더 자유롭게 빌드업에 집중할 수 있다.",
                applicablePositions = Position.CDM | Position.CM,
                allowedDuties = new List<RoleDuty> { RoleDuty.Defend, RoleDuty.Support },
                keyTechnicalAttributes = new List<TechnicalAttribute> { TechnicalAttribute.Passing, TechnicalAttribute.LongPassing, TechnicalAttribute.FirstTouch },
                keyPhysicalAttributes  = new List<PhysicalAttribute> { PhysicalAttribute.Stamina },
                keyMentalAttributes    = new List<MentalAttribute> { MentalAttribute.Vision, MentalAttribute.Composure, MentalAttribute.Decisions },
                defendInstructions = new PlayerInstructions
                {
                    verticalMovement  = VerticalMovement.StayBackMore,
                    runFrequency      = RunFrequency.FewerRuns,
                },
                supportInstructions = new PlayerInstructions
                {
                    passingStyle      = PassingStyle.Risky,
                    passingRange      = PassingRange.Long,
                }
            });

            Add(new RoleDefinition
            {
                role = PlayerRole.BallWinningMidfielder,
                displayName = "볼 탈취 미드필더",
                shortDescription = "압박과 태클로 볼을 빼앗는 전투형 미드필더",
                fullDescription =
                    "상대의 볼을 빼앗는 데 특화된 미드필더. 강인한 태클, 높은 압박 강도, " +
                    "적극적인 클로징으로 상대의 빌드업을 방해하고 볼을 회수한다. " +
                    "팀의 수비 블록 앞에서 거름망 역할을 하며 상대의 전진 패스를 차단하는 '더티 워크'의 전문가다.",
                tacticalBehavior =
                    "볼을 가진 상대에게 신속히 접근해 태클을 시도한다. " +
                    "볼 탈취 후 창의적인 플레이보다 안전한 패스로 팀에 볼을 전달한다. " +
                    "상대 미드필더의 수신 직전 압박으로 패스 루트를 차단하는 예측 압박도 특기다.",
                strengths =
                    "공격적 미드필더들이 전진해 생기는 빈 공간을 수비적으로 커버한다. " +
                    "상대 빌드업을 중원에서 끊어 빠른 역습으로 연결하는 효율이 높다.",
                weaknesses =
                    "창의성이 부족해 볼 탈취 후 팀의 공격 흐름이 단조로워질 수 있다. " +
                    "공격 가담이 제한되어 상대가 중원을 수비적으로 내려앉으면 압박 효율이 감소한다.",
                bestIn =
                    "공격적 팀에서 균형을 잡아주는 역할로, 딥 라잉 플레이메이커와 짝을 이루는 이중 CDM 시스템에 이상적이다. " +
                    "게겐프레스나 하이프레스 전술에서 중원 압박의 핵심 엔진 역할을 한다.",
                applicablePositions = Position.CDM | Position.CM,
                allowedDuties = new List<RoleDuty> { RoleDuty.Defend, RoleDuty.Support },
                keyTechnicalAttributes = new List<TechnicalAttribute> { TechnicalAttribute.Tackling, TechnicalAttribute.Marking },
                keyPhysicalAttributes  = new List<PhysicalAttribute> { PhysicalAttribute.Strength, PhysicalAttribute.Stamina, PhysicalAttribute.Agility },
                keyMentalAttributes    = new List<MentalAttribute> { MentalAttribute.Aggression, MentalAttribute.WorkRate, MentalAttribute.Concentration },
                defendInstructions = new PlayerInstructions
                {
                    pressingIntensity = PressingIntensity.PressMore,
                    tacklingStyle     = TacklingStyle.TackleHarder,
                    closingDownStyle  = ClosingDownStyle.CloseDownMore,
                    passingStyle      = PassingStyle.Safe,
                },
                supportInstructions = new PlayerInstructions
                {
                    pressingIntensity = PressingIntensity.PressMore,
                    tacklingStyle     = TacklingStyle.TackleHarder,
                    closingDownStyle  = ClosingDownStyle.CloseDownMore,
                }
            });

            Add(new RoleDefinition
            {
                role = PlayerRole.HalfBack,
                displayName = "하프백",
                shortDescription = "볼 손실 시 CB 사이로 내려앉아 동적 3백을 만드는 CDM",
                fullDescription =
                    "수비 전환 시 두 CB 사이로 내려가 4백을 즉각 3백으로 변환하는 전술적 CDM. " +
                    "이로써 양쪽 풀백이 더 공격적으로 올라갈 수 있는 공간과 심리적 여유를 만들어준다. " +
                    "공격 시에는 일반 CDM처럼 중원에 위치하지만, 볼을 잃는 순간 즉각 CB 사이로 하강하는 판단력이 이 역할의 핵심이다.",
                tacticalBehavior =
                    "팀이 볼을 잃으면 CB 라인으로 내려가 3~5백 구조를 형성해 상대 역습을 수적 우위로 막는다. " +
                    "공격 시에는 중원에 위치해 짧고 안전한 패스로 빌드업을 지원한다. " +
                    "풀백이 올라가 생기는 측면 수비 공백을 사전에 예측하고 하강 타이밍을 맞추는 것이 중요하다.",
                strengths =
                    "4백 시스템에서 실질적으로 3백처럼 기능해 공격적 풀백의 뒷공간을 자동으로 커버한다. " +
                    "수비 수적 우위를 만들어 상대의 빠른 역습에 대비하는 구조적 안전망이 된다.",
                weaknesses =
                    "CB 라인으로 내려가는 동안 중원이 비어 상대 미드필더가 자유롭게 볼을 받을 수 있다. " +
                    "내려가는 타이밍이 맞지 않으면 중원과 수비 라인 사이 공간이 오히려 위험해질 수 있다.",
                bestIn =
                    "공격적 풀백(Attack 듀티)을 운용하는 4-1-4-1이나 4-2-3-1에서 풀백의 전진을 안전하게 뒷받침한다. " +
                    "3백 시스템에서도 중앙 CB 보호와 수비 숫자 확보에 활용할 수 있다.",
                applicablePositions = Position.CDM,
                allowedDuties = new List<RoleDuty> { RoleDuty.Defend },
                keyTechnicalAttributes = new List<TechnicalAttribute> { TechnicalAttribute.Tackling, TechnicalAttribute.Passing, TechnicalAttribute.Marking },
                keyPhysicalAttributes  = new List<PhysicalAttribute> { PhysicalAttribute.Strength, PhysicalAttribute.Stamina },
                keyMentalAttributes    = new List<MentalAttribute> { MentalAttribute.Positioning, MentalAttribute.Teamwork, MentalAttribute.Decisions },
                defendInstructions = new PlayerInstructions
                {
                    verticalMovement  = VerticalMovement.StayBackMore,
                    pressingIntensity = PressingIntensity.PressLess,
                    passingStyle      = PassingStyle.Safe,
                }
            });

            Add(new RoleDefinition
            {
                role = PlayerRole.DefensiveMidfielder,
                displayName = "수비형 미드필더",
                shortDescription = "압박과 위치 이동을 병행하는 균형형 CDM",
                fullDescription =
                    "수비 블록을 지키면서도 필요 시 압박을 나가고 전방으로 패스를 연결하는 기본형 CDM. " +
                    "앵커맨처럼 완전히 고정되지 않고, 상황에 따라 이동하며 볼을 회수하거나 배급한다. " +
                    "볼 탈취 미드필더보다 덜 공격적이고 딥 라잉 플레이메이커보다 덜 창의적이지만, " +
                    "두 역할의 균형을 잡는 가장 범용적인 CDM으로 어떤 시스템에도 안정적으로 기능한다.",
                tacticalBehavior =
                    "수비 라인 앞을 커버하며 상대 공격의 첫 번째 차단 지점이 된다. " +
                    "볼을 잡으면 안전한 패스를 우선시하지만 공간이 열리면 전방으로 적극 배급한다. " +
                    "Support 듀티에서는 볼 소유 시 더 적극적으로 공격 전개에 참여하며 포지셔닝이 유연해진다.",
                strengths =
                    "수비와 빌드업을 모두 무난하게 소화해 어떤 시스템에도 적용할 수 있는 범용성이 높다. " +
                    "앵커맨보다 유동적이어서 상황 변화에 능동적으로 대응하며 중원 균형을 유지한다.",
                weaknesses =
                    "특화된 역할(앵커맨, 볼 탈취 미드필더, 딥 라잉 플레이메이커)에 비해 각 항목의 전문성이 낮다. " +
                    "극단적인 전술(하이프레스 또는 울트라 딥블록)에서는 더 특화된 역할이 효율적이다.",
                bestIn =
                    "4-4-2, 4-2-3-1 등 전통적인 시스템에서 두 CDM 중 하나 또는 단독 CDM으로 안정적으로 기능한다. " +
                    "더 공격적이거나 수비적인 파트너 CDM과 조합해 균형을 맞추는 방식으로 활용한다.",
                applicablePositions = Position.CDM,
                allowedDuties = new List<RoleDuty> { RoleDuty.Defend, RoleDuty.Support },
                keyTechnicalAttributes = new List<TechnicalAttribute> { TechnicalAttribute.Tackling, TechnicalAttribute.Passing, TechnicalAttribute.Marking },
                keyPhysicalAttributes  = new List<PhysicalAttribute> { PhysicalAttribute.Strength, PhysicalAttribute.Stamina },
                keyMentalAttributes    = new List<MentalAttribute> { MentalAttribute.Positioning, MentalAttribute.Concentration, MentalAttribute.Decisions },
                defendInstructions = new PlayerInstructions
                {
                    verticalMovement  = VerticalMovement.StayBackMore,
                    passingStyle      = PassingStyle.Safe,
                    runFrequency      = RunFrequency.FewerRuns,
                },
            });

            Add(new RoleDefinition
            {
                role = PlayerRole.SegundoVolante,
                displayName = "세군도 볼란테",
                shortDescription = "CDM 위치에서 박스까지 침투해 득점과 어시스트를 노리는 공격적 수비형 미드필더",
                fullDescription =
                    "스페인어로 '두 번째 수비형 미드필더'를 뜻하는 역할. 수비형 미드필더 포지션에 배치되지만 " +
                    "상대 박스까지 적극적으로 침투해 득점과 어시스트를 직접 노리는 공격적인 CDM이다. " +
                    "앵커맨이 뒤를 지키는 동안 세군도 볼란테는 파트너 CM·CAM처럼 전진하며, " +
                    "메짤라보다 더 깊은 출발점에서 더 멀리 침투한다는 점이 차이다.",
                tacticalBehavior =
                    "팀이 볼을 소유하면 CDM 위치에서 전방으로 치고 올라가 박스 근처에서 슈팅·어시스트를 노린다. " +
                    "볼을 잃으면 신속히 CDM 라인으로 복귀해 수비 균형을 잡는다. " +
                    "파트너로 앵커맨이나 딥 라잉 플레이메이커가 있어야 전진 시 뒷공간이 안전하게 커버된다.",
                strengths =
                    "중원에서 출발해 박스까지 침투하므로 상대 수비가 예측하기 어려운 3번째 공격 옵션이 된다. " +
                    "수비와 공격 모두 기여할 수 있어 팀의 유연성을 높이고 미드필드 수적 우위를 만든다.",
                weaknesses =
                    "전진 시 뒷공간이 열리므로 파트너 CDM의 커버가 필수적이다. " +
                    "수비 복귀가 늦어지면 중원이 뚫려 상대의 빠른 역습에 취약해진다.",
                bestIn =
                    "앵커맨 또는 딥 라잉 플레이메이커와 이중 CDM을 구성하는 4-2-3-1이나 4-1-4-1에서 최적이다. " +
                    "박스투박스 미드필더보다 더 낮은 출발점이 필요한 팀에서 세군도 볼란테가 그 역할을 대신한다.",
                applicablePositions = Position.CDM | Position.CM,
                allowedDuties = new List<RoleDuty> { RoleDuty.Support, RoleDuty.Attack },
                keyTechnicalAttributes = new List<TechnicalAttribute> { TechnicalAttribute.Shooting, TechnicalAttribute.Passing, TechnicalAttribute.Dribbling, TechnicalAttribute.FirstTouch },
                keyPhysicalAttributes  = new List<PhysicalAttribute> { PhysicalAttribute.Stamina, PhysicalAttribute.Speed, PhysicalAttribute.Agility },
                keyMentalAttributes    = new List<MentalAttribute> { MentalAttribute.Decisions, MentalAttribute.OffTheBall, MentalAttribute.WorkRate },
                supportInstructions = new PlayerInstructions
                {
                    runFrequency      = RunFrequency.MoreRuns,
                    offTheBallRuns    = OffTheBallRuns.MakeMoreRuns,
                    verticalMovement  = VerticalMovement.GetFurtherForward,
                },
                attackInstructions = new PlayerInstructions
                {
                    runFrequency      = RunFrequency.MoreRuns,
                    offTheBallRuns    = OffTheBallRuns.MakeMoreRuns,
                    verticalMovement  = VerticalMovement.GetFurtherForward,
                    shootingFrequency = ShootingFrequency.ShootOften,
                    longShotTendency  = LongShotTendency.ShootFromDistance,
                    dribblingTendency = DribblingTendency.DribbleMore,
                }
            });
        }
    }
}
