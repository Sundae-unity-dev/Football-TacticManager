using System;
using System.Collections.Generic;

namespace FootballTactician.Data
{
    // ── 프리셋 데이터 클래스 ───────────────────────────────────────────────────

    [Serializable]
    public class FormationPreset
    {
        public string id;           // 고유 키 ("4-4-2", "4-2-3-1", ...)
        public string displayName;  // UI 표시명 ("4-4-2 플랫", "4-4-2 다이아몬드", ...)
        public string shape;        // 숫자 형태 표시용 ("4-4-2", "4-3-3", ...)
        public List<FormationSlotTemplate> slots;
    }

    [Serializable]
    public class FormationSlotTemplate
    {
        public int      slotIndex;
        public Position defaultPosition;
        public float    attackingX, attackingY;
        public float    defendingX, defendingY;
    }

    // ── 포메이션 데이터베이스 ─────────────────────────────────────────────────

    public static class FormationDatabase
    {
        private static readonly Dictionary<string, FormationPreset> _all;

        static FormationDatabase()
        {
            _all = new Dictionary<string, FormationPreset>();
            Register(Make_442_Flat());
            Register(Make_442_Diamond());
            Register(Make_433());
            Register(Make_433_Attack());
            Register(Make_4231());
            Register(Make_451());
            Register(Make_4141());
            Register(Make_4321());
            Register(Make_352());
            Register(Make_343());
            Register(Make_532());
            Register(Make_541());
        }

        // ── 공개 API ─────────────────────────────────────────────────────────

        public static FormationPreset Get(string id)
            => _all.TryGetValue(id, out var p) ? p : null;

        public static IReadOnlyCollection<FormationPreset> All => _all.Values;

        /// <summary>
        /// 프리셋을 기반으로 새 TacticsData를 만든다.
        /// 선수 배정·역할·개별 지시사항은 빈 상태로 반환 — 감독이 직접 채운다.
        /// </summary>
        public static TacticsData CreateTactics(string presetId, string name)
        {
            var preset = Get(presetId);
            if (preset == null) return null;

            var tactics = new TacticsData
            {
                name          = name,
                formationShape = preset.shape,
            };

            foreach (var t in preset.slots)
            {
                tactics.slots.Add(new FormationSlot
                {
                    slotIndex        = t.slotIndex,
                    position         = t.defaultPosition,
                    assignedPlayerId = -1,
                    attackingX       = t.attackingX,
                    attackingY       = t.attackingY,
                    defendingX       = t.defendingX,
                    defendingY       = t.defendingY,
                });
            }

            return tactics;
        }

        // ─────────────────────────────────────────────────────────────────────

        private static void Register(FormationPreset p) => _all[p.id] = p;

        // 슬롯 생성 단축 헬퍼
        private static FormationSlotTemplate S(int idx, Position pos,
            float ax, float ay, float dx, float dy)
            => new FormationSlotTemplate
            {
                slotIndex = idx, defaultPosition = pos,
                attackingX = ax, attackingY = ay,
                defendingX = dx, defendingY = dy,
            };

        // ── 포메이션 정의 ────────────────────────────────────────────────────
        // 좌표계: x(0=왼쪽~1=오른쪽), y(0=자기골문~1=상대골문)
        // 공격/수비 좌표 분리 — 수비 시 전체적으로 y가 낮아지고 폭이 좁아짐

        private static FormationPreset Make_442_Flat() => new FormationPreset
        {
            id = "4-4-2", displayName = "4-4-2 플랫", shape = "4-4-2",
            slots = new List<FormationSlotTemplate>
            {
                S( 0, Position.GK,  0.50f, 0.05f,  0.50f, 0.05f),
                S( 1, Position.RB,  0.85f, 0.22f,  0.85f, 0.16f),
                S( 2, Position.CB,  0.62f, 0.17f,  0.62f, 0.13f),
                S( 3, Position.CB,  0.38f, 0.17f,  0.38f, 0.13f),
                S( 4, Position.LB,  0.15f, 0.22f,  0.15f, 0.16f),
                S( 5, Position.RM,  0.88f, 0.50f,  0.85f, 0.38f),
                S( 6, Position.CM,  0.62f, 0.47f,  0.60f, 0.35f),
                S( 7, Position.CM,  0.38f, 0.47f,  0.40f, 0.35f),
                S( 8, Position.LM,  0.12f, 0.50f,  0.15f, 0.38f),
                S( 9, Position.ST,  0.62f, 0.80f,  0.60f, 0.62f),
                S(10, Position.ST,  0.38f, 0.80f,  0.40f, 0.62f),
            }
        };

