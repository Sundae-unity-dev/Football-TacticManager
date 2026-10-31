using System;
using System.Collections.Generic;
using System.Linq;
using FootballTactician.Diagnostics;
using FootballTactician.Data;

namespace FootballTactician.World
{
    /// <summary>
    /// 탈의실. 선수 관계와 불만을 사실에서 끌어내, 사기 회복선을 통해 경기에 닿게 한다.
    ///
    /// 관계는 저장하지 않고 그때그때 계산한다. 선수 4,000명의 관계를 세이브에 넣으면
    /// 용량이 몇 배가 되는데, 어차피 나이·국적·포지션에서 똑같이 다시 나온다.
    /// </summary>
    public static class DressingRoom
    {
        /// <summary>사기가 돌아가는 기준선. 화합도 50 인 구단이 여기 선다.</summary>
        public const float MoraleAnchor = 62f;

        /// <summary>화합도가 기준선을 밀어 올리거나 내리는 폭.</summary>
        public const float AnchorSwing = 10f;

        /// <summary>멘토 관계가 성립하는 최소 나이 차.</summary>
        public const int MentorAgeGap = 8;

        /// <summary>같은 자리를 다투는 것으로 보는 능력치 차.</summary>
        public const float RivalCaBand = 6f;

        /// <summary>1인당 평균 불만 가중치를 0~100 불만 수위로 옮기는 배수.</summary>
        public const float DiscontentScale = 10f;

        // ── 사기 기준선 ────────────────────────────────────────────────────

        /// <summary>
        /// 이 구단 선수들의 사기가 돌아가는 기준선.
        /// 화합도 50 이 <see cref="MoraleAnchor"/>, 0 과 100 이 ±<see cref="AnchorSwing"/>.
        /// </summary>
        public static float MoraleAnchorFor(ClubData club)
        {
            float harmony = club?.dressingRoom?.overallHarmony ?? 50f;
            return MoraleAnchor + (harmony - 50f) / 50f * AnchorSwing;
        }

        // ── 매주 갱신 ──────────────────────────────────────────────────────

        /// <summary>
        /// 불만과 화합도를 지금 명단에서 다시 잰다.
        ///
        /// 난수를 쓰지 않는다. 플레이어가 명단을 바꾸면 그 결과가 보여야 하고,
        /// 눈에 띄지 않는 흔들림은 원인을 못 찾게 만든다.
        /// </summary>
        public static void Update(GameWorld w, ClubData club)
        {
            var room = club.dressingRoom ??= new DressingRoomMorale { clubId = club.id };

            // 매주 240구단을 도는 자리라 명단도 LINQ 없이 모은다.
            var squad = new List<PlayerData>(club.playerIds.Count);
            foreach (int id in club.playerIds)
                if (w.Players.TryGetValue(id, out var p)) squad.Add(p);

            if (squad.Count == 0) return;

            // 영향력을 한 번만 구해 네 군데가 같이 쓴다. 전에는 주장·부주장·영향력 3인·리더십이
            // 저마다 다시 구하며 25명을 네 번 정렬했다. 240구단을 매주 돌면 그게 주간 비용의 1/5 였다.
            var influence = new float[squad.Count];
            using (SimProfile.Measure("        · 영향력"))
                for (int i = 0; i < squad.Count; i++) influence[i] = Influence(w, squad[i]);

            // 주장과 부주장은 영향력 1·2위다 — 셋을 차례로 집으면 정렬과 같은 결과가 나온다.
            int first = TopIndex(influence);
            int second = TopIndex(influence, first);
            int third = TopIndex(influence, first, second);

            room.captainPlayerId = first < 0 ? -1 : squad[first].id;
            room.viceCaptainPlayerId = second < 0 ? -1 : squad[second].id;

            room.influencerPlayerIds = new List<int>(3);
            foreach (int i in new[] { first, second, third })
                if (i >= 0) room.influencerPlayerIds.Add(squad[i].id);

            // 인원으로 나눈다. 합계로 재면 스쿼드가 클수록 자동으로 불행해지고,
            // 25명 중 네 명이 불만인 팀과 전원이 불만인 팀이 구분되지 않는다.
            float total = 0f;
            using (SimProfile.Measure("        · 불만"))
                foreach (var g in Collect(w, club, squad)) total += g.weight;
            room.discontent = Math.Clamp(total / squad.Count * DiscontentScale, 0f, 100f);

            room.leadershipStrength = Math.Clamp(
                first < 0 ? 0f : influence[first] * 2.4f, 0f, 100f);

            // 화합도는 불만이 깎고 리더십이 떠받친다.
            room.overallHarmony = Math.Clamp(
                62f - room.discontent * 0.75f + (room.leadershipStrength - 50f) * 0.20f, 0f, 100f);
        }

