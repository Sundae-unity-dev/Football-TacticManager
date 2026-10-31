using System;
using System.Collections.Generic;
using System.Linq;
using FootballTactician.Diagnostics;
using FootballTactician.Balance;
using FootballTactician.Data;

namespace FootballTactician.World
{
    /// <summary>
    /// 임대. 1군에서 뛰지 못하는 어린 선수를 내보내 출전 시간을 받게 한다.
    ///
    /// 임대가 성장으로 이어지지 않으면 보낼 이유가 없다. 임대 간 선수는 받는 구단에서
    /// 실제로 뛰고, 그만큼 빨리 큰다. 대신 내 전력에서는 빠진다.
    /// </summary>
    public static class LoanMarket
    {
        /// <summary>임대 보낼 만한 선수 — 어리고, 1군에서 자리가 없고, 성장 여지가 있다.</summary>
        public static IEnumerable<PlayerData> Loanable(GameWorld w, int clubId)
        {
            var club = w.Club(clubId);
            if (club == null) yield break;

            var tac = club.savedTactics.FirstOrDefault(t => t.id == club.activeTacticsId);
            var starters = tac?.slots.Select(s => s.assignedPlayerId).ToHashSet() ?? new HashSet<int>();

            foreach (var p in w.Squad(clubId).OrderBy(p => w.Age(p)))
            {
                if (p.IsInjured) continue;
                if (w.OnLoan(p.id) != null) continue;
                if (starters.Contains(p.id)) continue;
                if (w.Age(p) > 23) continue;
                if (PlayerDevelopment.Pa(p) - GameWorld.Ca(p) < 10f) continue;
                yield return p;
            }
        }

        /// <summary>그 선수를 받을 만한 구단. 전력에 보탬이 되면서 너무 강하지 않은 곳.</summary>
        /// <summary>
        /// 이 선수를 보탠 스쿼드로 그 구단의 11명을 짰을 때 선발에 드는지.
        /// 게임이 쓰는 것과 같은 <see cref="LineupBuilder"/> 로 확인한다 —
        /// 다른 기준으로 재면 "뛸 수 있다"고 해 놓고 벤치에 앉히게 된다.
        /// </summary>
        public static bool WouldStart(GameWorld w, PlayerData p, ClubData host)
        {
            var tactics = host.savedTactics?.FirstOrDefault(t => t.id == host.activeTacticsId);
            if (tactics == null) return false;

            // 원본을 건드리지 않도록 슬롯만 복사해서 시험한다.
            var trial = new TacticsData
            {
                id = tactics.id,
                name = tactics.name,
                formationShape = tactics.formationShape,
                slots = tactics.slots
                    .Select(s => new FormationSlot
                    {
                        slotIndex = s.slotIndex,
                        position = s.position,
                        role = s.role,
                        assignedPlayerId = -1,
                    })
                    .ToList(),
            };

            using var span = SimProfile.Measure("      · 11명 확인");
            LineupBuilder.Assign(trial, w.Squad(host.id).Append(p));
            return trial.slots.Any(s => s.assignedPlayerId == p.id);
        }

