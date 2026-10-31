using System;

namespace FootballTactician.World
{
    /// <summary>
    /// 시드 섞기. 한 세계 안에서 용도마다 독립된 난수를 쓰기 위한 것이다 —
    /// 같은 <see cref="Random"/> 을 돌려쓰면 한쪽의 추첨 횟수가 다른 쪽의 흐름을 밀어내,
    /// 플레이어의 훈련 선택이 맞붙지도 않는 구단의 성장까지 바꾼다.
    ///
    /// 진행 엔진 안에 있던 함수다. 기자실·컵처럼 엔진이 아닌 곳에서도 필요해서 떼어 냈다 —
    /// 엔진 안에 두면 그 모듈들이 엔진을 되참조하고, 참조가 양방향이 된다.
    /// </summary>
    public static class SeedMix
    {
        /// <summary>세계 시드 · 용도 키 · 소금을 섞어 음수 아닌 시드를 만든다.</summary>
        public static int Of(int worldSeed, int key, int salt)
        {
            unchecked
            {
                int h = worldSeed * 486187739 + key * 31 + salt;
                return h == -1 ? 7 : Math.Abs(h);
            }
        }
    }
}