        public static void UpdateAll(GameWorld w)
        {
            foreach (var club in w.Clubs.Values) Update(w, club);
        }

        // ── 불만 ──────────────────────────────────────────────────────────

        /// <summary>한 건의 불만. 화면에 그대로 보여 줘서 손댈 곳을 알 수 있게 한다.</summary>
        public readonly record struct Grievance(int PlayerId, string Reason, float weight);

        /// <summary>
        /// 지금 명단에서 나오는 불만. 모두 플레이어가 손댈 수 있는 것들이다 —
        /// 어쩔 수 없는 불만은 분위기를 설명하지 못하고 짜증만 남긴다.
        /// </summary>
        public static List<Grievance> Grievances(GameWorld w, ClubData club, IEnumerable<PlayerData> squad = null)
            => Collect(w, club, squad).OrderByDescending(g => g.weight).ToList();

        /// <summary>
        /// 불만을 모은다. 큰 것부터 보여 주는 정렬은 화면 쪽 <see cref="Grievances"/> 가 맡는다 —
        /// 매주 240구단을 도는 갱신은 합계만 쓰므로 정렬할 이유가 없다.
        /// 규칙은 여기 한 곳에만 둔다. 합계용으로 따로 베껴 두면 둘이 조용히 어긋난다.
        /// </summary>
        static List<Grievance> Collect(GameWorld w, ClubData club, IEnumerable<PlayerData> squad)
        {
            var list = new List<Grievance>();
            var players = (squad ?? w.Squad(club.id)).ToList();
            if (players.Count == 0) return list;

            // 구단 평판 계수는 선수마다 같다. 전에는 선수마다 소속 리그를 찾아 다시 구했다.
            float rep = w.RepCoefOf(club.id);

            float best = players.Max(p => GameWorld.Ca(p));
            var tac = club.savedTactics?.FirstOrDefault(t => t.id == club.activeTacticsId);
            var starters = tac == null
                ? new HashSet<int>()
                : tac.slots.Select(s => s.assignedPlayerId).ToHashSet();

            foreach (var p in players)
            {
                float ca = GameWorld.Ca(p);
                int age = w.Age(p);

                // 주전급인데 못 뛴다 — 가장 흔하고 가장 큰 불만
                if (!starters.Contains(p.id) && ca >= best - 8f)
                    list.Add(new Grievance(p.id, "주전으로 뛰지 못하고 있습니다", 7f));

                // 계약이 얼마 안 남았는데 아무 말이 없다.
                // 붙박이만 센다 — 떠나도 그만인 선수의 만료는 구단 분위기를 흔들지 않고,
                // 계약은 누구나 언젠가 끝나므로 전원을 세면 상시 불만이 된다.
                if (ca >= best - 15f && ContractTalks.MonthsLeft(w, p) is int m && m >= 0 && m <= 6)
                    list.Add(new Grievance(p.id, "계약 만료가 가까운데 재계약 이야기가 없습니다", 5f));

                // 또래보다 한참 적게 받는다
                int market = TransferMarket.ExpectedWage(w, p, rep);
                int wage = p.contract?.weeklyWage ?? 0;
                if (market > 0 && wage < market * 0.60f)
                    list.Add(new Grievance(p.id, "실력에 비해 주급이 낮습니다", 4f));

                // 한창 클 나이에 경기를 못 뛴다
                if (age <= 21 && !starters.Contains(p.id)
                    && p.seasonStats != null && p.seasonStats.appearances == 0)
                    list.Add(new Grievance(p.id, "출전 기회가 없어 성장이 멈췄습니다", 3f));

                // 이적을 요청해 놓고 답을 못 받으면 그것 자체가 불만이다.
                if (w.TransferRequests.ContainsKey(p.id))
                    list.Add(new Grievance(p.id, "이적 요청에 답을 기다리고 있습니다", 6f));
            }

            return list;
        }

        // ── 관계 ──────────────────────────────────────────────────────────

