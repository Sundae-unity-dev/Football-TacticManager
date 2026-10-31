using System;
using System.Collections.Generic;

namespace FootballTactician.Data
{
    /// <summary>한 답변 선택지. tone과 매니저 PressStyle 매칭에 따라 효과가 증감된다.</summary>
    [Serializable]
    public class PressAnswerOption
    {
        public string            text;
        public ManagerPressStyle tone;
        public int               pressDelta;     // 언론평판 ±
        public int               fanDelta;       // 팬지지도 ±
        public int               boardDelta;     // 이사회신뢰도 ±
    }

    /// <summary>한 회 기자회견 세션 — 질문 1개 + 선택지 3~4개.</summary>
    [Serializable]
    public class PressConferenceSession
    {
        public PressConferenceQuestion question;
        public string                  questionText;
        public List<PressAnswerOption> options       = new List<PressAnswerOption>();
        public int                     selectedIndex = -1;
    }
}
