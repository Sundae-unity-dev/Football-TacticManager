using System;
using System.Collections.Generic;

namespace FootballTactician.Data
{
    /// <summary>한 세션 = 하루의 한 훈련. 휴식일은 category=Recovery + intensity=VeryLight 권장.</summary>
    [Serializable]
    public class TrainingSession
    {
        public TrainingCategory  category;
        public TrainingIntensity intensity;
    }

    /// <summary>한 주(7일) 훈련 계획. 인덱스 0=월, 6=일.</summary>
    [Serializable]
    public class WeeklyTrainingSchedule
    {
        public int                  clubId;
        public List<TrainingSession> sessions = new List<TrainingSession>(7);

        public static WeeklyTrainingSchedule Default(int clubId)
        {
            var w = new WeeklyTrainingSchedule { clubId = clubId };
            void Add(TrainingCategory c, TrainingIntensity i) =>
                w.sessions.Add(new TrainingSession { category = c, intensity = i });

            Add(TrainingCategory.Physical,   TrainingIntensity.Normal);   // 월
            Add(TrainingCategory.Tactics,    TrainingIntensity.Normal);   // 화
            Add(TrainingCategory.Technical,  TrainingIntensity.Normal);   // 수
            Add(TrainingCategory.Defending,  TrainingIntensity.Normal);   // 목
            Add(TrainingCategory.Attacking,  TrainingIntensity.Normal);   // 금
            Add(TrainingCategory.SetPieces,  TrainingIntensity.Light);    // 토
            Add(TrainingCategory.Recovery,   TrainingIntensity.VeryLight);// 일
            return w;
        }
    }

    /// <summary>주간 훈련 결과 — 선수 1명 단위 보고서. UI/로그용.</summary>
    [Serializable]
    public class TrainingWeekReport
    {
        public int                playerId;
        public List<AttributeGain> gains = new List<AttributeGain>();
        public float              conditionDelta;       // -100 ~ +100 (음수 = 피로)
        public bool               injured;
        public InjurySeverity     injurySeverity;
    }

    [Serializable]
    public class AttributeGain
    {
        public string attributeName;   // 예: "passing", "stamina"
        public int    delta;           // +1 등 정수 증가 (소수 누적은 시스템 내부)
    }
}