        /// <summary>
        /// 이 선수가 팀 안에서 맺고 있는 관계.
        /// 나이 차, 국적, 같은 자리 경쟁 — 모두 화면에서 확인할 수 있는 사실에서 나온다.
        /// </summary>
        public static List<PlayerRelationship> RelationsOf(GameWorld w, PlayerData p)
        {
            var club = w.Club(p.clubId);
            if (club == null) return new List<PlayerRelationship>();

            var room = club.dressingRoom;
            var list = new List<PlayerRelationship>();
            int age = w.Age(p);
            float ca = GameWorld.Ca(p);

            foreach (var other in w.Squad(club.id))
            {
                if (other.id == p.id) continue;

                int otherAge = w.Age(other);
                float otherCa = GameWorld.Ca(other);
                RelationshipType? type = null;
                int strength = 0;

                if (room?.captainPlayerId == other.id)
                {
                    type = RelationshipType.Captain;
                    strength = 70;
                }
                // type 은 상대방의 역할이다. 나보다 나이 많고 잘하면 그가 내 멘토,
                // 내가 나이 많고 잘하면 그가 내 멘티다.
                else if (otherAge - age >= MentorAgeGap && otherCa >= ca)
                {
                    type = RelationshipType.Mentor;
                    strength = 55 + Math.Min(20, otherAge - age);
                }
                else if (age - otherAge >= MentorAgeGap && ca >= otherCa)
                {
                    type = RelationshipType.Mentee;
                    strength = 55 + Math.Min(20, age - otherAge);
                }
                else if (SamePosition(p, other) && Math.Abs(ca - otherCa) <= RivalCaBand)
                {
                    // 같은 자리를 비슷한 실력으로 다투면 긴장이 생긴다.
                    type = RelationshipType.Rival;
                    strength = 50 + (int)(RivalCaBand - Math.Abs(ca - otherCa)) * 3;
                }
                else if (p.nationality == other.nationality)
                {
                    type = RelationshipType.NationalTeam;
                    strength = 45;
                }

                if (type is { } t)
                    list.Add(new PlayerRelationship
                    {
                        playerId = p.id,
                        targetPlayerId = other.id,
                        type = t,
                        strength = Math.Clamp(strength, 0, 100),
                        since = p.contract?.startDate ?? w.Date,
                    });
            }

            return list
                .OrderByDescending(r => r.type == RelationshipType.Captain)
                .ThenByDescending(r => r.strength)
                .ToList();
        }

        // ── 주장 ──────────────────────────────────────────────────────────

        /// <summary>
        /// 탈의실 영향력. 나이와 실력, 그리고 숨은 리더십 성향으로 본다.
        /// </summary>
        public static float Influence(GameWorld w, PlayerData p)
        {
            if (p == null) return 0f;

            float ca = GameWorld.Ca(p);
            int age = w.Age(p);

            float seniority = Math.Clamp((age - 20) / 14f, 0f, 1f);        // 20세 0 → 34세 1
            float quality = Math.Clamp((ca - 60f) / 80f, 0f, 1f);
            float leadership = (p.mental?.leadership ?? 10) / 20f;

            return (seniority * 7f + quality * 10f + leadership * 24f);
        }

        /// <summary>
        /// 가장 큰 값의 자리. 이미 뽑은 자리는 건너뛴다.
        /// 동점이면 앞에 오는 쪽을 집는다 — 내림차순 정렬의 순서와 같다.
        /// </summary>
        static int TopIndex(float[] score, int skipA = -1, int skipB = -1)
        {
            int best = -1;
            for (int i = 0; i < score.Length; i++)
            {
                if (i == skipA || i == skipB) continue;
                if (best < 0 || score[i] > score[best]) best = i;
            }
            return best;
        }


        /// <summary>주장을 직접 지명한다. 영향력이 낮은 선수를 세우면 화합도가 내려간다.</summary>
        public static void SetCaptain(GameWorld w, ClubData club, int playerId)
        {
            if (club?.dressingRoom == null) return;
            if (!club.playerIds.Contains(playerId)) return;

            club.dressingRoom.captainPlayerId = playerId;
            club.dressingRoom.leadershipStrength = Math.Clamp(
                Influence(w, w.Player(playerId)) * 2.4f, 0f, 100f);

            club.dressingRoom.overallHarmony = Math.Clamp(
                62f - club.dressingRoom.discontent * 0.75f
                    + (club.dressingRoom.leadershipStrength - 50f) * 0.20f, 0f, 100f);
        }

        /// <summary>같은 자리를 놓고 다투는지. 소화 가능한 포지션이 겹치면 경쟁자로 본다.</summary>
        static bool SamePosition(PlayerData a, PlayerData b)
            => (a.positionFlags & b.positionFlags) != 0;
    }
}
