using System;
using System.Collections.Generic;
using System.Linq;
using FootballTactician.Balance;
using FootballTactician.Data;

namespace FootballTactician.Simulation
{
    /// <summary>
    /// 세트피스 — 코너, 직접 프리킥, 페널티.
    ///
    /// 핵심 원칙: <b>찬스를 더 주는 것이 아니라 일부를 대체한다.</b>
    /// 그냥 얹으면 리그 득점이 통째로 올라가 지금까지 맞춰 둔 수치가 전부 틀어진다.
    /// 공격 시도 중 일부가 열린 플레이 대신 세트피스로 끝난다.
    ///
    /// 그래서 생기는 것은 <b>새로운 축</b>이다 — 키 크고 힘센 선수와 좋은 키커를 모으면
    /// 열린 플레이가 약해도 득점할 길이 생긴다.
    /// </summary>
    public static class SetPieces
    {
        public enum Kind { Corner, FreeKick, Penalty }

        /// <summary>이번 공격이 세트피스로 끝나는지, 끝난다면 무엇인지.</summary>
        public static Kind? Roll(Random rng)
        {
            double r = rng.NextDouble();
            if (r < BalanceConfig.PenaltyShare) return Kind.Penalty;
            if (r < BalanceConfig.PenaltyShare + BalanceConfig.FreeKickShare) return Kind.FreeKick;
            if (r < BalanceConfig.SetPieceShare) return Kind.Corner;
            return null;
        }

        // ── 공중전 ────────────────────────────────────────────────────────

        /// <summary>
        /// 공중에서의 위협. 헤딩·점프·힘으로 본다.
        /// 박스 안에 들어가는 선수들만 센다 — 수비수도 코너에는 올라간다.
        /// </summary>
        public static float AerialThreat(PlayerData[] starters)
        {
            float best = 0f, sum = 0f;
            int n = 0;

            foreach (var p in starters)
            {
                if (p?.technical == null || p.physical == null) continue;
                if ((p.positionFlags & Position.GK) != 0) continue;

                float v = (p.technical.heading * 1.2f + p.physical.jumping + p.physical.strength) / 3.2f;
                sum += v; n++;
                if (v > best) best = v;
            }

            if (n == 0) return 10f;

            // 평균과 최고를 섞는다. 장신 한 명만 있어도 코너는 위협적이지만,
            // 팀 전체가 작으면 세컨볼을 못 잡는다.
            return sum / n * 0.6f + best * 0.4f;
        }

        /// <summary>
        /// 달려드는 움직임. 오프더볼·위치 선정·가속으로 본다.
        /// 아웃스윙과 낮은 크로스는 키가 아니라 이쪽에 걸린다.
        /// </summary>
        public static float MovementThreat(PlayerData[] starters)
        {
            float sum = 0f;
            int n = 0;

            foreach (var p in starters)
            {
                if (p?.mental == null || p.physical == null) continue;
                if ((p.positionFlags & Position.GK) != 0) continue;

                sum += (p.mental.offTheBall * 1.3f + p.mental.positioning + p.physical.acceleration) / 3.3f;
                n++;
            }

            return n == 0 ? 10f : sum / n;
        }

        /// <summary>수비 쪽 공중 방어. 골키퍼의 제공권과 박스 장악이 함께 들어간다.</summary>
        public static float AerialDefence(PlayerData[] starters, PlayerData keeper)
        {
            float sum = 0f;
            int n = 0;

            foreach (var p in starters)
            {
                if (p?.technical == null || p.physical == null) continue;
                if ((p.positionFlags & Position.GK) != 0) continue;
                if ((p.positionFlags & (Position.CB | Position.LB | Position.RB |
                                        Position.LWB | Position.RWB | Position.CDM)) == 0) continue;

                sum += (p.technical.heading + p.physical.jumping + p.physical.strength) / 3f;
                n++;
            }

            float outfield = n == 0 ? 10f : sum / n;
            float gk = keeper?.goalkeeper == null
                ? 10f
                : (keeper.goalkeeper.aerialAbility + keeper.goalkeeper.commandArea) / 2f;

            return outfield * 0.72f + gk * 0.28f;
        }

