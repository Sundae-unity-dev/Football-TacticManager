using System;

namespace FootballTactician.Data
{
    [Serializable]
    public class CommentaryLine
    {
        public int                 minute;
        public CommentaryEventType eventType;
        public CommentaryTone      tone;
        public string              text;
        public bool                isHighlight;   // UI 하이라이트용 (골/레드/막판승부 등)
    }
}
