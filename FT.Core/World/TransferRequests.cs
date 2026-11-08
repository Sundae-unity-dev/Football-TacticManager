using System;
using System.Collections.Generic;
using System.Linq;
using FootballTactician.Balance;
using FootballTactician.Data;

namespace FootballTactician.World
{

    public static class TransferRequests
    {
        /// <summary>
        /// 이 선수가 지금 이적을 요청할 만한지. 불만의 크기로 잰다.
        /// </summary>
        public static float Pressure(GameWorld w, PlayerData p)
        {
            var club = w.Club(p.clubId);
            if (club == null) return 0f;

            float pressure = PlayerAmbition.Frustration(w, p, PlayerAmbition.SeasonProgress(w));

            // 탈의실 전체가 흔들리면 개인도 흔들린다.
            float harmony = club.dressingRoom?.overallHarmony ?? 50f;
            pressure += Math.Max(0f, 50f - harmony) / 50f * BalanceConfig.HarmonyRequestWeight;

            // 사기가 바닥이면 더 빨리 터진다.
            pressure += Math.Max(0f, 45f - p.morale) / 45f * BalanceConfig.MoraleRequestWeight;

            // 이미 거절당한 선수는 다시 요구하기 쉽다.
            if (w.TransferRequests.TryGetValue(p.id, out var existing))
                pressure += existing.rejections * BalanceConfig.RejectionRequestWeight;

            return pressure;
        }

        /// <summary>
        /// 매주 불러 요청이 올라오는지 본다.
        /// 플레이어 구단만 다룬다 — AI 구단은 스스로 명단을 정리하므로 요청이 의미가 없다.
        /// </summary>
        public static void Update(GameWorld w, Random rng)
        {
            if (w.ManagedClubId <= 0 || w.MyClub is not { } club) return;

            foreach (var p in w.Squad(club.id).ToList())
            {
                // 답하지 않은 요청이 쌓이는 동안에는 새 요청이 올라오지 않는다.
                // 막지 않으면 감독이 답을 미루는 사이 스쿼드 절반이 한 화면에 들어찬다.
                if (w.TransferRequests.Count >= BalanceConfig.MaxPendingRequests) return;

                if (w.TransferRequests.ContainsKey(p.id)) continue;
                if (w.Loans.ContainsKey(p.id)) continue;         // 임대 중에는 요청하지 않는다

                // 이미 내보내기로 한 선수는 더 조르지 않는다.
                // 막지 않으면 수용 → 요청 해제 → 다시 요청의 고리가 돌아
                // 한 시즌에 스쿼드 전원이 몇 번씩 이적을 요구한다.
                if (p.transferStatus is TransferStatus.WantAway or TransferStatus.ListedForTransfer)
                    continue;

                // 거절당한 직후에는 한동안 참는다.
                if (w.RejectedRequests.TryGetValue(p.id, out var last)
                    && GameDate.DaysBetween(last.madeOn, w.Date) < BalanceConfig.RequestCooldownDays)
                    continue;

                float pressure = Pressure(w, p);
                if (pressure < BalanceConfig.RequestThreshold) continue;

                // 참다 못하는 데도 시간이 걸린다. 임계를 넘자마자 전원이 요청하면
                // 한 주에 스쿼드가 통째로 반란을 일으킨다.
                float chance = Math.Clamp(
                    (pressure - BalanceConfig.RequestThreshold) * BalanceConfig.RequestChancePerPoint,
                    0f, BalanceConfig.RequestChanceCap);

                if (rng.NextDouble() >= chance) continue;

                Submit(w, p);
            }
        }

        static void Submit(GameWorld w, PlayerData p)
        {
            string reason = MainReason(w, p);

            w.TransferRequests[p.id] = new TransferRequest
            {
                playerId = p.id,
                madeOn = w.Date,
                reason = reason,
            };

            w.Post("이적 요청",
                $"{p.FullName} 이 이적을 요청했습니다. \"{reason}\"",
                InboxKind.Transfer, playerId: p.id);
        }

