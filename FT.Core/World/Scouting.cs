using System;
using System.Collections.Generic;
using System.Linq;
using FootballTactician.Balance;
using FootballTactician.Data;

namespace FootballTactician.World
{
    /// <summary>
    /// 선수 정보의 불확실성.
    ///
    /// 지금까지 세계의 모든 선수가 능력치를 그대로 드러냈다. 그러면 이적은 숫자 비교일 뿐이고,
    /// 스카우트를 보낼 이유도, 잘못 사서 후회할 일도 없다.
    ///
    /// 내 선수는 매일 보니까 정확히 안다. 남의 선수는 <b>구간</b>으로만 보이고,
    /// 지켜볼수록 구간이 좁아진다.
    ///
    /// 구간은 저장하지 않는다. 본 횟수만 저장하고 값은 그때그때 계산한다 —
    /// 선수 6,000명분 구간을 저장하면 세이브가 또 불어난다.
    /// </summary>
    public static class Scouting
    {
        /// <summary>그 선수를 얼마나 아는지. 0 = 소문만, 1 = 내 선수처럼.</summary>
        public static float Knowledge(GameWorld w, PlayerData p)
        {
            if (p == null) return 0f;

            // 내 선수는 매일 본다.
            if (p.clubId == w.ManagedClubId) return 1f;

            // 보고서 한 장의 값어치는 스카우트가 정한다. 사람 없이 보낸 관찰은 거의 헛걸음이다.
            float perReport = StaffRoom.ReportValue(w, w.ManagedClubId);
            float reports = w.ScoutReports.GetValueOrDefault(p.id) * perReport;
            float known = Math.Min(reports, BalanceConfig.ScoutReportsForFull)
                          / BalanceConfig.ScoutReportsForFull;

            // 같은 리그에서 매주 보는 선수는 보고서가 없어도 어느 정도 안다.
            if (SameLeague(w, p)) known = Math.Max(known, BalanceConfig.SameLeagueKnowledge);

            // 대표팀에 나가는 선수는 누구나 안다.
            if (p.caps > 0) known = Math.Max(known, BalanceConfig.CappedKnowledge);

            return Math.Clamp(known, 0f, 1f);
        }

        static bool SameLeague(GameWorld w, PlayerData p)
        {
            var mine = w.MyClub == null ? null : w.LeagueOf(w.ManagedClubId);
            return mine != null && w.LeagueOf(p.clubId)?.id == mine.id;
        }

        /// <summary>
        /// 능력치 추정 구간. 모를수록 넓다.
        ///
        /// 구간의 중심은 참값에서 조금 빗나간다 — 그래야 넓은 구간이
        /// "참값을 품은 채 넓기만 한" 것이 아니라 실제로 틀릴 수 있다.
        /// 빗나가는 방향은 선수마다 고정이다(시드). 새로고침할 때마다 바뀌면 추정이 아니라 잡음이다.
        /// </summary>
        public static (float low, float high, float shown) Estimate(GameWorld w, PlayerData p, float trueValue)
        {
            float known = Knowledge(w, p);
            if (known >= 1f) return (trueValue, trueValue, trueValue);

            float width = BalanceConfig.ScoutMaxError * (1f - known);

            // 선수 id 로 고정된 치우침. -1 ~ +1.
            float bias = Bias(p.id) * width * BalanceConfig.ScoutBiasShare;
            float centre = trueValue + bias;

            return (centre - width, centre + width, centre);
        }

        static float Bias(int playerId)
        {
            // 작은 해시. 같은 선수는 늘 같은 방향으로 빗나간다.
            unchecked
            {
                int h = (int)(playerId * 2654435761u);
                return ((h >> 8) & 0xFFFF) / 32767.5f - 1f;
            }
        }

        /// <summary>화면에 쓸 문구. 모를수록 뭉뚱그린다.</summary>
        public static string Label(GameWorld w, PlayerData p, float trueValue)
        {
            float known = Knowledge(w, p);
            if (known >= 1f) return MathF.Round(trueValue).ToString("F0");

            var (low, high, _) = Estimate(w, p, trueValue);
            return $"{MathF.Round(low):F0}~{MathF.Round(high):F0}";
        }

        /// <summary>신뢰도 표시용.</summary>
        public static string Confidence(GameWorld w, PlayerData p) => Knowledge(w, p) switch
        {
            >= 1f => "직접 봄",
            >= 0.75f => "잘 앎",
            >= 0.45f => "대략 앎",
            >= 0.2f => "소문 수준",
            _ => "거의 모름",
        };

        // ── 보고서 ────────────────────────────────────────────────────────

        /// <summary>
        /// 스카우트를 보낸다. 한 번에 다 알 수는 없고, 볼수록 좁아진다.
        /// 공짜가 아니다 — 주당 스카우트 횟수가 정해져 있다.
        /// </summary>
        public static bool SendScout(GameWorld w, PlayerData p)
        {
            if (p == null || p.clubId == w.ManagedClubId) return false;
            if (w.ScoutingLeft <= 0) return false;
            if (Knowledge(w, p) >= 1f) return false;

            w.ScoutReports[p.id] = w.ScoutReports.GetValueOrDefault(p.id) + 1;
            w.ScoutingLeft--;
            return true;
        }

        /// <summary>한 주가 지나면 스카우트 횟수가 돌아온다.</summary>
        public static void StartWeek(GameWorld w)
        {
            var club = w.MyClub;
            w.ScoutingLeft = club == null
                ? 0
                : BalanceConfig.ScoutsBase + club.reputation * BalanceConfig.ScoutsPerStar
                  + StaffRoom.ExtraScoutTrips(w, club.id);   // 스카우트가 많으면 더 돌아본다
        }

        /// <summary>
        /// 영입한 선수는 바로 알게 되고, 떠난 선수의 보고서는 버린다.
        /// 남겨 두면 세이브에 쓸모없는 기록이 쌓인다.
        /// </summary>
        public static void Forget(GameWorld w, int playerId) => w.ScoutReports.Remove(playerId);

        /// <summary>
        /// 다 보려면 보고서가 몇 장 더 필요한지. 스카우트가 좋을수록 적다.
        /// </summary>
        public static int ReportsNeeded(GameWorld w, PlayerData p)
        {
            float per = StaffRoom.ReportValue(w, w.ManagedClubId);
            float have = w.ScoutReports.GetValueOrDefault(p.id) * per;
            return (int)MathF.Ceiling(Math.Max(0f, BalanceConfig.ScoutReportsForFull - have) / per);
        }

        /// <summary>
        /// AI 는 정보를 다 안다고 본다. 스카우트를 흉내 내려면 구단마다 지식을 들고 있어야 하는데,
        /// 세이브가 선수 수 × 구단 수로 불어난다. 대신 AI 의 영입 안목을 평판으로 제한해
        /// (<see cref="AiRecruiting.SkillOf"/>) 전지함이 그대로 우위가 되지 않게 했다.
        /// </summary>
        public const bool AiKnowsEverything = true;
    }
}