        private static FormationPreset Make_442_Diamond() => new FormationPreset
        {
            id = "4-4-2-diamond", displayName = "4-4-2 다이아몬드", shape = "4-4-2",
            slots = new List<FormationSlotTemplate>
            {
                S( 0, Position.GK,  0.50f, 0.05f,  0.50f, 0.05f),
                S( 1, Position.RB,  0.85f, 0.22f,  0.85f, 0.16f),
                S( 2, Position.CB,  0.62f, 0.17f,  0.62f, 0.13f),
                S( 3, Position.CB,  0.38f, 0.17f,  0.38f, 0.13f),
                S( 4, Position.LB,  0.15f, 0.22f,  0.15f, 0.16f),
                S( 5, Position.CDM, 0.50f, 0.38f,  0.50f, 0.30f),
                S( 6, Position.CM,  0.75f, 0.52f,  0.72f, 0.42f),
                S( 7, Position.CM,  0.25f, 0.52f,  0.28f, 0.42f),
                S( 8, Position.CAM, 0.50f, 0.65f,  0.50f, 0.53f),
                S( 9, Position.ST,  0.62f, 0.82f,  0.60f, 0.65f),
                S(10, Position.ST,  0.38f, 0.82f,  0.40f, 0.65f),
            }
        };

        private static FormationPreset Make_433() => new FormationPreset
        {
            id = "4-3-3", displayName = "4-3-3", shape = "4-3-3",
            slots = new List<FormationSlotTemplate>
            {
                S( 0, Position.GK,  0.50f, 0.05f,  0.50f, 0.05f),
                S( 1, Position.RB,  0.85f, 0.22f,  0.85f, 0.16f),
                S( 2, Position.CB,  0.62f, 0.17f,  0.62f, 0.13f),
                S( 3, Position.CB,  0.38f, 0.17f,  0.38f, 0.13f),
                S( 4, Position.LB,  0.15f, 0.22f,  0.15f, 0.16f),
                S( 5, Position.CM,  0.70f, 0.50f,  0.68f, 0.38f),
                S( 6, Position.CM,  0.50f, 0.47f,  0.50f, 0.35f),
                S( 7, Position.CM,  0.30f, 0.50f,  0.32f, 0.38f),
                S( 8, Position.RW,  0.85f, 0.75f,  0.82f, 0.58f),
                S( 9, Position.ST,  0.50f, 0.82f,  0.50f, 0.65f),
                S(10, Position.LW,  0.15f, 0.75f,  0.18f, 0.58f),
            }
        };

        private static FormationPreset Make_433_Attack() => new FormationPreset
        {
            id = "4-3-3-attack", displayName = "4-3-3 공격형", shape = "4-3-3",
            slots = new List<FormationSlotTemplate>
            {
                S( 0, Position.GK,  0.50f, 0.05f,  0.50f, 0.05f),
                S( 1, Position.RB,  0.85f, 0.22f,  0.85f, 0.16f),
                S( 2, Position.CB,  0.62f, 0.17f,  0.62f, 0.13f),
                S( 3, Position.CB,  0.38f, 0.17f,  0.38f, 0.13f),
                S( 4, Position.LB,  0.15f, 0.22f,  0.15f, 0.16f),
                S( 5, Position.CDM, 0.62f, 0.38f,  0.62f, 0.30f),
                S( 6, Position.CM,  0.50f, 0.52f,  0.50f, 0.40f),
                S( 7, Position.CDM, 0.38f, 0.38f,  0.38f, 0.30f),
                S( 8, Position.RW,  0.88f, 0.77f,  0.85f, 0.60f),
                S( 9, Position.ST,  0.50f, 0.83f,  0.50f, 0.67f),
                S(10, Position.LW,  0.12f, 0.77f,  0.15f, 0.60f),
            }
        };

        private static FormationPreset Make_4231() => new FormationPreset
        {
            id = "4-2-3-1", displayName = "4-2-3-1", shape = "4-2-3-1",
            slots = new List<FormationSlotTemplate>
            {
                S( 0, Position.GK,  0.50f, 0.05f,  0.50f, 0.05f),
                S( 1, Position.RB,  0.85f, 0.22f,  0.85f, 0.16f),
                S( 2, Position.CB,  0.62f, 0.17f,  0.62f, 0.13f),
                S( 3, Position.CB,  0.38f, 0.17f,  0.38f, 0.13f),
                S( 4, Position.LB,  0.15f, 0.22f,  0.15f, 0.16f),
                S( 5, Position.CDM, 0.62f, 0.37f,  0.62f, 0.30f),
                S( 6, Position.CDM, 0.38f, 0.37f,  0.38f, 0.30f),
                S( 7, Position.RM,  0.87f, 0.60f,  0.85f, 0.46f),
                S( 8, Position.CAM, 0.50f, 0.63f,  0.50f, 0.50f),
                S( 9, Position.LM,  0.13f, 0.60f,  0.15f, 0.46f),
                S(10, Position.ST,  0.50f, 0.82f,  0.50f, 0.65f),
            }
        };

