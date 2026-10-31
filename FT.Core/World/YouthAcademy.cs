using System;
using System.Collections.Generic;
using System.Linq;
using FootballTactician.Balance;
using FootballTactician.Data;

namespace FootballTactician.World
{
    /// <summary>
    /// 유스 승격. 시즌마다 아카데미에서 새 얼굴이 올라온다.
    ///
    /// 인원이 모자랄 때만 올라오면 유스는 땜질 수단일 뿐이다. 매 시즌 정기적으로 올라와야
    /// 아카데미 등급과 유스 훈련 중점이 의미를 가진다.
    /// </summary>
    public static class YouthAcademy
    {
        /// <summary>아카데미 등급별 승격 인원.</summary>
        public static int IntakeSize(ClubData club, Random rng)
        {
            // 연 700명이 들어오면 선수 6,000명 세계의 평균 생애가 9년이 되고,
            // 열여덟에 들어와 스물일곱에 사라진다 — 세계가 유소년 리그가 된다.
            // 은퇴를 넣기 전에는 노인이 쌓여 이 불균형이 가려져 있었다.
            int baseCount = club.academyRating switch
            {
                5 => 3,
                4 => 2,
                3 => 2,
                2 => 1,
                _ => 1,
            };
            return baseCount + (rng.Next(100) < 30 ? 1 : 0);
        }

        /// <summary>
        /// 이번 기수에서 가장 뛰어난 선수의 잠재력. 아카데미가 좋을수록 드물게 큰 재능이 나온다.
        /// </summary>
        public static float GemChance(ClubData club) => club.academyRating switch
        {
            5 => 0.22f,
            4 => 0.15f,
            3 => 0.09f,
            2 => 0.05f,
            _ => 0.03f,
        };

        /// <summary>한 시즌치 유스를 올린다. 올라온 선수를 돌려준다.</summary>
        public static List<PlayerData> RunIntake(GameWorld w, ClubData club, Random rng)
        {
            var produced = new List<PlayerData>();

            // 인원이 이미 넘치면 올리지 않는다 — 스쿼드가 부풀어 오른다.
            if (club.playerIds.Count >= BalanceConfig.SquadMax) return produced;

            var nation = NationNamesFor(w, club.id);
            float rep = w.RepCoefOf(club.id);
            int count = Math.Min(IntakeSize(club, rng),
                                 BalanceConfig.SquadMax - club.playerIds.Count);

            for (int i = 0; i < count; i++)
            {
                var pos = IntakePosition(rng);
                var p = WorldGenerator.CreateYouthPlayer(
                    rng, w.NextPlayerId++, club, nation, rep, pos, w.Date);

                // 드물게 또래보다 훨씬 나은 재능이 나온다. 유스를 지켜볼 이유가 된다.
                if (rng.NextDouble() < GemChance(club))
                {
                    p.potential = Math.Clamp((int)(p.potential * 1.35f), 1, 99);
                    p.hidden.professionalism = Math.Min(20, p.hidden.professionalism + 4);
                    p.hidden.ambition = Math.Min(20, p.hidden.ambition + 4);
                }

                Homegrown.MarkAcademyGraduate(p);
                w.Players[p.id] = p;
                club.playerIds.Add(p.id);
                produced.Add(p);
            }

            TransferMarket.RecalcWages(w, club);
            return produced;
        }

        static readonly Position[] IntakeShape =
        {
            Position.GK, Position.CB, Position.CB, Position.LB, Position.RB,
            Position.CDM, Position.CM, Position.CM, Position.CAM,
            Position.LW, Position.RW, Position.ST, Position.ST,
        };

        static Position IntakePosition(Random rng) => IntakeShape[rng.Next(IntakeShape.Length)];

        static NationNames NationNamesFor(GameWorld w, int clubId)
        {
            var nation = w.NationOf(clubId);
            return nation == null ? NameForge.All[0] : NameForge.ByCode(nation.Code);
        }

        /// <summary>모든 구단의 유스를 올리고, 플레이어 구단 결과를 알린다.</summary>
        public static void RunAll(GameWorld w, Random rng)
        {
            foreach (var club in w.Clubs.Values)
            {
                var produced = RunIntake(w, club, rng);
                if (club.id != w.ManagedClubId || produced.Count == 0) continue;

                var best = produced.OrderByDescending(PlayerDevelopment.Pa).First();
                string note = PlayerDevelopment.Pa(best) >= 150
                    ? " 그중 한 명은 눈여겨볼 만합니다."
                    : "";

                w.Post("유스 승격",
                       $"아카데미에서 {produced.Count}명이 1군에 합류했습니다. " +
                       $"가장 기대되는 선수는 {best.FullName} ({w.Age(best)}세, " +
                       $"잠재력 {Math.Round(PlayerDevelopment.Pa(best))}) 입니다.{note}",
                       InboxKind.News);
            }
        }
    }
}
