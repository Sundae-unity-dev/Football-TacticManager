using System;

namespace FootballTactician.Data
{
    /// <summary>
    /// 일정에 없는데 감독이 직접 치러야 할 경기 한 판.
    ///
    /// 경기 화면은 <see cref="FixtureData"/> 하나로 돌아간다. 그래서 컵 경기도 그 모양으로
    /// 싸 두면 화면을 한 줄도 고치지 않고 그대로 쓸 수 있다 — 일정에는 넣지 않으므로
    /// 순위표에도 잡히지 않는다.
    ///
    /// 이것이 남아 있는 동안 세계는 더 나아가지 않는다. 경기가 끝나야 그 대진이 이어지고,
    /// 그 주의 남은 일(A매치·2군·주 넘김)이 치러진다.
    /// </summary>
    [Serializable]
    public sealed class PendingMatch
    {
        /// <summary>화면에 넘길 경기. 일정에는 들어 있지 않다.</summary>
        public FixtureData fixture;

        /// <summary>결과를 돌려보낼 대회. 그 대회의 지금 라운드에서 내 경기를 찾는다.</summary>
        public int cupId;

        /// <summary>끝나고 마저 돌려야 할 라운드 번호.</summary>
        public int matchday;

        /// <summary>"FA컵 8강" 처럼 화면에 적을 말.</summary>
        public string label;
    }
}