        private static FormationPreset Make_451() => new FormationPreset
        {
            id = "4-5-1", displayName = "4-5-1", shape = "4-5-1",
            slots = new List<FormationSlotTemplate>
            {
                S( 0, Position.GK,  0.50f, 0.05f,  0.50f, 0.05f),
                S( 1, Position.RB,  0.85f, 0.22f,  0.85f, 0.16f),
                S( 2, Position.CB,  0.62f, 0.17f,  0.62f, 0.13f),
                S( 3, Position.CB,  0.38f, 0.17f,  0.38f, 0.13f),
                S( 4, Position.LB,  0.15f, 0.22f,  0.15f, 0.16f),
                S( 5, Position.RM,  0.90f, 0.48f,  0.88f, 0.36f),
                S( 6, Position.CM,  0.70f, 0.45f,  0.68f, 0.33f),
                S( 7, Position.CM,  0.50f, 0.43f,  0.50f, 0.32f),
                S( 8, Position.CM,  0.30f, 0.45f,  0.32f, 0.33f),
                S( 9, Position.LM,  0.10f, 0.48f,  0.12f, 0.36f),
                S(10, Position.ST,  0.50f, 0.80f,  0.50f, 0.63f),
            }
        };

        private static FormationPreset Make_4141() => new FormationPreset
        {
            id = "4-1-4-1", displayName = "4-1-4-1", shape = "4-1-4-1",
            slots = new List<FormationSlotTemplate>
            {
                S( 0, Position.GK,  0.50f, 0.05f,  0.50f, 0.05f),
                S( 1, Position.RB,  0.85f, 0.22f,  0.85f, 0.16f),
                S( 2, Position.CB,  0.62f, 0.17f,  0.62f, 0.13f),
                S( 3, Position.CB,  0.38f, 0.17f,  0.38f, 0.13f),
                S( 4, Position.LB,  0.15f, 0.22f,  0.15f, 0.16f),
                S( 5, Position.CDM, 0.50f, 0.35f,  0.50f, 0.28f),
                S( 6, Position.RM,  0.88f, 0.55f,  0.86f, 0.43f),
                S( 7, Position.CM,  0.65f, 0.52f,  0.63f, 0.40f),
                S( 8, Position.CM,  0.35f, 0.52f,  0.37f, 0.40f),
                S( 9, Position.LM,  0.12f, 0.55f,  0.14f, 0.43f),
                S(10, Position.ST,  0.50f, 0.80f,  0.50f, 0.63f),
            }
        };

        private static FormationPreset Make_4321() => new FormationPreset
        {
            id = "4-3-2-1", displayName = "4-3-2-1 크리스마스 트리", shape = "4-3-2-1",
            slots = new List<FormationSlotTemplate>
            {
                S( 0, Position.GK,  0.50f, 0.05f,  0.50f, 0.05f),
                S( 1, Position.RB,  0.85f, 0.22f,  0.85f, 0.16f),
                S( 2, Position.CB,  0.62f, 0.17f,  0.62f, 0.13f),
                S( 3, Position.CB,  0.38f, 0.17f,  0.38f, 0.13f),
                S( 4, Position.LB,  0.15f, 0.22f,  0.15f, 0.16f),
                S( 5, Position.CM,  0.70f, 0.40f,  0.68f, 0.32f),
                S( 6, Position.CM,  0.50f, 0.38f,  0.50f, 0.30f),
                S( 7, Position.CM,  0.30f, 0.40f,  0.32f, 0.32f),
                S( 8, Position.CAM, 0.65f, 0.62f,  0.63f, 0.50f),
                S( 9, Position.CAM, 0.35f, 0.62f,  0.37f, 0.50f),
                S(10, Position.ST,  0.50f, 0.82f,  0.50f, 0.65f),
            }
        };

