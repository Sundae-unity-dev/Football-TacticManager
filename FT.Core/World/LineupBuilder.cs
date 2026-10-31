using System.Collections.Generic;
using System.Linq;
using FootballTactician.Diagnostics;
using FootballTactician.Balance;
using FootballTactician.Data;

namespace FootballTactician.World
{
    /// <summary>
    /// 선발 11인 배치. 게임·측정 하네스·회귀 테스트가 전부 이 함수를 쓴다.
    ///
    /// 전에는 하네스가 CA 순서대로 슬롯을 채웠다. 스트라이커가 센터백 자리에 서도 그대로 뒀다는 뜻이고,
    /// 그 라인업으로 잰 수치는 실제 경기와 맞지 않았다. 배치 규칙은 여기 한 곳에만 둔다.
    /// </summary>
    public static class LineupBuilder
    {
        /// <summary>본 포지션 보너스.</summary>
        const float ExactBonus = 60f;
        /// <summary>선호 포지션 보너스.</summary>
        const float PreferredBonus = 40f;
        /// <summary>윙어와 윙 미드처럼 서로 소화 가능한 자리의 보너스.</summary>
        const float RelatedBonus = 26f;

        /// <summary>자리 소화 정도. 화면 표기와 배치 계산이 같은 판정을 쓰도록 한 곳에 둔다.</summary>
        public enum FitTier { Impossible, Unfamiliar, Related, Natural }

        public static FitTier Tier(PlayerData p, Position slot)
        {
            bool gkSlot = (slot & Position.GK) != 0;
            bool gkPlayer = (p.positionFlags & Position.GK) != 0;
            if (gkSlot != gkPlayer) return FitTier.Impossible;   // 골키퍼와 필드 선수는 섞지 않는다

            if ((p.positionFlags & slot) != 0) return FitTier.Natural;
            if ((p.preferredPosition & slot) != 0) return FitTier.Natural;
            if ((p.positionFlags & Related(slot)) != 0) return FitTier.Related;
            return FitTier.Unfamiliar;
        }

        /// <summary>
        /// 슬롯 적합도. 본 포지션이면 크게 가산하고, 인접 포지션이면 조금 가산한다.
        /// 포메이션이 LM/RM 을 쓰는데 스쿼드에 LW/RW 밖에 없는 경우가 흔하므로 인접을 봐야 한다.
        /// </summary>
        public static float Fit(PlayerData p, Position slot)
            => Fit(p, BalanceConfig.PowerToCa(p.AbilityScore), slot);

        /// <summary>
        /// CA 를 이미 구해 둔 경우의 적합도. 적합도의 CA 항은 자리와 무관하다 —
        /// 한 선수를 열한 자리에 견주는 동안 열한 번 다시 구할 이유가 없다.
        /// 값은 <see cref="Fit(PlayerData, Position)"/> 와 같다.
        /// </summary>
        internal static float Fit(PlayerData p, float ca, Position slot)
            => Fit(p, ca, slot, Related(slot));

        /// <summary>
        /// 인접 자리 마스크까지 미리 받은 경우. 한 자리에 스물다섯 명을 견주는 동안
        /// 같은 마스크를 스물다섯 번 다시 만들 이유가 없다.
        /// 판정도 한 함수에서 끝낸다 — <see cref="Tier"/> 와 분기 순서가 같아 값이 같다.
        /// </summary>
        internal static float Fit(PlayerData p, float ca, Position slot, Position related)
        {
            var flags = p.positionFlags;

            // 골키퍼와 필드 선수는 섞지 않는다
            if (((slot & Position.GK) != 0) != ((flags & Position.GK) != 0)) return -1000f;

            if ((flags & slot) != 0) return ca + ExactBonus;
            if ((p.preferredPosition & slot) != 0) return ca + PreferredBonus;
            if ((flags & related) != 0) return ca + RelatedBonus;
            return ca;
        }

        /// <summary>해당 자리를 무리 없이 소화하는 인접 포지션들.</summary>
        static Position Related(Position slot)
        {
            if ((slot & (Position.LM | Position.LW)) != 0) return Position.LM | Position.LW | Position.LWB;
            if ((slot & (Position.RM | Position.RW)) != 0) return Position.RM | Position.RW | Position.RWB;
            if ((slot & (Position.LB | Position.LWB)) != 0) return Position.LB | Position.LWB | Position.LM;
            if ((slot & (Position.RB | Position.RWB)) != 0) return Position.RB | Position.RWB | Position.RM;
            if ((slot & Position.CDM) != 0) return Position.CM | Position.CB;
            if ((slot & Position.CM) != 0) return Position.CDM | Position.CAM;
            if ((slot & Position.CAM) != 0) return Position.CM | Position.CF | Position.LW | Position.RW;
            if ((slot & (Position.ST | Position.CF)) != 0) return Position.ST | Position.CF | Position.CAM | Position.LW | Position.RW;
            if ((slot & Position.CB) != 0) return Position.CDM | Position.LB | Position.RB;
            return Position.None;
        }

