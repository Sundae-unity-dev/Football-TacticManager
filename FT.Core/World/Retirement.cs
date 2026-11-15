using System;
using System.Collections.Generic;
using System.Linq;
using FootballTactician.Balance;
using FootballTactician.Data;

namespace FootballTactician.World
{
    /// <summary>
    /// 은퇴.
    ///
    /// 지금까지 아무도 그만두지 않았다. 12시즌을 돌리면 <b>46세 선수가 뛰고 40세 이상이 330명</b>
    /// 있었다. 전력과 득점만 재는 감사로는 잡히지 않았다 — 늙은 선수는 약해져서
    /// 경기에 못 나갈 뿐 명단에서 사라지지 않았기 때문이다.
    ///
    /// 나이만으로 자르지 않는다. 서른여섯에도 주전이면 한 해 더 뛰고,
    /// 서른둘이라도 한 경기도 못 뛰었으면 그만둔다. 그래야 늙은 주전을 붙잡는 것이
    /// <b>판단</b>이 된다 — 다음 시즌에 갑자기 떠날 수 있다.
    /// </summary>
    public static class Retirement
    {
        /// <summary>이 나이 아래로는 그만두지 않는다. 부상 은퇴는 따로 다루지 않는다.</summary>
        public static bool CanRetire(int age) => age >= BalanceConfig.RetireMinAge;

        /// <summary>
        /// 그만둘 확률. 나이가 밀어 올리고, 출전과 기량이 붙잡는다.
        /// </summary>
        public static float Chance(GameWorld w, PlayerData p) => Chance(w, p, null);

        static float Chance(GameWorld w, PlayerData p, Dictionary<int, float> peerCache)
        {
            int age = w.Age(p);
            if (!CanRetire(age)) return 0f;

            // 나이가 바탕을 깐다.
            float chance = (age - BalanceConfig.RetireMinAge + 1) * BalanceConfig.RetirePerYear;

            // 뛴 사람은 더 뛴다. 한 경기도 못 뛰었으면 접는다.
            int games = p.seasonStats?.appearances ?? 0;
            chance -= Math.Min(games, BalanceConfig.RetireGamesCap) * BalanceConfig.RetirePerGame;

            // 기량이 남아 있으면 붙잡힌다. 절대 수준이 아니라 또래 대비로 본다 —
            // 절대 수준을 쓰면 하부 리그 선수가 서른둘에 전부 사라진다.
            float ca = GameWorld.Ca(p);
            float peers = w.LeagueOf(p.clubId) is { } lg
                ? AverageCa(w, lg, peerCache)
                : ca;
            chance -= (ca - peers) * BalanceConfig.RetirePerCaOverPeers;

            // 소속이 없으면 더 빨리 접는다.
            if (p.clubId <= 0) chance += BalanceConfig.RetireFreeAgentPenalty;

            // 골키퍼는 오래 한다.
            if ((p.positionFlags & Position.GK) != 0) chance -= BalanceConfig.RetireKeeperBonus;

            return Math.Clamp(chance, 0f, BalanceConfig.RetireChanceCap);
        }

        /// <summary>
        /// 리그 평균 기량. 한 번 은퇴를 돌리는 동안 리그마다 한 번만 센다 — 선수 6,000명 ×
        /// 리그 전체를 매번 훑으면 시즌 넘김이 느려진다.
        ///
        /// <b>캐시는 부르는 쪽이 들고 있다.</b> 예전에는 정적 사전이었는데, 열쇠가 리그 id 와
        /// 시즌이라 같은 나라로 만든 두 세계가 서로의 평균을 읽었다. 화면에는 세계가 하나라
        /// 드러나지 않았지만, 테스트가 나란히 돌면 은퇴자가 바뀌어 결과가 흔들렸다.
        /// </summary>
        static float AverageCa(GameWorld w, LeagueData league, Dictionary<int, float> cache)
        {
            if (cache != null && cache.TryGetValue(league.id, out float hit)) return hit;

            var squad = league.clubIds.SelectMany(id => w.Squad(id)).ToList();
            float avg = squad.Count == 0 ? 100f : squad.Average(GameWorld.Ca);
            if (cache != null) cache[league.id] = avg;
            return avg;
        }

        public static List<PlayerData> RunAll(GameWorld w, Random rng)
        {
            var peerCache = new Dictionary<int, float>();
            var gone = new List<PlayerData>();

            // 소속이 없는 선수도 본다. 자유계약은 경기를 못 뛰니 더 빨리 접는다 —
            // 빼놓으면 마흔둘짜리 자유계약이 남아 있다가 계약 정리 때 다시 영입된다.
            //
            // 명단 순서를 고정한다. Dictionary 순회 순서에 기대면 세이브를 거쳐 결과가 달라진다.
            foreach (var p in w.Players.Values.OrderBy(x => x.id).ToList())
            {
                bool forced = w.Age(p) >= BalanceConfig.RetireHardAge;
                if (!forced && rng.NextDouble() >= Chance(w, p, peerCache)) continue;

                // 마지막 한 명까지 그만두면 구단이 빈다. 인원 보충이 메우지만 그 전에 터질 수 있다.
                // 다만 나이 상한은 이 보호를 넘는다 — 넘지 않으면 얇은 구단에 마흔넷이 남는다.
                if (!forced && p.clubId > 0 && w.Squad(p.clubId).Count() <= BalanceConfig.RetireSquadFloor)
                    continue;

                gone.Add(p);
            }

            foreach (var p in gone) Retire(w, p);
            return gone;
        }

        static void Retire(GameWorld w, PlayerData p)
        {
            var club = w.Club(p.clubId);
            bool mine = p.clubId == w.ManagedClubId;
            int age = w.Age(p);
            int caps = p.caps;
            int apps = p.careerStats?.appearances ?? 0;

            club?.playerIds?.Remove(p.id);
            p.clubId = 0;   // 남겨 두면 떠난 선수가 아직 소속된 것처럼 읽힌다

            // 남은 흔적을 모두 지운다. 하나라도 남으면 없는 선수를 가리키는 참조가 된다.
            w.Loans.Remove(p.id);
            TransferRequests.Clear(w, p.id);
            Scouting.Forget(w, p.id);
            // 자기 구단 전술만 본다. 240구단을 모두 훑으면 은퇴자 600명 × 240 × 11 로
            // 시즌마다 300만 번이 되는데, 선수는 남의 구단 전술에 들어 있을 수 없다.
            if (club?.savedTactics != null)
                foreach (var t in club.savedTactics)
                    foreach (var s in t.slots)
                        if (s.assignedPlayerId == p.id) s.assignedPlayerId = 0;

            w.Players.Remove(p.id);

            if (mine)
                w.Post($"{p.FullName} 은퇴",
                       $"{age}세. 통산 {apps}경기" + (caps > 0 ? $", A매치 {caps}경기" : "") + "를 끝으로 그라운드를 떠납니다.",
                       InboxKind.News);
        }
    }
}
