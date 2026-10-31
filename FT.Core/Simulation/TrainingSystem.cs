using System;
using System.Collections.Generic;
using FootballTactician.Data;

namespace FootballTactician.Simulation
{
    public class TrainingContext
    {
        public List<CoachingAttributes> coaches = new List<CoachingAttributes>();
        public int                       facilityRating = 3;   // 1~5 (ClubData.trainingFacilitiesRating)
        public GameDate                  currentDate;
        public int                       seed = -1;            // -1 = 무작위
    }

    /// <summary>
    /// 주간 훈련 적용기. 능력치 성장(확률), 컨디션 변화, 부상 위험을 한 번에 처리.
    /// Unity 의존성 없음 — 단위 테스트 가능.
    /// 핫패스 최적화: 능력치 식별을 enum 점프테이블로, 카테고리→타깃 배열을 static readonly로 보유.
    /// </summary>
    public static class TrainingSystem
    {
        // ── 진입점 ────────────────────────────────────────────────────────────

        public static Dictionary<int, TrainingWeekReport> ApplyWeek(
            IEnumerable<PlayerData> squad,
            WeeklyTrainingSchedule schedule,
            TrainingContext ctx)
        {
            var rng     = ctx.seed >= 0 ? new Random(ctx.seed) : new Random();
            var reports = new Dictionary<int, TrainingWeekReport>();
            var coachQ  = BuildCoachQuality(ctx.coaches);   // float[(int)TrainingCategory.Count]
            var accum   = new int[(int)AttrId.Count];

            foreach (var p in squad)
            {
                if (p == null) continue;
                var report = new TrainingWeekReport { playerId = p.id };
                reports[p.id] = report;

                if (p.IsInjured) continue;     // 부상 선수는 회복 트랙 별도 — 본 시스템 스킵

                Array.Clear(accum, 0, accum.Length);

                int age = p.GetAge(ctx.currentDate);
                bool stoppedByInjury = false;

                foreach (var session in schedule.sessions)
                {
                    if (stoppedByInjury) break;

                    ApplySession(p, age, session, ctx, coachQ, accum, report, rng);

                    if (RollInjury(p, session.intensity, rng))
                    {
                        AssignInjury(p, ctx.currentDate, report, rng);
                        stoppedByInjury = true;
                    }
                }

                FlushGains(accum, report);
                p.condition = SimMath.Clamp(p.condition + report.conditionDelta, 0f, 100f);
                p.InvalidateAbilityCache();
            }

            return reports;
        }

        // ── 세션 적용 ─────────────────────────────────────────────────────────

        private static void ApplySession(
            PlayerData p, int age, TrainingSession s, TrainingContext ctx,
            float[] coachQ,
            int[] accum, TrainingWeekReport report, Random rng)
        {
            report.conditionDelta += ConditionDelta(s.category, s.intensity);

            if (s.category == TrainingCategory.Recovery) return;

            bool isGk = (p.positionFlags & Position.GK) != 0;
            if (s.category == TrainingCategory.Goalkeeping && !isGk) return;
            if (s.category == TrainingCategory.YouthDevelopment && age > 21) return;

            float intensityMult = IntensityGrowthMult(s.intensity);
            float coachMult     = SimMath.Clamp(0.5f + coachQ[(int)s.category] / 30f, 0.5f, 1.7f);
            float facilityMult  = 0.7f + (ctx.facilityRating - 1) * 0.15f;
            float ageMult       = AgeMult(age);
            float profMult      = 0.7f + p.hidden.professionalism  / 33f;
            float ambitionMult  = 0.85f + p.hidden.ambition        / 67f;
            float conditionMult = 0.4f + p.condition / 167f;

            float potentialGap  = Math.Max(0, p.potential - p.CurrentAbility);
            float potentialMult = SimMath.Clamp(potentialGap / 20f, 0.05f, 1.5f);

            float baseGrowth = 0.05f * intensityMult * coachMult * facilityMult
                             * ageMult * profMult * ambitionMult
                             * conditionMult * potentialMult;

            if (s.category == TrainingCategory.YouthDevelopment) baseGrowth *= 1.4f;
            if (s.category == TrainingCategory.MatchPrepare)     baseGrowth *= 0.3f;

            float growthClamped = SimMath.Clamp01(baseGrowth);
            var targets = TargetAttributes(s.category, isGk);

            for (int i = 0; i < targets.Length; i++)
            {
                if (rng.NextDouble() < growthClamped && TryIncrement(p, targets[i]))
                    accum[(int)targets[i]]++;
            }
        }

        // ── 카테고리 → 능력치 매핑 (static readonly — 할당 0) ─────────────────