        /// <summary>
        /// 전술의 슬롯에 선수를 채운다. 부상자는 제외한다.
        ///
        /// 슬롯 순서대로 탐욕적으로 채우면 앞쪽 슬롯이 좋은 선수를 먼저 가져가, 스트라이커가
        /// 윙 자리에 서고 최전방에는 유망주가 남는 일이 생긴다.
        /// 그래서 (1) 소화 가능한 선수가 적은 자리부터 채우고, (2) 둘씩 바꿔 전체 적합도가
        /// 올라가면 바꾸는 개선 과정을 둔다.
        /// </summary>
        public static void Assign(TacticsData tactics, IEnumerable<PlayerData> squad)
        {
            if (tactics == null) return;

            // 명단과 자리는 LINQ 없이 모은다. 한 라운드에 1만 번 넘게 지나가는 자리다.
            var pool = new List<PlayerData>(24);
            foreach (var p in squad)
                if (p != null && !p.IsInjured) pool.Add(p);

            // 자리 번호 순. 삽입 정렬이라 번호가 같으면 원래 순서를 지킨다(OrderBy 와 같은 규칙).
            var slots = new List<FormationSlot>(tactics.slots.Count);
            foreach (var s in tactics.slots)
            {
                int put = slots.Count;
                while (put > 0 && slots[put - 1].slotIndex > s.slotIndex) put--;
                slots.Insert(put, s);
            }

            if (pool.Count == 0 || slots.Count == 0) return;

            // 선수마다 CA 와 기량을 한 번만 구해 돌려쓴다. 적합도의 CA 항은 자리와 무관한데,
            // 전에는 Fit 안에서 자리마다 다시 구해 한 라인업에 700번이 넘었다.
            var ca = new float[pool.Count];
            var ability = new float[pool.Count];
            for (int k = 0; k < pool.Count; k++)
            {
                ability[k] = pool[k].AbilityScore;
                ca[k] = BalanceConfig.PowerToCa(ability[k]);
            }

            // 1) 희소한 자리부터 — 본 포지션 선수가 적은 슬롯이 먼저 고른다.
            //    세는 것은 비트 검사뿐이라 싸다. 비싼 쪽은 자리 열한 개를 LINQ 로 정렬하며
            //    자리마다 명단을 IEnumerable 로 다시 훑던 것이었다.
            //    삽입 정렬이라 동점인 자리끼리는 원래 순서를 지킨다(OrderBy 와 같은 규칙).
            var scarcity = new int[slots.Count];
            for (int i = 0; i < slots.Count; i++)
            {
                int n = 0;
                var pos = slots[i].position;
                for (int k = 0; k < pool.Count; k++)
                    if ((pool[k].positionFlags & pos) != 0) n++;
                scarcity[i] = n;
            }

            var order = new int[slots.Count];
            for (int i = 0; i < slots.Count; i++)
            {
                int j = i;
                while (j > 0 && Later(order[j - 1], i))
                {
                    order[j] = order[j - 1];
                    j--;
                }
                order[j] = i;
            }

            // a 가 b 보다 뒤에 와야 하는가 — 희소한 자리가 먼저, 같으면 골키퍼가 먼저.
            bool Later(int a, int b)
                => scarcity[a] > scarcity[b]
                   || (scarcity[a] == scarcity[b] && Gk(a) < Gk(b));

            int Gk(int i) => (slots[i].position & Position.GK) != 0 ? 1 : 0;

            // 자리마다 한 명을 고르려고 22명을 정렬하던 곳이다. 라운드마다 수천 번 불리므로
            // 한 번 훑어 최고를 집는다 — 내림차순 정렬의 첫 번째와 같은 값이다.
            // 동점이면 먼저 오는 쪽이 이기도록 '더 클 때만' 바꾼다(안정 정렬과 같은 규칙).
            // 인접 자리 마스크는 자리마다 하나다. 명단을 훑는 동안 다시 만들지 않는다.
            var related = new Position[slots.Count];
            for (int i = 0; i < slots.Count; i++) related[i] = Related(slots[i].position);

            var taken = new bool[pool.Count];
            var at = new int[slots.Count];

            using (SimProfile.Measure("      · 자리 채우기"))
            foreach (int oi in order)
            {
                var slot = slots[oi];
                int best = -1;
                float bestFit = 0f, bestAbility = 0f;

                for (int k = 0; k < pool.Count; k++)
                {
                    if (taken[k]) continue;

                    float fit = Fit(pool[k], ca[k], slot.position, related[oi]);
                    if (best >= 0 && !(fit > bestFit || (fit == bestFit && ability[k] > bestAbility)))
                        continue;

                    best = k;
                    bestFit = fit;
                    bestAbility = ability[k];
                }

                at[oi] = best;
                slot.assignedPlayerId = best < 0 ? -1 : pool[best].id;
                if (best >= 0) taken[best] = true;
            }

            using (SimProfile.Measure("      · 맞바꿔 개선"))
                Improve(slots, pool, ca, at, related);
        }

