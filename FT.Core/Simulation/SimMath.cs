namespace FootballTactician.Simulation
{
    /// <summary>
    /// 시뮬레이션 시스템 공통 수학 유틸. UnityEngine.Mathf 의존을 피해 단위 테스트 가능하도록 분리.
    /// </summary>
    internal static class SimMath
    {
        public static int   Clamp(int   v, int   min, int   max) => v < min ? min : v > max ? max : v;
        public static float Clamp(float v, float min, float max) => v < min ? min : v > max ? max : v;
        public static float Clamp01(float v)                     => v < 0f ? 0f : v > 1f ? 1f : v;
    }
}