        private static readonly AttrId[] _physical =
            { AttrId.Speed, AttrId.Acceleration, AttrId.Stamina, AttrId.Strength,
              AttrId.Jumping, AttrId.Agility, AttrId.Balance };

        private static readonly AttrId[] _technical =
            { AttrId.Passing, AttrId.Dribbling, AttrId.FirstTouch,
              AttrId.LongPassing, AttrId.Crossing, AttrId.Heading };

        private static readonly AttrId[] _tactics =
            { AttrId.Positioning, AttrId.Decisions, AttrId.Teamwork, AttrId.Vision };

        private static readonly AttrId[] _defending =
            { AttrId.Tackling, AttrId.Marking, AttrId.Heading,
              AttrId.Positioning, AttrId.Concentration };

        private static readonly AttrId[] _attacking =
            { AttrId.Shooting, AttrId.Finishing, AttrId.OffTheBall, AttrId.Composure };

        private static readonly AttrId[] _setPieces =
            { AttrId.SetPieces, AttrId.Crossing, AttrId.LongPassing };

        private static readonly AttrId[] _goalkeeping =
            { AttrId.Handling, AttrId.Diving, AttrId.Reflexes, AttrId.OneOnOne,
              AttrId.Kicking, AttrId.Throwing, AttrId.AerialAbility, AttrId.CommandArea };

        private static readonly AttrId[] _youthOutfield =
            { AttrId.Passing, AttrId.Dribbling, AttrId.FirstTouch,
              AttrId.Stamina, AttrId.Agility, AttrId.Decisions };

        private static readonly AttrId[] _youthGk =
            { AttrId.Handling, AttrId.Reflexes, AttrId.Positioning, AttrId.Kicking };

        private static readonly AttrId[] _matchPrepare =
            { AttrId.Positioning, AttrId.Concentration };

        private static readonly AttrId[] _empty = Array.Empty<AttrId>();

        private static AttrId[] TargetAttributes(TrainingCategory cat, bool isGk) => cat switch
        {
            TrainingCategory.Physical         => _physical,
            TrainingCategory.Technical        => _technical,
            TrainingCategory.Tactics          => _tactics,
            TrainingCategory.Defending        => _defending,
            TrainingCategory.Attacking        => _attacking,
            TrainingCategory.SetPieces        => _setPieces,
            TrainingCategory.Goalkeeping      => _goalkeeping,
            TrainingCategory.YouthDevelopment => isGk ? _youthGk : _youthOutfield,
            TrainingCategory.MatchPrepare     => _matchPrepare,
            _                                 => _empty,
        };

        // ── 능력치 식별자 (enum 점프테이블 — switch에서 O(1)) ─────────────────

        private enum AttrId
        {
            // Technical
            Passing, Dribbling, Shooting, Finishing, Heading, Crossing,
            Tackling, Marking, FirstTouch, LongPassing, SetPieces,
            // Physical
            Speed, Acceleration, Stamina, Strength, Jumping, Agility, Balance,
            // Mental
            Vision, Composure, WorkRate, Concentration, Decisions, Positioning,
            Teamwork, OffTheBall,
            // Goalkeeper
            Handling, Diving, Reflexes, Kicking, Throwing, OneOnOne,
            AerialAbility, CommandArea,

            Count,
        }

        // AttributeGain.attributeName 에 사용 (UI 직렬화 호환)
        private static readonly string[] _attrNames =
        {
            "passing", "dribbling", "shooting", "finishing", "heading", "crossing",
            "tackling", "marking", "firstTouch", "longPassing", "setPieces",
            "speed", "acceleration", "stamina", "strength", "jumping", "agility", "balance",
            "vision", "composure", "workRate", "concentration", "decisions", "positioning",
            "teamwork", "offTheBall",
            "handling", "diving", "reflexes", "kicking", "throwing", "oneOnOne",
            "aerialAbility", "commandArea",
        };

