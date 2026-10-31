using System;
using System.Collections.Generic;
using FootballTactician.Balance;
using FootballTactician.Data;

namespace FootballTactician.World
{
    /// <summary>
    /// 주간 성장. BalanceConfig.WeeklyGrowth 로 CA 변화량을 구하고, 그것을 개별 능력치로 옮긴다.
    ///
    /// 매치 엔진은 CA 가 아니라 개별 능력치를 직접 읽는다. 그래서 CA 만 올려두면 경기에 반영되지 않는다.
    /// 반대로 능력치를 매주 다시 뽑으면 선수 성격이 사라지므로, 변화분만큼만 항목을 집어 올리고 내린다.
    /// </summary>
    public static class PlayerDevelopment
    {
        /// <summary>능력치 1점이 전력 평균을 움직이는 폭. 세 카테고리 평균의 평균이라 분모가 다르다.</summary>
        const float TechStep = 1f / 11f / 3f;
        const float PhysStep = 1f / 7f / 3f;
        const float MentStep = 1f / 11f / 3f;

        /// <summary>한 주 성장을 적용하고 CA 변화량을 돌려준다.</summary>
        public static float ApplyWeek(
            GameWorld w, PlayerData p, Random rng,
            float coach, float facility, float minutes)
        {
            if (p.IsInjured) minutes *= 0.3f;

            int age = p.GetAge(w.Date);
            float ca = GameWorld.Ca(p);
            float pa = Pa(p);
            float professionalism = 0.7f + p.hidden.professionalism / 20f * 0.7f;  // 0.7 ~ 1.4

            float delta = BalanceConfig.WeeklyGrowth(age, ca, pa, coach, facility, professionalism, minutes);
            if (Math.Abs(delta) < 1e-5f) return 0f;

            // CA 변화를 전력(1~20) 변화로 바꾸고, 반올림 손실은 다음 주로 넘긴다.
            float wantPower = delta * 0.055f;
            w.GrowthCarry.TryGetValue(p.id, out float carry);
            float budget = carry + wantPower;

            int moved = Redistribute(p, budget, age, rng, out float spent);
            w.GrowthCarry[p.id] = budget - spent;

            if (moved != 0) p.InvalidateAbilityCache();
            return delta;
        }

        public static float Pa(PlayerData p) => Math.Clamp(p.potential * 2f, 20f, 200f);

        /// <summary>
        /// 전력 변화분을 능력치에 배분한다. 성장은 포지션에 맞는 항목부터,
        /// 하락은 나이가 먼저 깎는 신체 항목부터 건드린다.
        /// </summary>
        static int Redistribute(PlayerData p, float budget, int age, Random rng, out float spent)
        {
            spent = 0f;
            int moved = 0;
            int guard = 0;
            bool up = budget > 0f;

            while (guard++ < 24)
            {
                if (up && budget - spent < PhysStep * 0.9f) break;
                if (!up && budget - spent > -PhysStep * 0.9f) break;

                var slot = Pick(p, up, age, rng);
                if (!slot.Exists) break;

                int v = Read(p, slot);
                if (up && v >= 20) continue;
                if (!up && v <= 1) continue;

                Write(p, slot, v + (up ? 1 : -1));
                spent += up ? slot.Step : -slot.Step;
                moved++;
            }
            return moved;
        }

        /// <summary>능력치 묶음. 어느 표의 몇 번째 칸인지로 자리를 가리킨다.</summary>
        enum Attrs { None, Technical, Physical, Mental, Goalkeeper }

        /// <summary>
        /// 건드릴 능력치 한 칸.
        ///
        /// 전에는 칸마다 객체 하나와 대리자 둘(읽기·쓰기)을 새로 만들었다. 능력치가 한 칸
        /// 움직일 때마다 할당이 네 번이고, 매주 선수 6,000명이 이 길을 지난다.
        /// 번호만 들고 다니면 할당이 없다 — 뽑는 난수와 고르는 칸은 그대로다.
        /// </summary>
        readonly struct Slot
        {
            public readonly Attrs group;
            public readonly int index;
            public readonly float Step;

            public Slot(Attrs group, int index, float step)
            {
                this.group = group;
                this.index = index;
                Step = step;
            }

            public bool Exists => group != Attrs.None;
        }