        // ── 키커 ──────────────────────────────────────────────────────────

        /// <summary>
        /// 지명된 키커. 감독이 정하지 않았으면 가장 잘 차는 선수가 찬다.
        /// 자동으로 고르는 것이 기본이라, 설정을 건드리지 않아도 손해가 아니다.
        /// </summary>
        public static PlayerData Taker(PlayerData[] starters, TacticsData tactics, Kind kind)
        {
            int chosen = ChosenTakerId(tactics, kind);
            if (chosen > 0)
            {
                var named = starters.FirstOrDefault(p => p?.id == chosen);
                if (named != null) return named;        // 교체돼 나갔으면 자동으로 넘어간다
            }

            return kind == Kind.Penalty
                ? Pick(starters, p => p.technical.finishing * 1.3f + p.mental.composure)
                : Pick(starters, p => p.technical.setPieces * 1.5f + p.technical.crossing);
        }

        static int ChosenTakerId(TacticsData tactics, Kind kind)
        {
            if (tactics?.setPieces == null) return -1;

            if (kind == Kind.Penalty)
            {
                var pen = tactics.slots?.FirstOrDefault(
                    s => s.customInstructions?.takePenalties == true);
                return pen?.assignedPlayerId ?? -1;
            }

            var list = kind == Kind.Corner
                ? tactics.setPieces.cornerKickLeft
                : tactics.setPieces.freeKickRoutines;

            return list is { Count: > 0 } ? list[0].takerPlayerId : -1;
        }

        static PlayerData Pick(PlayerData[] starters, Func<PlayerData, float> score)
        {
            PlayerData best = null;
            float bestScore = -1f;

            foreach (var p in starters)
            {
                if (p?.technical == null || p.mental == null) continue;
                if ((p.positionFlags & Position.GK) != 0) continue;

                float s = score(p);
                if (s > bestScore) { bestScore = s; best = p; }
            }

            return best;
        }

        /// <summary>키커의 배달 품질. 1.0 이 평범함.</summary>
        public static float TakerQuality(PlayerData taker, Kind kind)
        {
            if (taker?.technical == null) return 1f;

            float raw = kind == Kind.Penalty
                ? (taker.technical.finishing * 1.3f + taker.mental.composure) / 2.3f
                : (taker.technical.setPieces * 1.5f + taker.technical.crossing) / 2.5f;

            // 13 을 평범한 1부 주전으로 본다.
            return 1f + (raw - BalanceConfig.SetPieceTakerBase) / BalanceConfig.SetPieceTakerBase
                        * BalanceConfig.SetPieceTakerSwing;
        }

        /// <summary>
        /// 골키퍼의 박스 장악력. 코너 수비는 전체 능력보다 제공권과 지휘가 중요하다.
        /// 컨디션·사기가 반영된 gkPower 의 비율을 그대로 받아 같은 척도로 맞춘다.
        /// </summary>
        public static float GkCommand(PlayerData keeper, float gkPower)
        {
            if (keeper?.goalkeeper is not { } g) return gkPower;

            float raw = g.Average();
            if (raw <= 0f) return gkPower;

            float command = (g.commandArea + g.aerialAbility) / 2f;
            return gkPower * (command / raw);
        }

        // ── 배달 선택 ─────────────────────────────────────────────────────

