using System;
using System.Collections.Generic;
using FootballTactician.Balance;
using FootballTactician.Data;

namespace FootballTactician.Simulation
{
    /// <summary>
    /// 역할 적합도. 그 선수의 능력치가 역할이 요구하는 것과 얼마나 맞는지.
    ///
    /// 핵심은 <b>전체 능력이 아니라 치우침</b>을 본다는 것이다. 능력치 총합이 같아도
    /// 결정력이 높은 선수는 포처에, 헤딩과 힘이 좋은 선수는 타깃맨에 맞는다.
    /// 그래야 "제일 잘하는 선수를 아무 자리에나" 두는 대신 역할을 고를 이유가 생긴다.
    /// </summary>
    public static class RoleFit
    {
        /// <summary>적합도 1.0 이 평범함. 이 폭만큼 위아래로 벌어진다.</summary>
        public const float Swing = BalanceConfig.RoleFitSwing;

        /// <summary>
        /// 0~1 척도의 적합도. 0.5 가 "역할이 요구하는 능력치가 본인 평균과 같음".
        /// </summary>
        public static float Score(PlayerData p, RoleDefinition def)
        {
            if (p == null || def == null) return 0.5f;

            float sum = 0f;
            int n = 0;

            void Take(float v) { sum += v; n++; }

            if (def.keyTechnicalAttributes != null)
                foreach (var a in def.keyTechnicalAttributes) Take(Value(p, a));
            if (def.keyPhysicalAttributes != null)
                foreach (var a in def.keyPhysicalAttributes) Take(Value(p, a));
            if (def.keyMentalAttributes != null)
                foreach (var a in def.keyMentalAttributes) Take(Value(p, a));
            if (def.keyGkAttributes != null)
                foreach (var a in def.keyGkAttributes) Take(Value(p, a));

            if (n == 0) return 0.5f;

            float key = sum / n;
            float overall = Overall(p);
            if (overall <= 0f) return 0.5f;

            // 핵심 능력치가 본인 평균보다 얼마나 높은지. ±25% 를 0~1 로 편다.
            float lean = (key - overall) / overall;
            return Math.Clamp(0.5f + lean * 2f, 0f, 1f);
        }

        /// <summary>경기력에 곱해지는 값. 적합도 0.5 에서 1.0 이다.</summary>
        public static float Multiplier(PlayerData p, RoleDefinition def)
            => 1f + (Score(p, def) - 0.5f) * 2f * Swing;

        /// <summary>화면에 쓰는 0~100 적합도.</summary>
        public static int Percent(PlayerData p, RoleDefinition def)
            => (int)MathF.Round(Score(p, def) * 100f);

        /// <summary>
        /// 그 선수에게 가장 맞는 역할. 자리에서 소화 가능한 역할 중에서 고른다.
        ///
        /// PlayerRole 에는 None 이 없고 기본값이 Goalkeeper(0) 다. 못 찾았을 때 default 를
        /// 돌려주면 필드 선수가 골키퍼 역할을 달게 되므로 null 로 분명히 알린다.
        /// </summary>
        public static PlayerRole? Best(PlayerData p, Position position)
        {
            PlayerRole? best = null;
            float bestScore = -1f;

            // 그 자리에서 쓸 수 있는 역할만 본다. 추려 둔 순서는 전체 순서와 같으므로,
            // 동점일 때 뽑히는 역할도 같다.
            foreach (var def in RoleDatabase.ForPosition(position))
            {
                float s = Score(p, def);
                if (s > bestScore) { bestScore = s; best = def.role; }
            }

            return best;
        }

        /// <summary>그 자리에서 쓸 수 있는 역할. 추리는 규칙은 역할 사전 한 곳에 둔다.</summary>
        public static List<PlayerRole> For(Position position)
        {
            var defs = RoleDatabase.ForPosition(position);
            var list = new List<PlayerRole>(defs.Length);
            foreach (var def in defs) list.Add(def.role);
            return list;
        }

        // ── 임무 ──────────────────────────────────────────────────────────

        static int Axis(RoleDuty duty) => duty switch
        {
            RoleDuty.Attack => 1,
            RoleDuty.Defend => -1,
            _ => 0,
        };

        /// <summary>
        /// 임무가 공격·수비 기여를 옮긴다.
        ///
        /// 기준은 <b>그 역할의 기본 임무</b>다. 절대값으로 재면 수비 임무만 허용하는
        /// 전문 역할(스토퍼, 커버, 앵커…)이 자동으로 18% 더 수비적이 되는데, 그건 이미
        /// 포지션 가중치가 반영하고 있는 것이라 두 번 걸린다. 실제로 그렇게 두자
        /// 리그 전체 득점이 2.74 에서 2.32 로 내려갔다.
        ///
        /// 총량은 건드리지 않는다 — 한쪽을 올리면 다른 쪽을 같은 만큼 내린다.
        /// </summary>
        public static (float atk, float def) DutyShift(RoleDefinition def, RoleDuty duty)
        {
            int step = Axis(duty) - Axis(DefaultDuty(def));
            if (step == 0) return (1f, 1f);

            float shift = step * BalanceConfig.DutyShift;
            return (1f + shift, 1f - shift);
        }