        static int Read(PlayerData p, Slot s)
        {
            var t = p.technical; var ph = p.physical; var m = p.mental; var g = p.goalkeeper;
            return s.group switch
            {
                Attrs.Technical => s.index switch
                {
                    0 => t.passing, 1 => t.dribbling, 2 => t.shooting, 3 => t.finishing,
                    4 => t.heading, 5 => t.crossing, 6 => t.tackling, 7 => t.marking,
                    8 => t.firstTouch, 9 => t.longPassing, _ => t.setPieces,
                },
                Attrs.Physical => s.index switch
                {
                    0 => ph.speed, 1 => ph.acceleration, 2 => ph.stamina, 3 => ph.strength,
                    4 => ph.jumping, 5 => ph.agility, _ => ph.balance,
                },
                Attrs.Mental => s.index switch
                {
                    0 => m.vision, 1 => m.composure, 2 => m.leadership, 3 => m.courage,
                    4 => m.workRate, 5 => m.concentration, 6 => m.decisions, 7 => m.positioning,
                    8 => m.aggression, 9 => m.teamwork, _ => m.offTheBall,
                },
                Attrs.Goalkeeper => s.index switch
                {
                    0 => g.handling, 1 => g.diving, 2 => g.reflexes, 3 => g.positioning,
                    4 => g.communication, 5 => g.kicking, 6 => g.throwing, 7 => g.oneOnOne,
                    8 => g.aerialAbility, _ => g.commandArea,
                },
                _ => 0,
            };
        }

        static void Write(PlayerData p, Slot s, int v)
        {
            var t = p.technical; var ph = p.physical; var m = p.mental; var g = p.goalkeeper;
            switch (s.group)
            {
                case Attrs.Technical:
                    switch (s.index)
                    {
                        case 0: t.passing = v; break;
                        case 1: t.dribbling = v; break;
                        case 2: t.shooting = v; break;
                        case 3: t.finishing = v; break;
                        case 4: t.heading = v; break;
                        case 5: t.crossing = v; break;
                        case 6: t.tackling = v; break;
                        case 7: t.marking = v; break;
                        case 8: t.firstTouch = v; break;
                        case 9: t.longPassing = v; break;
                        default: t.setPieces = v; break;
                    }
                    break;

                case Attrs.Physical:
                    switch (s.index)
                    {
                        case 0: ph.speed = v; break;
                        case 1: ph.acceleration = v; break;
                        case 2: ph.stamina = v; break;
                        case 3: ph.strength = v; break;
                        case 4: ph.jumping = v; break;
                        case 5: ph.agility = v; break;
                        default: ph.balance = v; break;
                    }
                    break;

                case Attrs.Mental:
                    switch (s.index)
                    {
                        case 0: m.vision = v; break;
                        case 1: m.composure = v; break;
                        case 2: m.leadership = v; break;
                        case 3: m.courage = v; break;
                        case 4: m.workRate = v; break;
                        case 5: m.concentration = v; break;
                        case 6: m.decisions = v; break;
                        case 7: m.positioning = v; break;
                        case 8: m.aggression = v; break;
                        case 9: m.teamwork = v; break;
                        default: m.offTheBall = v; break;
                    }
                    break;

                case Attrs.Goalkeeper:
                    switch (s.index)
                    {
                        case 0: g.handling = v; break;
                        case 1: g.diving = v; break;
                        case 2: g.reflexes = v; break;
                        case 3: g.positioning = v; break;
                        case 4: g.communication = v; break;
                        case 5: g.kicking = v; break;
                        case 6: g.throwing = v; break;
                        case 7: g.oneOnOne = v; break;
                        case 8: g.aerialAbility = v; break;
                        default: g.commandArea = v; break;
                    }
                    break;
            }
        }

        static Slot Pick(PlayerData p, bool up, int age, Random rng)
        {
            bool gk = (p.positionFlags & Position.GK) != 0;

            // 하락 구간은 신체 능력부터 빠진다 — 30대 선수가 패스는 유지하고 속도를 잃는 모습.
            int roll = rng.Next(100);
            if (!up)
            {
                if (roll < 60) return PhysSlot(rng);
                if (roll < 80) return gk ? GkSlot(rng) : TechSlot(rng);
                return MentSlot(rng);
            }

            if (gk)
            {
                if (roll < 55) return GkSlot(rng);
                if (roll < 75) return MentSlot(rng);
                return PhysSlot(rng);
            }

            // 어린 선수일수록 신체가 같이 자란다.
            int physChance = age <= 21 ? 35 : age <= 25 ? 25 : 15;
            if (roll < physChance) return PhysSlot(rng);
            if (roll < physChance + 40) return TechSlot(rng);
            return MentSlot(rng);
        }

        static Slot TechSlot(Random r) => new(Attrs.Technical, r.Next(11), TechStep);
        static Slot PhysSlot(Random r) => new(Attrs.Physical, r.Next(7), PhysStep);
        static Slot MentSlot(Random r) => new(Attrs.Mental, r.Next(11), MentStep);

        /// <summary>골키퍼 항목은 전력 평균에 들어가지 않지만, 공짜로 자라지 않도록 같은 비용을 물린다.</summary>
        static Slot GkSlot(Random r) => new(Attrs.Goalkeeper, r.Next(10), TechStep);
    }
}