        /// <summary>가장 큰 불만 한 가지. 화면에 그대로 나간다.</summary>
        public static string MainReason(GameWorld w, PlayerData p)
        {
            var unmet = PlayerAmbition.For(w, p).Where(g => !g.Met).ToList();

            if (unmet.Count > 0)
            {
                var worst = unmet.OrderBy(g => g.Ratio).First();
                return worst.Kind switch
                {
                    PlayerAmbition.Kind.StarterSpot => "주전으로 뛰고 싶습니다.",
                    PlayerAmbition.Kind.Appearances => "경기에 나가지 못하고 있습니다.",
                    PlayerAmbition.Kind.Goals => "골을 넣을 기회가 없습니다.",
                    PlayerAmbition.Kind.Assists => "제 역할을 할 자리가 아닙니다.",
                    PlayerAmbition.Kind.Silverware => "우승을 다툴 팀에서 뛰고 싶습니다.",
                    PlayerAmbition.Kind.CallUp => "대표팀에 들려면 더 뛰어야 합니다.",
                    _ => "새로운 도전이 필요합니다.",
                };
            }

            var club = w.Club(p.clubId);
            if ((club?.dressingRoom?.overallHarmony ?? 50f) < 45f)
                return "팀 분위기가 좋지 않습니다.";

            return "새로운 도전이 필요합니다.";
        }

        // ── 감독의 답 ─────────────────────────────────────────────────────

        /// <summary>
        /// 받아들인다. 선수는 이적 명단에 오르고 사기를 되찾는다.
        /// 대신 호가가 떨어져 제값을 받기 어렵다 — 그게 받아들이는 대가다.
        /// </summary>
        public static void Accept(GameWorld w, PlayerData p)
        {
            if (!w.TransferRequests.Remove(p.id)) return;

            p.transferStatus = TransferStatus.WantAway;
            p.morale = Math.Clamp(p.morale + BalanceConfig.RequestAcceptMorale, 25f, 95f);

            w.Post("이적 요청 수용",
                $"{p.FullName} 을 이적 명단에 올렸습니다. 사기를 되찾았지만 호가는 내려갑니다.",
                InboxKind.Transfer, playerId: p.id);
        }

        /// <summary>
        /// 거절한다. 당장 선수를 지키지만 사기가 떨어지고 탈의실이 흔들린다.
        /// 거듭 거절할수록 다음 요청이 빨리 온다.
        /// </summary>
        public static void Reject(GameWorld w, PlayerData p)
        {
            if (!w.TransferRequests.TryGetValue(p.id, out var req)) return;

            req.rejections++;
            req.madeOn = w.Date;

            p.morale = Math.Clamp(p.morale - BalanceConfig.RequestRejectMorale, 25f, 95f);

            // 다시 올라올 수 있도록 요청은 거둔다. 압력은 그대로 남아 금세 돌아온다.
            w.TransferRequests.Remove(p.id);
            w.RejectedRequests[p.id] = req;

            w.Post("이적 요청 거절",
                $"{p.FullName} 의 이적 요청을 거절했습니다. 사기가 떨어졌습니다" +
                (req.rejections >= 2 ? " — 벌써 " + req.rejections + "번째입니다." : "."),
                InboxKind.Transfer, playerId: p.id);
        }

        /// <summary>선수를 팔거나 내보내면 요청도 사라진다.</summary>
        public static void Clear(GameWorld w, int playerId)
        {
            w.TransferRequests.Remove(playerId);
            w.RejectedRequests.Remove(playerId);
        }

        /// <summary>시즌이 바뀌면 묵은 감정이 정리된다.</summary>
        public static void StartSeason(GameWorld w)
        {
            w.TransferRequests.Clear();
            w.RejectedRequests.Clear();
        }
    }
}
