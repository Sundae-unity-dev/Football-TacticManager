using System;
using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace FootballTactician.Data
{
    /// <summary>
    /// 시즌 넘김이 <b>도중에 멈춰 있는</b> 동안의 자리. 넘김이 끝나면 비운다.
    ///
    /// 승격·챔피언 플레이오프는 시즌 넘김 한가운데서 치러진다. 내 경기를 감독이 직접
    /// 뛰려면 넘김을 거기서 끊었다가 이어 붙여야 하는데, 넘김은 한 번에 도는 긴 길이라
    /// 중간 상태를 하나하나 저장하기엔 갈래가 너무 많다.
    ///
    /// 그래서 <b>치른 경기만 적어 둔다</b>. 이어 붙일 때는 대진을 처음부터 다시 걷되,
    /// 적어 둔 경기는 다시 치르지 않고 적힌 점수를 쓴다. 대진은 점수만 같으면 같은 길로
    /// 가므로, 멈춘 자리까지 똑같이 와서 내 경기 점수를 받아 그 다음으로 넘어간다.
    /// 경기를 다시 치르지 않으니 선수 기록과 피로도 두 번 쌓이지 않는다.
    /// </summary>
    [Serializable]
    public sealed class RolloverState
    {
        /// <summary>넘기는 중인 시즌.</summary>
        public int year;

        /// <summary>남은 경기와 시상까지 끝냈는지. 이어 붙일 때 시상을 두 번 하지 않는다.</summary>
        public bool closed;

        /// <summary>치른 플레이오프 경기. 이어 붙일 때 이 점수를 그대로 쓴다.</summary>
        public List<PlayoffLeg> legs = new();

        /// <summary>
        /// 이 구단의 경기 앞에서 멈춘다. 0 이면 멈추지 않는다.
        /// 부를 때마다 정하므로 저장하지 않는다.
        /// </summary>
        [JsonIgnore] public int deferClubId;

        [JsonIgnore] Dictionary<(int, int, int, int), PlayoffLeg> index;

        public bool TryGet(int seedKey, int home, int away, int tag, out (int home, int away) score)
        {
            if (index == null)
            {
                index = new();
                foreach (var l in legs) index[l.Key] = l;
            }

            if (index.TryGetValue((seedKey, home, away, tag), out var leg))
            {
                score = (leg.homeGoals, leg.awayGoals);
                return true;
            }
            score = default;
            return false;
        }

        public void Add(PlayoffLeg leg)
        {
            legs.Add(leg);
            if (index != null) index[leg.Key] = leg;
        }
    }

    /// <summary>
    /// 플레이오프 한 경기. 대회(씨드 열쇠) · 홈 · 원정 · 회차로 한 판이 정해진다.
    /// </summary>
    [Serializable]
    public sealed class PlayoffLeg
    {
        public int seedKey;
        public int homeClubId;
        public int awayClubId;
        public int tag;
        public int homeGoals;
        public int awayGoals;

        [JsonIgnore] public (int, int, int, int) Key => (seedKey, homeClubId, awayClubId, tag);
    }
}