        private static bool TryIncrement(PlayerData p, AttrId id)
        {
            switch (id)
            {
                case AttrId.Passing:       if (p.technical.passing      >= 99) return false; p.technical.passing++;      return true;
                case AttrId.Dribbling:     if (p.technical.dribbling    >= 99) return false; p.technical.dribbling++;    return true;
                case AttrId.Shooting:      if (p.technical.shooting     >= 99) return false; p.technical.shooting++;     return true;
                case AttrId.Finishing:     if (p.technical.finishing    >= 99) return false; p.technical.finishing++;    return true;
                case AttrId.Heading:       if (p.technical.heading      >= 99) return false; p.technical.heading++;      return true;
                case AttrId.Crossing:      if (p.technical.crossing     >= 99) return false; p.technical.crossing++;     return true;
                case AttrId.Tackling:      if (p.technical.tackling     >= 99) return false; p.technical.tackling++;     return true;
                case AttrId.Marking:       if (p.technical.marking      >= 99) return false; p.technical.marking++;      return true;
                case AttrId.FirstTouch:    if (p.technical.firstTouch   >= 99) return false; p.technical.firstTouch++;   return true;
                case AttrId.LongPassing:   if (p.technical.longPassing  >= 99) return false; p.technical.longPassing++;  return true;
                case AttrId.SetPieces:     if (p.technical.setPieces    >= 99) return false; p.technical.setPieces++;    return true;
                case AttrId.Speed:         if (p.physical.speed         >= 99) return false; p.physical.speed++;         return true;
                case AttrId.Acceleration:  if (p.physical.acceleration  >= 99) return false; p.physical.acceleration++;  return true;
                case AttrId.Stamina:       if (p.physical.stamina       >= 99) return false; p.physical.stamina++;       return true;
                case AttrId.Strength:      if (p.physical.strength      >= 99) return false; p.physical.strength++;      return true;
                case AttrId.Jumping:       if (p.physical.jumping       >= 99) return false; p.physical.jumping++;       return true;
                case AttrId.Agility:       if (p.physical.agility       >= 99) return false; p.physical.agility++;       return true;
                case AttrId.Balance:       if (p.physical.balance       >= 99) return false; p.physical.balance++;       return true;
                case AttrId.Vision:        if (p.mental.vision          >= 99) return false; p.mental.vision++;          return true;
                case AttrId.Composure:     if (p.mental.composure       >= 99) return false; p.mental.composure++;       return true;
                case AttrId.WorkRate:      if (p.mental.workRate        >= 99) return false; p.mental.workRate++;        return true;
                case AttrId.Concentration: if (p.mental.concentration   >= 99) return false; p.mental.concentration++;   return true;
                case AttrId.Decisions:     if (p.mental.decisions       >= 99) return false; p.mental.decisions++;       return true;
                case AttrId.Positioning:   if (p.mental.positioning     >= 99) return false; p.mental.positioning++;     return true;
                case AttrId.Teamwork:      if (p.mental.teamwork        >= 99) return false; p.mental.teamwork++;        return true;
                case AttrId.OffTheBall:    if (p.mental.offTheBall      >= 99) return false; p.mental.offTheBall++;      return true;
                case AttrId.Handling:      if (p.goalkeeper.handling      >= 99) return false; p.goalkeeper.handling++;      return true;
                case AttrId.Diving:        if (p.goalkeeper.diving        >= 99) return false; p.goalkeeper.diving++;        return true;
                case AttrId.Reflexes:      if (p.goalkeeper.reflexes      >= 99) return false; p.goalkeeper.reflexes++;      return true;
                case AttrId.Kicking:       if (p.goalkeeper.kicking       >= 99) return false; p.goalkeeper.kicking++;       return true;
                case AttrId.Throwing:      if (p.goalkeeper.throwing      >= 99) return false; p.goalkeeper.throwing++;      return true;
                case AttrId.OneOnOne:      if (p.goalkeeper.oneOnOne      >= 99) return false; p.goalkeeper.oneOnOne++;      return true;
                case AttrId.AerialAbility: if (p.goalkeeper.aerialAbility >= 99) return false; p.goalkeeper.aerialAbility++; return true;
                case AttrId.CommandArea:   if (p.goalkeeper.commandArea   >= 99) return false; p.goalkeeper.commandArea++;   return true;
                default: return false;
            }
        }

        private static void FlushGains(int[] accum, TrainingWeekReport report)
        {
            for (int i = 0; i < accum.Length; i++)
            {
                int delta = accum[i];
                if (delta == 0) continue;
                report.gains.Add(new AttributeGain
                {
                    attributeName = _attrNames[i],
                    delta         = delta,
                });
            }
        }

        // ── 부상 ──────────────────────────────────────────────────────────────

        private static bool RollInjury(PlayerData p, TrainingIntensity intensity, Random rng)
        {
            float intensityMult = intensity switch
            {
                TrainingIntensity.VeryLight => 0.3f,
                TrainingIntensity.Light     => 0.5f,
                TrainingIntensity.Normal    => 1.0f,
                TrainingIntensity.Heavy     => 1.6f,
                TrainingIntensity.VeryHeavy => 2.5f,
                _ => 1.0f,
            };
            float conditionFactor = 1f + (100f - p.condition) / 50f;
            float proneness       = 1f + p.hidden.injuryProneness / 30f;
            float chance          = 0.001f * intensityMult * conditionFactor * proneness;

            return rng.NextDouble() < chance;
        }

