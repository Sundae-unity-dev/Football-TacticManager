using System;
using System.Collections.Generic;
using FootballTactician.Data;

namespace FootballTactician.World
{
    /// <summary>
    /// 구단별·자리별 최고 CA 표. 한 창구(또는 한 번의 순회) 동안만 들고 있는다.
    ///
    /// "그 자리에 이 선수보다 나은 사람이 이미 있는가" 는 이적도 임대도 묻는 질문이다.
    /// 그때마다 명단을 전부 훑으면 한 번에 선수 6,000명을 보게 되고, 창구가 열린 주에
    /// 그걸 서른 번 했다. 포지션은 비트라서 <b>비트별 최고</b>를 미리 재 두면
    /// 어떤 조합이든 그중 최대가 답이다 — 조합의 최고는 각 비트 최고 중 하나이므로 값이 같다.
    ///
    /// 표를 들고 있으려면 <b>명단이 바뀌는 곳을 하나로 좁히고</b> 거기서 다시 재야 한다.
    /// 틀린 표는 느린 코드보다 나쁘다 — 임대가 사실과 다른 판단을 하고도 조용히 돌아간다.
    /// </summary>
    public sealed class SquadPeaks
    {
        const int Bits = 15;                 // Position 은 GK(1<<0) 부터 CF(1<<14) 까지

        readonly GameWorld world;
        readonly Dictionary<int, float[]> byClub = new();

        public SquadPeaks(GameWorld w)
        {
            world = w;
            foreach (int id in w.Clubs.Keys) Refresh(id);
        }

        /// <summary>그 구단의 명단이 바뀐 뒤 다시 잰다.</summary>
        public void Refresh(int clubId)
        {
            if (!byClub.TryGetValue(clubId, out var peak))
                byClub[clubId] = peak = new float[Bits];
            else
                Array.Clear(peak);

            foreach (var p in world.Squad(clubId))
            {
                float ca = GameWorld.Ca(p);
                int flags = (int)p.positionFlags;
                for (int b = 0; b < Bits && flags != 0; b++)
                {
                    if ((flags & (1 << b)) == 0) continue;
                    if (ca > peak[b]) peak[b] = ca;
                    flags &= ~(1 << b);
                }
            }
        }

        /// <summary>그 자리를 소화하는 선수 중 가장 높은 CA. 없으면 0.</summary>
        public float BestCaAt(int clubId, Position pos)
        {
            if (!byClub.TryGetValue(clubId, out var peak))
                return world.BestCaAt(clubId, pos);

            float best = 0f;
            int flags = (int)pos;
            for (int b = 0; b < Bits && flags != 0; b++)
            {
                if ((flags & (1 << b)) == 0) continue;
                if (peak[b] > best) best = peak[b];
                flags &= ~(1 << b);
            }
            return best;
        }
    }
}