        /// <summary>그 역할이 허용하는 임무인지.</summary>
        public static bool Allows(RoleDefinition def, RoleDuty duty)
            => def?.allowedDuties == null || def.allowedDuties.Count == 0 || def.allowedDuties.Contains(duty);

        /// <summary>허용된 임무 중 기본값. 중간(지원)을 선호한다.</summary>
        public static RoleDuty DefaultDuty(RoleDefinition def)
        {
            if (def?.allowedDuties == null || def.allowedDuties.Count == 0) return RoleDuty.Support;
            if (def.allowedDuties.Contains(RoleDuty.Support)) return RoleDuty.Support;
            return def.allowedDuties[0];
        }

        // ── 팀 균형 ───────────────────────────────────────────────────────

        /// <summary>역할의 성격. 역할이 요구하는 능력치에서 끌어낸다 — 손으로 분류하면 자의적이다.</summary>
        public enum Archetype { Creator, Finisher, Defender, Worker }

        /// <summary>
        /// 역할별 갈래 표. 역할 정의는 바뀌지 않으므로 한 번만 분류한다 —
        /// 균형 배수는 경기마다 두 번, 세계를 만들 때 수천 번 불리는데
        /// 그때마다 열한 자리의 역할을 하나씩 다시 분류하고 있었다.
        /// 정적 필드 초기화는 런타임이 한 번만 돌려 주므로 잠금이 필요 없다.
        /// </summary>
        static readonly Dictionary<PlayerRole, Archetype> Archetypes = BuildArchetypes();

        static Dictionary<PlayerRole, Archetype> BuildArchetypes()
        {
            var map = new Dictionary<PlayerRole, Archetype>(RoleDatabase.All.Count);
            foreach (var (role, def) in RoleDatabase.All) map[role] = TypeOf(def);
            return map;
        }

        public static Archetype TypeOf(RoleDefinition def)
        {
            if (def == null) return Archetype.Worker;

            int creator = 0, finisher = 0, defender = 0;

            if (def.keyTechnicalAttributes != null)
                foreach (var a in def.keyTechnicalAttributes)
                    switch (a)
                    {
                        case TechnicalAttribute.Passing:
                        case TechnicalAttribute.LongPassing:
                        case TechnicalAttribute.Crossing: creator++; break;
                        case TechnicalAttribute.Finishing:
                        case TechnicalAttribute.Shooting:
                        case TechnicalAttribute.Heading: finisher++; break;
                        case TechnicalAttribute.Tackling:
                        case TechnicalAttribute.Marking: defender++; break;
                    }

            if (def.keyMentalAttributes != null)
                foreach (var a in def.keyMentalAttributes)
                    switch (a)
                    {
                        case MentalAttribute.Vision:
                        case MentalAttribute.Teamwork: creator++; break;
                        case MentalAttribute.OffTheBall:
                        case MentalAttribute.Composure: finisher++; break;
                        case MentalAttribute.Positioning:
                        case MentalAttribute.Concentration: defender++; break;
                    }

            if (defender >= creator && defender >= finisher && defender > 0) return Archetype.Defender;
            if (creator > finisher && creator > 0) return Archetype.Creator;
            if (finisher > 0) return Archetype.Finisher;
            return Archetype.Worker;
        }

        /// <summary>만드는 역할과 넣는 역할이 각각 이만큼은 있어야 깎이지 않는다.</summary>
        public const int HealthyMinimum = 2;

        /// <summary>
        /// 팀의 역할 구성이 쓸 만한지. 공격 전개에 곱해지는 값이다.
        ///
        /// 반반을 요구하면 안 된다 — 4-2-3-1 처럼 최전방이 한 명인 정상 포메이션까지
        /// 깎여 리그 전체 득점이 내려간다. 비율이 아니라 <b>부재</b>를 본다.
        /// 만들어 줄 사람이 없거나 넣어 줄 사람이 없을 때만 공격이 겉돈다.
        /// </summary>
        /// <summary>
        /// 슬롯 목록을 그대로 받는다. 경기마다 두 번 불리는 자리라
        /// 역할만 뽑아내는 LINQ 한 겹도 아깝다 — 보는 것과 값은 같다.
        /// </summary>
        public static float BalanceMultiplier(List<FormationSlot> slots)
        {
            int creators = 0, finishers = 0, assigned = 0;

            for (int i = 0; i < slots.Count; i++)
            {
                var r = slots[i].role;
                if (r == null || !Archetypes.TryGetValue(r.role, out var kind)) continue;

                assigned++;
                switch (kind)
                {
                    case Archetype.Creator: creators++; break;
                    case Archetype.Finisher: finishers++; break;
                }
            }

            return Verdict(creators, finishers, assigned);
        }