        /// <summary>
        /// 배달 방식의 거래.
        ///
        /// 핵심은 <b>제공권 싸움에 얼마나 거느냐</b>다. 높이 띄우면 제공권 우위가 그대로
        /// 증폭되지만 열세면 그만큼 손해고, 낮고 빠르게 깔면 제공권을 거의 건너뛴다.
        /// 단순히 "위협 몇 % 증가"로 두면 어느 하나가 늘 최선이라 고를 이유가 없다.
        ///
        /// 반환: (기본 위협 배수, 제공권 비중 배수, 골키퍼가 걷어낼 위험 배수)
        /// </summary>
        /// <summary>
        /// 배달 방식별 효과.
        ///
        /// 다섯 가지가 <b>서로 다른 능력에 기댄다</b>. 전부 "제공권에 얼마나 거느냐"로만
        /// 두면 높이 띄우기와 인스윙이 사실상 같은 선택이 되어, 셋이 늘 지배당한다.
        ///
        /// 반환: (기본 위협, 제공권 비중, 움직임 비중, 키커 비중, 골키퍼 노출)
        /// </summary>
        public static Delivery DeliveryEffect(SetPieceDelivery delivery) => delivery switch
        {
            // 안쪽으로 휘어 들어온다 — 키커의 배달이 전부이고 골키퍼 앞을 지난다
            SetPieceDelivery.Inswinger => new Delivery(
                1f + BalanceConfig.InswingerThreat, 1.00f, 0.20f, 2.0f, 1f + BalanceConfig.InswingerGkRisk),

            // 골문에서 멀어진다 — 달려드는 선수의 타이밍이 중요하고 골키퍼는 못 나온다
            SetPieceDelivery.Outswinger => new Delivery(
                1f - BalanceConfig.OutswingerThreat, 0.40f, 2.00f, 1.0f, 1f - BalanceConfig.OutswingerGkRisk),

            // 낮고 빠르게 — 제공권 싸움을 건너뛴다. 키 작은 팀의 선택지.
            SetPieceDelivery.DrivenLow => new Delivery(
                1f + BalanceConfig.DrivenThreat, 0.25f, 0.55f, 1.2f, 1f - BalanceConfig.DrivenGkRisk),

            // 높이 띄워 제공권에 전부 건다. 우위면 크게 먹고 열세면 크게 잃는다.
            SetPieceDelivery.Lofted => new Delivery(
                1f + BalanceConfig.LoftedThreat, 1.80f, 0.10f, 0.6f, 1f + BalanceConfig.LoftedGkRisk),

            // 짧게 — 득점 기대는 낮지만 공을 잃지 않는다(Resolve 가 열린 찬스로 되돌린다)
            _ => new Delivery(
                1f - BalanceConfig.ShortThreat, 0.35f, 0.70f, 0.8f, 1f - BalanceConfig.ShortGkRisk),
        };

        public readonly record struct Delivery(
            float Threat, float AerialWeight, float MovementWeight, float TakerWeight, float GkRisk);

        /// <summary>감독이 고른 배달 방식. 정하지 않았으면 인스윙.</summary>
        public static SetPieceDelivery DeliveryOf(TacticsData tactics, Kind kind)
        {
            if (tactics?.setPieces == null) return SetPieceDelivery.Inswinger;

            var list = kind == Kind.Corner
                ? tactics.setPieces.cornerKickLeft
                : tactics.setPieces.freeKickRoutines;

            return list is { Count: > 0 } ? list[0].delivery : SetPieceDelivery.Inswinger;
        }

        /// <summary>
        /// 스쿼드에 맞는 배달 방식. 상대를 모르는 상태에서 고르는 기본값이다.
        /// 장신이면 띄우고, 작지만 잘 움직이면 아웃스윙, 둘 다 아니면 낮게 깐다.
        /// </summary>
        public static SetPieceDelivery SuggestDelivery(PlayerData[] starters)
        {
            float aerial = AerialThreat(starters);
            float movement = MovementThreat(starters);
            var taker = Pick(starters, p => p.technical.setPieces * 1.5f + p.technical.crossing);
            float takerQ = TakerQuality(taker, Kind.Corner);

            // 평범한 1부 주전을 13 으로 본다.
            float aerialEdge = aerial / 13.5f;
            float moveEdge = movement / 13f;

            if (aerialEdge > 1.08f) return SetPieceDelivery.Lofted;
            if (takerQ > 1.12f) return SetPieceDelivery.Inswinger;
            if (moveEdge > 1.05f) return SetPieceDelivery.Outswinger;
            if (aerialEdge < 0.95f) return SetPieceDelivery.DrivenLow;
            return SetPieceDelivery.Inswinger;
        }