        public static List<ClubData> Suitors(GameWorld w, PlayerData p, int take = 8, SquadPeaks peaks = null)
        {
            var owner = w.Club(p.clubId);
            if (owner == null) return new List<ClubData>();

            float ca = GameWorld.Ca(p);
            var pos = p.preferredPosition != Position.None ? p.preferredPosition : p.positionFlags;

            // 먼저 싸게 거른다. 같은 자리의 최고와 너무 차이 나면 볼 것도 없다.
            // 한 번 임대 시도마다 240구단을 보므로 여기서 목록을 만들면 안 된다 —
            // 창구가 열린 주에 리스트 7,000개가 만들어지고 버려졌다.
            var pass = new List<ClubData>();
            foreach (var c in w.Clubs.Values)
            {
                if (c.id == p.clubId) continue;
                if (c.playerIds.Count >= BalanceConfig.SquadMax) continue;
                if (ca < BestCaAt(w, c.id, pos, peaks) - 2f) continue;
                if (WageShare(w, p, c) > WageRoom(w, c)) continue;
                pass.Add(c);
            }

            // 시설이 좋을수록 성장에 낫고, 그다음은 수준이 맞는 곳.
            // 전력은 거르고 남은 구단만 잰다 — 240구단 전부 재면 그게 임대 비용의 대부분이 된다.
            var gap = new float[pass.Count];
            var order = new int[pass.Count];
            for (int i = 0; i < pass.Count; i++)
            {
                gap[i] = Math.Abs(w.Strength(pass[i].id) - ca);
                order[i] = i;
            }

            // 마지막 기준으로 원래 자리를 본다 — 동점이면 먼저 오는 쪽(안정 정렬과 같은 규칙).
            Array.Sort(order, (a, b) =>
            {
                int r = pass[b].trainingFacilitiesRating.CompareTo(pass[a].trainingFacilitiesRating);
                if (r != 0) return r;
                r = gap[a].CompareTo(gap[b]);
                return r != 0 ? r : a.CompareTo(b);
            });

            // 그리고 실제로 11명 안에 드는지 돌려서 확인한다.
            // 능력치 비교만으로는 모자라다 — 라인업은 슬롯 경쟁과 포지션 적합도로 정해져,
            // 같은 자리의 최고와 2 차이여도 그 한 자리를 뺏지 못하면 한 경기도 못 뛴다.
            // 돌려 보는 것이 가장 비싸므로 앞선 구단부터 보고 take 개를 채우면 멈춘다.
            var result = new List<ClubData>(take);
            foreach (int i in order)
            {
                if (result.Count >= take) break;
                if (WouldStart(w, p, pass[i])) result.Add(pass[i]);
            }
            return result;
        }

        /// <summary>
        /// 그 자리를 소화하는 선수 중 가장 높은 CA. 표가 있으면 표에서 본다 — 값은 같다.
        /// </summary>
        static float BestCaAt(GameWorld w, int clubId, Position pos, SquadPeaks peaks)
            => peaks != null ? peaks.BestCaAt(clubId, pos) : w.BestCaAt(clubId, pos);

        /// <summary>받는 구단이 부담하는 주급. 기본 70%.</summary>
        public static int WageShare(GameWorld w, PlayerData p, ClubData receiver)
            => (int)((p.contract?.weeklyWage ?? 0) * 0.70f);

        static long WageRoom(GameWorld w, ClubData c)
            => (long)(c.finances.annualRevenue * BalanceConfig.AiWageCeiling / 52)
               - c.finances.currentWageSpend;

        public enum Verdict { Accepted, NotLoanable, NoRoom, WontPlay, AlreadyOut }

        public sealed class Outcome
        {
            public Verdict Result;
            public string Message = "";
            public bool Accepted => Result == Verdict.Accepted;
        }

        public static Outcome Evaluate(GameWorld w, PlayerData p, ClubData receiver, SquadPeaks peaks = null)
        {
            if (w.OnLoan(p.id) != null)
                return new Outcome { Result = Verdict.AlreadyOut, Message = "이미 임대 중입니다." };
            if (receiver.playerIds.Count >= BalanceConfig.SquadMax)
                return new Outcome { Result = Verdict.NoRoom, Message = $"{receiver.name} 의 스쿼드가 가득 찼습니다." };
            if (WageShare(w, p, receiver) > WageRoom(w, receiver))
                return new Outcome { Result = Verdict.NoRoom, Message = $"{receiver.name} 이 주급을 감당하지 못합니다." };

            var pos = p.preferredPosition != Position.None ? p.preferredPosition : p.positionFlags;
            float best = BestCaAt(w, receiver.id, pos, peaks);
            if (GameWorld.Ca(p) < best - 2f)
                return new Outcome
                {
                    Result = Verdict.WontPlay,
                    Message = $"{receiver.name} 에는 이미 더 나은 자원이 있어 뛰지 못합니다.",
                };

            return new Outcome { Result = Verdict.Accepted, Message = "임대에 합의했습니다." };
        }

        /// <summary>임대를 보낸다. 선수는 받는 구단 명단으로 옮겨가고 시즌 말에 돌아온다.</summary>
        public static void Send(GameWorld w, PlayerData p, ClubData receiver)
        {
            var owner = w.Club(p.clubId);
            if (owner == null) return;

            var deal = new LoanSpell
            {
                playerId = p.id,
                parentClubId = owner.id,
                hostClubId = receiver.id,
                startDate = w.Date,
                endYear = w.SeasonYear + 1,                 // 시즌 말에 복귀
                wageSharePercent = 70,
            };

            owner.playerIds.Remove(p.id);
            receiver.playerIds.Add(p.id);
            p.clubId = receiver.id;
            p.isOnLoan = true;
            p.loanedFromClubId = owner.id;
            p.status = PlayerStatus.OnLoan;

            w.Loans[p.id] = deal;

            TransferMarket.RecalcWages(w, owner);
            TransferMarket.RecalcWages(w, receiver);
            WorldGenerator.ApplyBestEleven(owner, w);
            WorldGenerator.ApplyBestEleven(receiver, w);
        }

