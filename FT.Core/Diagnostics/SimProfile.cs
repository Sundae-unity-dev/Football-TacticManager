using System;
using System.Collections.Generic;
using System.Diagnostics;

namespace FootballTactician.Diagnostics
{
    /// <summary>
    /// 이 파일은 어느 계층에도 속하지 않는다. 진행 코드(World)와 경기 엔진(Simulation)이
    /// 모두 구간을 찍으므로, World 에 두면 Simulation 이 World 를 참조하게 된다.
    /// 그래서 Diagnostics 로 떼어 두고, 아무것도 참조하지 않는다.
    /// </summary>
    /// <summary>
    /// 켜 두면 진행 경로의 구간별 시간을 모은다. 꺼져 있으면 bool 검사 하나뿐이고
    /// 할당도 없다(<see cref="Span"/> 이 구조체이고 꺼진 상태는 default).
    ///
    /// 측정은 반드시 게임이 실제로 지나는 경로에서 해야 한다. 하네스가 같은 순서를
    /// 베껴 재면 그 베낀 것만 빠르게 만들고 끝난다 — 그래서 구간 표시를
    /// 측정 코드가 아니라 진행 코드 안에 둔다.
    ///
    /// 합친 수치가 두 번 빗나가면 쪼갠다. 쪼개기 전에 고치지 않는다.
    /// </summary>
    public static class SimProfile
    {
        public static bool Enabled;

        static readonly Stopwatch clock = Stopwatch.StartNew();
        static readonly Dictionary<string, long> ticks = new();
        static readonly Dictionary<string, int> hits = new();
        static readonly List<string> order = new();

        /// <summary>구간 하나. <c>using</c> 으로 감싼다.</summary>
        public readonly struct Span : IDisposable
        {
            readonly string label;
            readonly long start;
            internal Span(string label) { this.label = label; start = clock.ElapsedTicks; }
            public void Dispose()
            {
                if (label == null) return;              // 꺼진 상태의 default
                Record(label, clock.ElapsedTicks - start, 1);
            }
        }

        public static Span Measure(string label) => Enabled ? new Span(label) : default;

        /// <summary>
        /// 시간 없이 개수만 센다. "몇 명을 훑었나" 는 구간 시간만큼 중요하다 —
        /// 느린 이유가 한 번이 비싼 것인지 너무 여러 번인지 가려 준다.
        /// </summary>
        public static void Tally(string label, int n)
        {
            if (Enabled) Record(label, 0, n);
        }

        static void Record(string label, long elapsed, int n)
        {
            if (!ticks.TryGetValue(label, out long had)) order.Add(label);
            ticks[label] = had + elapsed;
            hits[label] = hits.TryGetValue(label, out int k) ? k + n : n;
        }

        public static void Reset()
        {
            ticks.Clear();
            hits.Clear();
            order.Clear();
        }

        /// <summary>등장 순서대로 (이름, 밀리초, 횟수).</summary>
        public static IEnumerable<(string label, double ms, int count)> Results()
        {
            foreach (string label in order)
                yield return (label, ticks[label] * 1000.0 / Stopwatch.Frequency, hits[label]);
        }

        public static double MsOf(string label)
            => ticks.TryGetValue(label, out long t) ? t * 1000.0 / Stopwatch.Frequency : 0;
    }
}