        /// <summary>두 자리를 맞바꿔 합계 적합도가 오르면 바꾼다. 11자리라 몇 바퀴면 수렴한다.</summary>
        static void Improve(List<FormationSlot> slots, List<PlayerData> pool, float[] ca, int[] at,
                            Position[] related)
        {
            // 자리에 앉은 선수를 명단 번호로 들고 다닌다. 번호를 들고 있으면 CA 를
            // 그대로 돌려쓸 수 있고, 배치 단계가 이미 알고 있던 것을 다시 찾지 않아도 된다.
            // 전에는 패스마다 선수 번호로 명단을 훑고 적합도를 네 번씩 다시 구했다 —
            // 맞바꿔 보는 짝이 55개이고 패스가 4번이라 한 라인업에 880번이었다.
            var fitNow = new float[slots.Count];

            for (int i = 0; i < slots.Count; i++)
                if (at[i] >= 0)
                    fitNow[i] = Fit(pool[at[i]], ca[at[i]], slots[i].position, related[i]);

            for (int pass = 0; pass < 4; pass++)
            {
                bool changed = false;

                for (int i = 0; i < slots.Count; i++)
                    for (int j = i + 1; j < slots.Count; j++)
                    {
                        int a = at[i];
                        int b = at[j];
                        if (a < 0 || b < 0) continue;

                        float toJ = Fit(pool[a], ca[a], slots[j].position, related[j]);
                        float toI = Fit(pool[b], ca[b], slots[i].position, related[i]);

                        if (toI + toJ > fitNow[i] + fitNow[j] + 0.01f)
                        {
                            (at[i], at[j]) = (b, a);
                            (fitNow[i], fitNow[j]) = (toI, toJ);
                            changed = true;
                        }
                    }

                if (!changed) break;
            }

            for (int i = 0; i < slots.Count; i++)
                if (at[i] >= 0) slots[i].assignedPlayerId = pool[at[i]].id;
        }

        /// <summary>선발에 들지 않은 선수 중 기량 순으로 벤치를 채운다.</summary>
        public static List<int> Bench(TacticsData tactics, IEnumerable<PlayerData> squad, int size = 7)
        {
            // 25명을 정렬해 일곱 명을 떼던 자리. 경기마다 두 번 불린다.
            // 일곱 칸 목록에 끼워 넣으며 한 번 훑으면 같은 일곱 명이 같은 순서로 남는다 —
            // 기량이 같으면 명단에서 앞선 쪽이 앞이어야 하므로 '더 클 때만' 앞으로 끼운다.
            int slotCount = tactics.slots.Count;
            var starters = new int[slotCount];
            for (int i = 0; i < slotCount; i++) starters[i] = tactics.slots[i].assignedPlayerId;

            var top = new List<PlayerData>(size + 1);
            var score = new List<float>(size + 1);

            foreach (var p in squad)
            {
                if (p == null || p.IsInjured) continue;

                bool starter = false;
                for (int i = 0; i < slotCount; i++)
                    if (starters[i] == p.id) { starter = true; break; }
                if (starter) continue;

                float a = p.AbilityScore;
                int at = top.Count;
                while (at > 0 && a > score[at - 1]) at--;
                if (at >= size) continue;

                top.Insert(at, p);
                score.Insert(at, a);
                if (top.Count > size) { top.RemoveAt(size); score.RemoveAt(size); }
            }

            var ids = new List<int>(top.Count);
            foreach (var p in top) ids.Add(p.id);
            return ids;
        }
    }
}