        /// <summary>임대를 끝내고 원소속으로 돌려보낸다.</summary>
        public static void Recall(GameWorld w, PlayerData p)
        {
            if (!w.Loans.TryGetValue(p.id, out var deal)) return;

            var host = w.Club(deal.hostClubId);
            var parent = w.Club(deal.parentClubId);
            w.Loans.Remove(p.id);

            // 임대 중에 선수가 어디로 갔든, 지금 이름이 올라 있는 명단에서 전부 뺀다.
            // 한 곳만 지우면 다른 구단 명단에 남의 선수가 남는다.
            foreach (var c in w.Clubs.Values) c.playerIds.Remove(p.id);

            p.isOnLoan = false;
            p.loanedFromClubId = null;
            p.status = PlayerStatus.Active;

            if (parent == null) { p.clubId = 0; return; }

            if (!parent.playerIds.Contains(p.id)) parent.playerIds.Add(p.id);
            p.clubId = parent.id;

            if (host != null)
            {
                TransferMarket.RecalcWages(w, host);
                WorldGenerator.ApplyBestEleven(host, w);
            }
            TransferMarket.RecalcWages(w, parent);
            WorldGenerator.ApplyBestEleven(parent, w);
        }

        /// <summary>시즌이 끝나면 임대 선수를 모두 돌려보낸다.</summary>
        public static void ReturnAll(GameWorld w)
        {
            var returning = new List<string>();

            foreach (int playerId in w.Loans.Keys.ToList())
            {
                var p = w.Player(playerId);
                if (p == null) { w.Loans.Remove(playerId); continue; }

                bool mine = w.Loans[playerId].parentClubId == w.ManagedClubId;
                Recall(w, p);
                if (mine) returning.Add($"{p.FullName} (CA {Math.Round(GameWorld.Ca(p))})");
            }

            if (returning.Count > 0 && w.ManagedClubId > 0)
                w.Post("임대 복귀",
                       $"{string.Join(", ", returning)} 이 임대를 마치고 돌아왔습니다.",
                       InboxKind.Transfer);
        }

        /// <summary>
        /// AI 구단끼리의 임대. 1군에서 못 뛰는 어린 선수를 수준이 맞는 곳으로 보낸다.
        /// </summary>
        public static int RunAiLoans(GameWorld w, Random rng, int attempts = 0)
        {
            if (attempts <= 0) attempts = Math.Max(8, w.Clubs.Count / 8);
            int done = 0;

            var clubs = w.Clubs.Values.Where(c => c.id != w.ManagedClubId).ToList();
            if (clubs.Count == 0) return 0;

            // 자리별 최고 CA 표를 한 번 만들어 시도 전체가 같이 쓴다.
            var peaks = new SquadPeaks(w);

            for (int i = 0; i < attempts; i++)
            {
                var owner = clubs[rng.Next(clubs.Count)];
                List<PlayerData> candidates;
                using (SimProfile.Measure("      · 임대 후보"))
                    candidates = Loanable(w, owner.id).ToList();
                if (candidates.Count == 0) continue;

                var p = candidates[rng.Next(candidates.Count)];
                List<ClubData> suitors;
                using (SimProfile.Measure("      · 받을 곳 찾기"))
                    suitors = Suitors(w, p, 8, peaks).Where(c => c.id != w.ManagedClubId).ToList();
                if (suitors.Count == 0) continue;

                var host = suitors[rng.Next(Math.Min(3, suitors.Count))];
                if (!Evaluate(w, p, host, peaks).Accepted) continue;

                Send(w, p, host);
                done++;

                // 명단이 바뀐 두 구단만 다시 잰다. 이게 빠지면 표가 사실과 어긋난다.
                peaks.Refresh(owner.id);
                peaks.Refresh(host.id);
            }

            return done;
        }
    }

}
