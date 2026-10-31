using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json.Serialization;

namespace FootballTactician.Data
{
    /// <summary>
    /// 국내 컵. 한 나라의 1부와 2부가 모두 참가하는 단판 토너먼트.
    ///
    /// 리그와 같은 주에 끼어들지 않도록 라운드를 정해진 주차에만 둔다.
    /// 재경기는 없고, 비기면 그 자리에서 승부차기로 가른다.
    /// </summary>
    [Serializable]
    public sealed class CupState
    {
        public int id;
        public string name;
        public string shortName;
        public string country;
        public int year;

        /// <summary>참가 구단. 2의 거듭제곱이 되도록 상위 구단에 부전승을 준다.</summary>
        public List<int> entrants = new();

        public List<CupRound> rounds = new();
        public int currentRound;
        public int winnerClubId = -1;

        /// <summary>부전승으로 1라운드를 건너뛴 구단. 2라운드에서 합류한다.</summary>
        public List<int> PendingByes = new();

        /// <summary>
        /// 라운드를 치르는 리그 라운드 번호. 대회마다 다르게 둬서 서로 겹치지 않게 한다.
        /// 대회를 만드는 쪽이 채운다 — 모델은 어느 대회가 어느 주차를 쓰는지 알지 않는다.
        /// 비어 있으면 읽는 쪽이 국내 컵 주차로 본다.
        /// </summary>
        public int[] roundMatchdays;

        /// <summary>대륙 대회면 true. 국내 컵과 구분해 표시한다.</summary>
        public bool continental;

        /// <summary>
        /// 조별리그. 비어 있으면 처음부터 토너먼트다.
        /// 조별리그가 있으면 이것을 모두 치른 뒤에야 rounds 가 채워진다.
        /// </summary>
        public List<CupGroup> groups = new();

        [JsonIgnore] public bool HasGroups => groups.Count > 0;

        /// <summary>조별리그가 끝났는지. 끝나야 토너먼트 대진을 짤 수 있다.</summary>
        [JsonIgnore]
        public bool GroupsDone => groups.Count > 0 && groups.All(g => g.fixtures.All(f => f.played));

        [JsonIgnore] public bool Finished => winnerClubId > 0;
        [JsonIgnore] public CupRound Current => currentRound < rounds.Count ? rounds[currentRound] : null;
    }

    /// <summary>
    /// 대륙 대회의 한 조. 같은 나라 팀끼리는 한 조에 넣지 않는다 —
    /// 같은 리그에서 또 만나면 대륙 대회의 맛이 사라진다.
    /// </summary>
    [Serializable]
    public sealed class CupGroup
    {
        public string name = "";
        public List<int> clubIds = new();
        public List<CupTie> fixtures = new();

        /// <summary>조 순위. 승점 → 득실 → 다득점.</summary>
        public List<int> Standing()
        {
            var points = clubIds.ToDictionary(id => id, _ => 0);
            var gf = clubIds.ToDictionary(id => id, _ => 0);
            var ga = clubIds.ToDictionary(id => id, _ => 0);

            foreach (var f in fixtures)
            {
                if (!f.played) continue;
                if (!points.ContainsKey(f.homeClubId) || !points.ContainsKey(f.awayClubId)) continue;

                gf[f.homeClubId] += f.homeGoals; ga[f.homeClubId] += f.awayGoals;
                gf[f.awayClubId] += f.awayGoals; ga[f.awayClubId] += f.homeGoals;

                if (f.homeGoals > f.awayGoals) points[f.homeClubId] += 3;
                else if (f.homeGoals < f.awayGoals) points[f.awayClubId] += 3;
                else { points[f.homeClubId]++; points[f.awayClubId]++; }
            }

            return clubIds
                .OrderByDescending(id => points[id])
                .ThenByDescending(id => gf[id] - ga[id])
                .ThenByDescending(id => gf[id])
                .ThenBy(id => id)
                .ToList();
        }

        public (int points, int gf, int ga) Record(int clubId)
        {
            int p = 0, f = 0, a = 0;
            foreach (var m in fixtures)
            {
                if (!m.played) continue;
                if (m.homeClubId == clubId)
                {
                    f += m.homeGoals; a += m.awayGoals;
                    p += m.homeGoals > m.awayGoals ? 3 : m.homeGoals == m.awayGoals ? 1 : 0;
                }
                else if (m.awayClubId == clubId)
                {
                    f += m.awayGoals; a += m.homeGoals;
                    p += m.awayGoals > m.homeGoals ? 3 : m.homeGoals == m.awayGoals ? 1 : 0;
                }
            }
            return (p, f, a);
        }
    }

    [Serializable]
    public sealed class CupRound
    {
        public int index;
        public string name;              // "64강" 같은 표시용
        public int matchday;             // 이 라운드를 치르는 리그 라운드 번호
        public GameDate date;
        public List<CupTie> ties = new();

        /// <summary>참가 팀이 홀수일 때 자동으로 다음 라운드에 오르는 팀. 없으면 0.</summary>
        public int ByeClubId;
        [JsonIgnore] public bool Played => ties.All(t => t.played);
    }

    [Serializable]
    public sealed class CupTie
    {
        public int homeClubId;
        public int awayClubId;
        public bool played;
        public int homeGoals;
        public int awayGoals;
        public bool decidedOnPenalties;
        public int homePens, awayPens;
        public int WinnerClubId;

        // 조별리그 경기는 라운드가 아니라 각자 주차를 들고 다닌다.
        public int matchday;
        public GameDate date;
    }
}