        public static float BalanceMultiplier(IEnumerable<RoleAssignment> roles)
        {
            int creators = 0, finishers = 0, assigned = 0;

            foreach (var r in roles)
            {
                if (r == null || !Archetypes.TryGetValue(r.role, out var kind)) continue;

                assigned++;
                switch (kind)
                {
                    case Archetype.Creator: creators++; break;
                    case Archetype.Finisher: finishers++; break;
                }
            }

            return Verdict(creators, finishers, assigned);
        }

        /// <summary>센 결과를 배수로. 두 입구가 같은 판정을 쓰도록 한 곳에 둔다.</summary>
        static float Verdict(int creators, int finishers, int assigned)
        {
            // 역할을 아예 지정하지 않은 전술은 깎지 않는다. 없는 것을 벌하면
            // 역할을 쓰지 않는 쪽이 불리해지고, 실제로 그렇게 두자 득점이 2.55 에서 1.63 으로 떨어졌다.
            if (assigned == 0) return 1f;

            int minority = Math.Min(creators, finishers);
            if (minority >= HealthyMinimum) return 1f;

            float shortfall = (HealthyMinimum - minority) / (float)HealthyMinimum;
            return 1f - shortfall * BalanceConfig.RoleBalancePenalty;
        }

        // ── 능력치 읽기 ───────────────────────────────────────────────────

        /// <summary>
        /// 본인 평균. 골키퍼는 필드 능력치가 낮게 생성되므로 GK 능력치로 잰다 —
        /// 섞으면 모든 골키퍼가 모든 역할에 잘 맞는 것으로 나온다.
        /// </summary>
        static float Overall(PlayerData p)
            => (p.positionFlags & Position.GK) != 0 && p.goalkeeper != null
                ? p.goalkeeper.Average()
                // 필드 선수의 본인 평균은 AbilityScore 와 글자 그대로 같은 식이다.
                // 그쪽은 캐시가 있으니 능력치 29개를 다시 더하지 않는다.
                : p.AbilityScore;

        public static int Value(PlayerData p, TechnicalAttribute a) => a switch
        {
            TechnicalAttribute.Passing => p.technical.passing,
            TechnicalAttribute.Dribbling => p.technical.dribbling,
            TechnicalAttribute.Shooting => p.technical.shooting,
            TechnicalAttribute.Finishing => p.technical.finishing,
            TechnicalAttribute.Heading => p.technical.heading,
            TechnicalAttribute.Crossing => p.technical.crossing,
            TechnicalAttribute.Tackling => p.technical.tackling,
            TechnicalAttribute.Marking => p.technical.marking,
            TechnicalAttribute.FirstTouch => p.technical.firstTouch,
            TechnicalAttribute.LongPassing => p.technical.longPassing,
            TechnicalAttribute.SetPieces => p.technical.setPieces,
            _ => 10,
        };

        public static int Value(PlayerData p, PhysicalAttribute a) => a switch
        {
            PhysicalAttribute.Speed => p.physical.speed,
            PhysicalAttribute.Acceleration => p.physical.acceleration,
            PhysicalAttribute.Stamina => p.physical.stamina,
            PhysicalAttribute.Strength => p.physical.strength,
            PhysicalAttribute.Jumping => p.physical.jumping,
            PhysicalAttribute.Agility => p.physical.agility,
            PhysicalAttribute.Balance => p.physical.balance,
            _ => 10,
        };

        public static int Value(PlayerData p, MentalAttribute a) => a switch
        {
            MentalAttribute.Vision => p.mental.vision,
            MentalAttribute.Composure => p.mental.composure,
            MentalAttribute.Leadership => p.mental.leadership,
            MentalAttribute.Courage => p.mental.courage,
            MentalAttribute.WorkRate => p.mental.workRate,
            MentalAttribute.Concentration => p.mental.concentration,
            MentalAttribute.Decisions => p.mental.decisions,
            MentalAttribute.Positioning => p.mental.positioning,
            MentalAttribute.Aggression => p.mental.aggression,
            MentalAttribute.Teamwork => p.mental.teamwork,
            MentalAttribute.OffTheBall => p.mental.offTheBall,
            _ => 10,
        };

        public static int Value(PlayerData p, GkAttribute a)
        {
            var g = p.goalkeeper;
            if (g == null) return 10;

            return a switch
            {
                GkAttribute.Handling => g.handling,
                GkAttribute.Diving => g.diving,
                GkAttribute.Reflexes => g.reflexes,
                GkAttribute.Positioning => g.positioning,
                GkAttribute.Communication => g.communication,
                GkAttribute.Kicking => g.kicking,
                GkAttribute.Throwing => g.throwing,
                GkAttribute.OneOnOne => g.oneOnOne,
                GkAttribute.AerialAbility => g.aerialAbility,
                GkAttribute.CommandArea => g.commandArea,
                _ => 10,
            };
        }
    }
}