        private static FormationPreset Make_352() => new FormationPreset
        {
            id = "3-5-2", displayName = "3-5-2", shape = "3-5-2",
            slots = new List<FormationSlotTemplate>
            {
                S( 0, Position.GK,  0.50f, 0.05f,  0.50f, 0.05f),
                S( 1, Position.CB,  0.72f, 0.17f,  0.72f, 0.13f),
                S( 2, Position.CB,  0.50f, 0.13f,  0.50f, 0.10f),
                S( 3, Position.CB,  0.28f, 0.17f,  0.28f, 0.13f),
                S( 4, Position.RWB, 0.92f, 0.45f,  0.90f, 0.25f),
                S( 5, Position.CM,  0.68f, 0.43f,  0.66f, 0.35f),
                S( 6, Position.CM,  0.50f, 0.40f,  0.50f, 0.33f),
                S( 7, Position.CM,  0.32f, 0.43f,  0.34f, 0.35f),
                S( 8, Position.LWB, 0.08f, 0.45f,  0.10f, 0.25f),
                S( 9, Position.ST,  0.62f, 0.80f,  0.60f, 0.63f),
                S(10, Position.ST,  0.38f, 0.80f,  0.40f, 0.63f),
            }
        };

        private static FormationPreset Make_343() => new FormationPreset
        {
            id = "3-4-3", displayName = "3-4-3", shape = "3-4-3",
            slots = new List<FormationSlotTemplate>
            {
                S( 0, Position.GK,  0.50f, 0.05f,  0.50f, 0.05f),
                S( 1, Position.CB,  0.72f, 0.17f,  0.72f, 0.13f),
                S( 2, Position.CB,  0.50f, 0.13f,  0.50f, 0.10f),
                S( 3, Position.CB,  0.28f, 0.17f,  0.28f, 0.13f),
                S( 4, Position.RWB, 0.90f, 0.45f,  0.88f, 0.25f),
                S( 5, Position.CM,  0.65f, 0.45f,  0.63f, 0.35f),
                S( 6, Position.CM,  0.35f, 0.45f,  0.37f, 0.35f),
                S( 7, Position.LWB, 0.10f, 0.45f,  0.12f, 0.25f),
                S( 8, Position.RW,  0.82f, 0.75f,  0.80f, 0.58f),
                S( 9, Position.ST,  0.50f, 0.82f,  0.50f, 0.65f),
                S(10, Position.LW,  0.18f, 0.75f,  0.20f, 0.58f),
            }
        };

        private static FormationPreset Make_532() => new FormationPreset
        {
            id = "5-3-2", displayName = "5-3-2", shape = "5-3-2",
            slots = new List<FormationSlotTemplate>
            {
                S( 0, Position.GK,  0.50f, 0.05f,  0.50f, 0.05f),
                S( 1, Position.RWB, 0.92f, 0.30f,  0.90f, 0.20f),
                S( 2, Position.CB,  0.75f, 0.17f,  0.75f, 0.13f),
                S( 3, Position.CB,  0.50f, 0.13f,  0.50f, 0.10f),
                S( 4, Position.CB,  0.25f, 0.17f,  0.25f, 0.13f),
                S( 5, Position.LWB, 0.08f, 0.30f,  0.10f, 0.20f),
                S( 6, Position.CM,  0.67f, 0.50f,  0.65f, 0.38f),
                S( 7, Position.CM,  0.50f, 0.47f,  0.50f, 0.35f),
                S( 8, Position.CM,  0.33f, 0.50f,  0.35f, 0.38f),
                S( 9, Position.ST,  0.62f, 0.80f,  0.60f, 0.63f),
                S(10, Position.ST,  0.38f, 0.80f,  0.40f, 0.63f),
            }
        };

        private static FormationPreset Make_541() => new FormationPreset
        {
            id = "5-4-1", displayName = "5-4-1", shape = "5-4-1",
            slots = new List<FormationSlotTemplate>
            {
                S( 0, Position.GK,  0.50f, 0.05f,  0.50f, 0.05f),
                S( 1, Position.RWB, 0.92f, 0.30f,  0.90f, 0.20f),
                S( 2, Position.CB,  0.75f, 0.17f,  0.75f, 0.13f),
                S( 3, Position.CB,  0.50f, 0.13f,  0.50f, 0.10f),
                S( 4, Position.CB,  0.25f, 0.17f,  0.25f, 0.13f),
                S( 5, Position.LWB, 0.08f, 0.30f,  0.10f, 0.20f),
                S( 6, Position.RM,  0.87f, 0.52f,  0.85f, 0.38f),
                S( 7, Position.CM,  0.63f, 0.48f,  0.62f, 0.35f),
                S( 8, Position.CM,  0.37f, 0.48f,  0.38f, 0.35f),
                S( 9, Position.LM,  0.13f, 0.52f,  0.15f, 0.38f),
                S(10, Position.ST,  0.50f, 0.80f,  0.50f, 0.63f),
            }
        };
    }
}