        private static void AssignInjury(PlayerData p, GameDate date,
            TrainingWeekReport report, Random rng)
        {
            double r = rng.NextDouble();
            InjurySeverity sev =
                r < 0.70 ? InjurySeverity.Minor    :
                r < 0.92 ? InjurySeverity.Moderate :
                r < 0.99 ? InjurySeverity.Serious  :
                           InjurySeverity.Career;

            int days = sev switch
            {
                InjurySeverity.Minor    => 7  + rng.Next(0, 7),
                InjurySeverity.Moderate => 21 + rng.Next(0, 14),
                InjurySeverity.Serious  => 60 + rng.Next(0, 60),
                _                       => 365,
            };

            p.injury = new InjuryData
            {
                injuryType         = TrainingInjuryName(sev),
                severity           = sev,
                injuryDate         = date,
                expectedReturnDate = date.AddDays(days),
                isActive           = true,
            };

            report.injured        = true;
            report.injurySeverity = sev;
        }

        private static string TrainingInjuryName(InjurySeverity sev) => sev switch
        {
            InjurySeverity.Minor    => "근육 경직",
            InjurySeverity.Moderate => "햄스트링 부상",
            InjurySeverity.Serious  => "인대 손상",
            InjurySeverity.Career   => "심각한 무릎 부상",
            _ => "훈련 부상",
        };

        // ── 컨디션 / 가중치 / 코치 ─────────────────────────────────────────────

        private static float ConditionDelta(TrainingCategory cat, TrainingIntensity intensity)
        {
            if (cat == TrainingCategory.Recovery)     return +12f;
            if (cat == TrainingCategory.MatchPrepare) return +3f;

            return intensity switch
            {
                TrainingIntensity.VeryLight => -2f,
                TrainingIntensity.Light     => -3f,
                TrainingIntensity.Normal    => -5f,
                TrainingIntensity.Heavy     => -8f,
                TrainingIntensity.VeryHeavy => -12f,
                _ => -5f,
            };
        }

        private static float IntensityGrowthMult(TrainingIntensity i) => i switch
        {
            TrainingIntensity.VeryLight => 0.4f,
            TrainingIntensity.Light     => 0.7f,
            TrainingIntensity.Normal    => 1.0f,
            TrainingIntensity.Heavy     => 1.3f,
            TrainingIntensity.VeryHeavy => 1.5f,
            _ => 1.0f,
        };

        private static float AgeMult(int age)
        {
            if (age <= 18) return 1.5f;
            if (age <= 23) return 1.3f;
            if (age <= 27) return 1.0f;
            if (age <= 30) return 0.7f;
            if (age <= 33) return 0.4f;
            return 0.1f;
        }

        // TrainingCategory 항목 수 — enum 추가 시 갱신 필요. 컴파일 시점 어서트로 검증.
        private const int CoachQualityLen = 10;

        private static float[] BuildCoachQuality(List<CoachingAttributes> coaches)
        {
            var q = new float[CoachQualityLen];
            if (coaches == null || coaches.Count == 0)
            {
                for (int i = 0; i < q.Length; i++) q[i] = 5f;
                return q;
            }

            int bestTactical = 0, bestTechnical = 0, bestPhysical = 0,
                bestMental   = 0, bestGk        = 0;
            foreach (var c in coaches)
            {
                if (c == null) continue;
                if (c.tacticalKnowledge > bestTactical)  bestTactical  = c.tacticalKnowledge;
                if (c.technicalCoaching > bestTechnical) bestTechnical = c.technicalCoaching;
                if (c.physicalCoaching  > bestPhysical)  bestPhysical  = c.physicalCoaching;
                if (c.mentalCoaching    > bestMental)    bestMental    = c.mentalCoaching;
                if (c.gkCoaching        > bestGk)        bestGk        = c.gkCoaching;
            }

            q[(int)TrainingCategory.Tactics]          = bestTactical;
            q[(int)TrainingCategory.Technical]        = bestTechnical;
            q[(int)TrainingCategory.Physical]         = bestPhysical;
            q[(int)TrainingCategory.Defending]        = (bestTactical + bestMental) * 0.5f;
            q[(int)TrainingCategory.Attacking]        = (bestTechnical + bestMental) * 0.5f;
            q[(int)TrainingCategory.SetPieces]        = (bestTechnical + bestTactical) * 0.5f;
            q[(int)TrainingCategory.Goalkeeping]      = bestGk;
            q[(int)TrainingCategory.YouthDevelopment] = (bestTechnical + bestPhysical + bestMental) / 3f;
            q[(int)TrainingCategory.MatchPrepare]     = bestTactical;
            q[(int)TrainingCategory.Recovery]         = bestPhysical;
            return q;
        }
    }
}
