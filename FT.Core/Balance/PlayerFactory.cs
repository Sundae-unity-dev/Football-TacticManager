using System;
using FootballTactician.Data;

namespace FootballTactician.Balance
{
    /// <summary>
    /// 절차 생성. PA 를 먼저 뽑고 나이별 실현율로 CA 를 유도한다.
    /// CA(1~200) 는 CaToPower 로 능력치 스케일(1~20)로 환산해 각 항목에 분배한다.
    /// </summary>
    public static class PlayerFactory
    {
        /// <summary>
        /// 평판 구간 하나의 PA 분포. 값이 정해져 있고 바뀌지 않으므로 구조체로 둔다 —
        /// 전에는 선수를 만들 때마다 객체를 새로 만들어 세계 하나에 6,000개가 생기고 버려졌다.
        /// </summary>
        public readonly struct Spec
        {
            public readonly float StarterPaMin, StarterPaMax, RotationPaMin, RotationPaMax;

            public Spec(float starterMin, float starterMax, float rotationMin, float rotationMax)
            {
                StarterPaMin = starterMin;
                StarterPaMax = starterMax;
                RotationPaMin = rotationMin;
                RotationPaMax = rotationMax;
            }
        }

        /// <summary>
        /// 국가별 이름 재료. 세계 생성 쪽에서 넘겨준다.
        /// null 이면 내부 기본 풀을 쓰므로 기존 테스트와 측정 하네스는 그대로 돈다.
        /// </summary>
        public sealed class Naming
        {
            public string[] First;
            public string[] Last;
            public Nationality Nationality;
        }

        /// <summary>리그 평판별 PA 분포. 측정으로 확정한 값.</summary>
        public static Spec SpecFor(float reputation)
        {
            if (reputation >= 1.30f) return new Spec(150, 190, 130, 165);
            if (reputation >= 1.10f) return new Spec(140, 180, 120, 155);
            if (reputation >= 0.95f) return new Spec(130, 170, 112, 145);
            if (reputation >= 0.85f) return new Spec(122, 158, 105, 135);
            if (reputation >= 0.75f) return new Spec(112, 148, 98, 128);
            // 하부 리그 구간. 이 밴드가 없으면 2부 구단이 1부 하위권과 전력이 같아져 승강이 의미를 잃는다.
            if (reputation >= 0.68f) return new Spec(104, 138, 92, 120);
            return new Spec(96, 128, 84, 112);
        }

        static float Range(Random r, float lo, float hi) => lo + (float)r.NextDouble() * (hi - lo);

        public static PlayerData Create(
            Random rng, int id, int clubId, float reputation,
            Position position, int age, bool starter, GameDate today,
            Naming naming = null)
        {
            var spec = SpecFor(reputation);
            float pa = starter
                ? Range(rng, spec.StarterPaMin, spec.StarterPaMax)
                : Range(rng, spec.RotationPaMin, spec.RotationPaMax);

            float ca = pa * BalanceConfig.Realized(age) * Range(rng, 0.94f, 1.04f);
            if (ca > pa) ca = pa;
            if (ca < 20f) ca = 20f;

            var p = new PlayerData
            {
                id = id,
                clubId = clubId,
                firstName   = naming != null ? naming.First[rng.Next(naming.First.Length)] : NameBank.First(rng),
                lastName    = naming != null ? naming.Last[rng.Next(naming.Last.Length)]   : NameBank.Last(rng),
                nationality = naming?.Nationality ?? Nationality.NOR,
                dateOfBirth = new GameDate { year = today.year - age, month = 1 + rng.Next(12), day = 1 + rng.Next(28) },
                positionFlags = position,
                preferredPosition = position,
                preferredFoot = rng.Next(4) == 0 ? PreferredFoot.Left : PreferredFoot.Right,
                height = 170 + rng.Next(22),
                weight = 65 + rng.Next(20),
                technical = new TechnicalAttributes(),
                physical = new PhysicalAttributes(),
                mental = new MentalAttributes(),
                goalkeeper = new GoalkeeperAttributes(),
                hidden = new HiddenAttributes(),
                morale = 60f + (float)rng.NextDouble() * 20f,
                condition = 90f + (float)rng.NextDouble() * 10f,
                potential = (int)Math.Clamp(pa / 2f, 1, 99),   // 기존 1~99 필드 호환
                seasonStats = new PlayerStats(),
            };

            ApplyAbility(p, ca, position, rng);
            FillHidden(p.hidden, rng);
            p.InvalidateAbilityCache();
            return p;
        }