        // ── 기대 득점 ─────────────────────────────────────────────────────

        /// <summary>
        /// 세트피스 하나의 기대 득점.
        /// 열린 플레이와 달리 전력 비율이 아니라 <b>제공권과 키커</b>가 정한다.
        /// </summary>
        /// <param name="movementEdge">달려드는 선수들의 움직임 우위. 1.0 이 평범함.</param>
        public static float Xg(
            Kind kind,
            float takerQuality,
            float aerialThreat, float aerialDefence,
            SetPieceDelivery delivery,
            float gkPower,
            float movementEdge = 1f)
        {
            if (kind == Kind.Penalty)
                return Math.Clamp(BalanceConfig.PenaltyXg * takerQuality, 0.55f, 0.92f);

            var d = DeliveryEffect(delivery);

            if (kind == Kind.FreeKick)
            {
                // 직접 프리킥은 거의 전적으로 키커의 몫이다.
                float fk = BalanceConfig.FreeKickXg * takerQuality * takerQuality;
                fk /= GkFactor(gkPower, d.GkRisk);
                return Math.Clamp(fk, 0.02f, 0.22f);
            }

            float edge = aerialDefence <= 0f ? 1f : aerialThreat / aerialDefence;
            float corner = BalanceConfig.CornerXg
                         * MathF.Pow(takerQuality, d.TakerWeight)
                         * MathF.Pow(edge, BalanceConfig.AerialExponent * d.AerialWeight)
                         * MathF.Pow(movementEdge, BalanceConfig.MovementExponent * d.MovementWeight)
                         * d.Threat;

            float gkRisk = d.GkRisk;

            // 코너에서는 골키퍼가 박스를 얼마나 장악하느냐가 열린 플레이보다 훨씬 크게 작용한다.
            // 그래야 인스윙(골키퍼를 부른다)과 아웃스윙(피한다)이 상대 골키퍼에 따라 갈린다.
            corner /= CornerGkFactor(gkPower, gkRisk);
            return Math.Clamp(corner, 0.005f, 0.16f);
        }

        static float GkFactor(float gkPower, float gkRisk)
            => Math.Clamp(
                (1f - BalanceConfig.GkSwing)
                + gkPower / BalanceConfig.GkNeutralPower * BalanceConfig.GkSwing * gkRisk,
                0.45f, 1.60f);

        static float CornerGkFactor(float gkCommand, float gkRisk)
        {
            // gkRisk 는 "골키퍼에게 얼마나 노출되는가"다. 1 보다 크면 골키퍼 실력이 더 크게 걸린다.
            float exposure = BalanceConfig.CornerGkSwing * gkRisk;
            return Math.Clamp(
                (1f - exposure) + gkCommand / BalanceConfig.GkNeutralPower * exposure,
                0.45f, 1.80f);
        }

        /// <summary>그 세트피스를 마무리할 선수의 가중치. 코너는 제공권이 좋은 선수가 넣는다.</summary>
        public static (PlayerData p, float w)[] ScorerWeights(PlayerData[] starters, Kind kind)
        {
            var list = new List<(PlayerData, float)>();

            foreach (var p in starters)
            {
                if (p?.technical == null || p.physical == null) continue;
                if ((p.positionFlags & Position.GK) != 0) continue;

                float w = kind == Kind.Corner
                    ? p.technical.heading * 1.4f + p.physical.jumping + p.mental.positioning * 0.5f
                    : p.technical.finishing + p.mental.offTheBall;

                list.Add((p, MathF.Max(0.1f, w)));
            }

            return list.ToArray();
        }
    }
}