        /// <summary>CA(1~200) 를 능력치(1~20) 로 환산해 포지션 가중치대로 분배한다.</summary>
        public static void ApplyAbility(PlayerData p, float ca, Position position, Random rng)
        {
            float baseLevel = BalanceConfig.CaToPower(ca);            // 1~20 스케일
            bool gk = (position & Position.GK) != 0;

            int J(float weight)
            {
                float v = baseLevel * weight + Range(rng, -1.2f, 1.2f);
                return (int)Math.Clamp(MathF.Round(v), 1, 20);
            }

            // 포지션별 강조 — 합이 아니라 항목별 가중치
            float att = (position & (Position.ST | Position.CF | Position.CAM | Position.LW | Position.RW)) != 0 ? 1.08f : 0.92f;
            float def = (position & (Position.CB | Position.LB | Position.RB | Position.CDM)) != 0 ? 1.08f : 0.92f;

            var t = p.technical;
            t.passing = J(1.0f); t.dribbling = J(att); t.shooting = J(att); t.finishing = J(att);
            t.heading = J(def); t.crossing = J(1.0f); t.tackling = J(def); t.marking = J(def);
            t.firstTouch = J(1.0f); t.longPassing = J(1.0f); t.setPieces = J(0.95f);

            var ph = p.physical;
            ph.speed = J(1.0f); ph.acceleration = J(1.0f); ph.stamina = J(1.0f); ph.strength = J(1.0f);
            ph.jumping = J(def); ph.agility = J(1.0f); ph.balance = J(1.0f);

            var m = p.mental;
            m.vision = J(1.0f); m.composure = J(1.0f); m.leadership = J(0.9f); m.courage = J(1.0f);
            m.workRate = J(1.0f); m.concentration = J(1.0f); m.decisions = J(1.0f);
            m.positioning = J(def); m.aggression = J(0.95f); m.teamwork = J(1.0f); m.offTheBall = J(att);

            var g = p.goalkeeper;
            float gw = gk ? 1.0f : 0.35f;
            g.handling = J(gw); g.diving = J(gw); g.reflexes = J(gw); g.positioning = J(gw);
            g.communication = J(gw); g.kicking = J(gw); g.throwing = J(gw);
            g.oneOnOne = J(gw); g.aerialAbility = J(gw); g.commandArea = J(gw);

            p.InvalidateAbilityCache();
        }

        static void FillHidden(HiddenAttributes h, Random r)
        {
            int V() => 5 + r.Next(14);
            h.consistency = V(); h.importantMatches = V(); h.pressureHandling = V();
            h.injuryProneness = V(); h.recoveryRate = V(); h.professionalism = V();
            h.ambition = V(); h.loyalty = V(); h.temperament = V(); h.sportsmanship = V();
            h.dirtiness = V(); h.adaptability = V(); h.versatility = V();
            h.controversy = V(); h.influence = V();
        }
    }

    internal static class NameBank
    {
        static readonly string[] F = {
            "Jon","Mikal","Aren","Sven","Toma","Erik","Pavl","Nilo","Kasp","Rune",
            "Dario","Lenn","Mats","Olav","Veit","Juho","Rasm","Teo","Anso","Ivar" };
        static readonly string[] L = {
            "Vestre","Halden","Korval","Brenn","Saros","Maltin","Doren","Kvist","Agren","Falk",
            "Norrby","Estrand","Velmar","Karlen","Solvik","Draven","Merlo","Calder","Rhun","Osk" };
        public static string First(Random r) => F[r.Next(F.Length)];
        public static string Last(Random r) => L[r.Next(L.Length)];
    }
}
