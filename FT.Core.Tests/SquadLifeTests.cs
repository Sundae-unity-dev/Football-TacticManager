using System;
using System.Collections.Generic;
using System.Linq;
using FootballTactician.Balance;
using FootballTactician.Data;
using FootballTactician.Simulation;
using FootballTactician.World;
using Xunit;

namespace FT.Core.Tests
{
    /// <summary>
    /// 선수 개인 목표 · 이적 요청 · 국가대표.
    /// 선수가 자기 사정을 갖고 행동해야 스쿼드 관리가 결정이 된다.
    /// </summary>
    public class SquadLifeTests
    {
        static GameWorld Make(int seed = 31415, int stars = 4)
        {
            var w = WorldGenerator.Create(seed);
            var club = w.Clubs.Values.First(c => c.reputation == stars);
            ManagerCareer.TakeCharge(w, club.id, "테스트");
            return w;
        }

        static void PlaySeason(GameWorld w)
        {
            int guard = 0;
            while (!w.SeasonFinished && guard++ < 60)
                if (GameEngine.PlayNextRound(w) == null) break;
        }

        // ── 개인 목표 ─────────────────────────────────────────────────────

        [Fact]
        public void 선수마다_바라는_것이_다르다()
        {
            // 전원이 같은 것을 바라면 목표가 아니라 배경이다.
            var w = Make();
            var club = w.MyClub!;

            var sets = w.Squad(club.id)
                .Select(p => string.Join(",", PlayerAmbition.For(w, p).Select(g => g.Kind).OrderBy(k => k)))
                .ToList();

            Assert.True(sets.Distinct().Count() >= 3, "스쿼드 전원이 같은 목표를 갖는다");
        }

        [Fact]
        public void 목표는_처지에_맞는다()
        {
            var w = Make();
            var club = w.MyClub!;

            foreach (var p in w.Squad(club.id))
            {
                var goals = PlayerAmbition.For(w, p);
                Assert.NotEmpty(goals);

                // 수비수에게 득점 목표를 주면 영원히 못 이룬다.
                bool attacker = (p.positionFlags &
                    (Position.ST | Position.CF | Position.LW | Position.RW)) != 0;
                if (!attacker)
                    Assert.DoesNotContain(goals, g => g.Kind == PlayerAmbition.Kind.Goals);
            }
        }

        [Fact]
        public void 매주_뛰는_선수는_주전_목표를_갖지_않는다()
        {
            // CA 순위로 보면 자리가 비어 매주 나가는 선수까지 "주전이 못 됐다"고 불만을 품는다.
            var w = Make();
            var club = w.MyClub!;
            var tac = club.savedTactics.First(t => t.id == club.activeTacticsId);

            foreach (int id in tac.slots.Select(s => s.assignedPlayerId))
            {
                var p = w.Player(id);
                if (p == null) continue;

                Assert.DoesNotContain(PlayerAmbition.For(w, p),
                    g => g.Kind == PlayerAmbition.Kind.StarterSpot);
            }
        }

        [Fact]
        public void 시즌_초에는_좌절이_거의_없다()
        {
            // 개막하자마자 "목표를 못 이뤘다"고 하면 아무도 손쓸 수 없다.
            var w = Make();
            var club = w.MyClub!;

            foreach (var p in w.Squad(club.id))
                Assert.Equal(0f, PlayerAmbition.Frustration(w, p, 0f), 3);
        }

        [Fact]
        public void 못_이룬_목표가_많을수록_좌절이_크다()
        {
            var w = Make();
            var club = w.MyClub!;
            PlaySeason(w);

            float progress = PlayerAmbition.SeasonProgress(w);
            Assert.True(progress > 0.9f);

            var squad = w.Squad(club.id).ToList();
            var benched = squad.OrderBy(p => p.seasonStats?.appearances ?? 0).First();
            var regular = squad.OrderByDescending(p => p.seasonStats?.appearances ?? 0).First();

            Assert.True(PlayerAmbition.Frustration(w, benched, progress)
                      > PlayerAmbition.Frustration(w, regular, progress));
        }

        // ── 이적 요청 ─────────────────────────────────────────────────────

        [Fact]
        public void 방치한_선수가_이적을_요청한다()
        {
            // 불만이 행동이 되지 않으면 탈의실 화면은 읽을거리일 뿐이다.
            var w = Make(606);
            PlaySeason(w);

            Assert.NotEmpty(w.TransferRequests);

            // 요청한 선수는 대체로 못 뛴 선수여야 한다.
            var club = w.MyClub!;
            double squadAvg = w.Squad(club.id).Average(p => p.seasonStats?.appearances ?? 0);
            double requesterAvg = w.TransferRequests.Keys
                .Select(id => w.Player(id)?.seasonStats?.appearances ?? 0)
                .DefaultIfEmpty(0)
                .Average();

            Assert.True(requesterAvg < squadAvg,
                $"요청자 평균 출전 {requesterAvg:F1} ≥ 스쿼드 평균 {squadAvg:F1}");
        }

        [Fact]
        public void 한_시즌에_스쿼드_전원이_반란을_일으키지는_않는다()
        {
            var w = Make(606);
            int submitted = 0;

            int guard = 0;
            while (!w.SeasonFinished && guard++ < 60)
            {
                if (GameEngine.PlayNextRound(w) == null) break;
                submitted += w.TransferRequests.Count;
                foreach (var id in w.TransferRequests.Keys.ToList())
                    TransferRequests.Accept(w, w.Player(id)!);
            }

            int squadSize = w.Squad(w.ManagedClubId).Count();
            Assert.True(submitted < squadSize,
                $"한 시즌에 {submitted}건 — 스쿼드 {squadSize}명보다 많다");
        }

        [Fact]
        public void 받아들이면_사기가_오르고_호가가_내려간다()
        {
            var w = Make(606);
            PlaySeason(w);
            Assert.NotEmpty(w.TransferRequests);

            var p = w.Player(w.TransferRequests.Keys.First())!;
            float before = p.morale;
            long askBefore = TransferMarket.AskingPrice(w, p);

            TransferRequests.Accept(w, p);

            Assert.Equal(TransferStatus.WantAway, p.transferStatus);
            Assert.True(p.morale > before);
            Assert.True(TransferMarket.AskingPrice(w, p) < askBefore);
            Assert.DoesNotContain(p.id, w.TransferRequests.Keys);
        }

        [Fact]
        public void 거절하면_사기가_떨어지고_기록이_남는다()
        {
            var w = Make(606);
            PlaySeason(w);
            Assert.NotEmpty(w.TransferRequests);

            var p = w.Player(w.TransferRequests.Keys.First())!;
            float before = p.morale;

            TransferRequests.Reject(w, p);

            Assert.True(p.morale < before);
            Assert.DoesNotContain(p.id, w.TransferRequests.Keys);
            Assert.Equal(1, w.RejectedRequests[p.id].rejections);
        }

        [Fact]
        public void 거절당한_선수는_한동안_다시_요구하지_않는다()
        {
            var w = Make(606);
            PlaySeason(w);
            Assert.NotEmpty(w.TransferRequests);

            var p = w.Player(w.TransferRequests.Keys.First())!;
            TransferRequests.Reject(w, p);

            // 같은 날 바로 다시 올라오면 거절이 아무 의미가 없다.
            for (int i = 0; i < 20; i++)
                TransferRequests.Update(w, new Random(i));

            Assert.DoesNotContain(p.id, w.TransferRequests.Keys);
        }

        [Fact]
        public void 이미_내보내기로_한_선수는_더_조르지_않는다()
        {
            // 막지 않으면 수용 → 해제 → 재요청 고리가 돌아 요청이 끝없이 쌓인다.
            var w = Make(606);
            PlaySeason(w);
            Assert.NotEmpty(w.TransferRequests);

            foreach (var id in w.TransferRequests.Keys.ToList())
                TransferRequests.Accept(w, w.Player(id)!);

            for (int i = 0; i < 20; i++)
                TransferRequests.Update(w, new Random(i));

            Assert.All(w.TransferRequests.Keys,
                id => Assert.NotEqual(TransferStatus.WantAway, w.Player(id)!.transferStatus));
        }

        [Fact]
        public void 임대_중인_선수는_요청하지_않는다()
        {
            var w = Make(515253, stars: 5);
            var parent = w.MyClub!;

            var target = LoanMarket.Loanable(w, parent.id)
                .FirstOrDefault(x => LoanMarket.Suitors(w, x, 1).Count > 0);
            if (target == null) return;

            LoanMarket.Send(w, target, LoanMarket.Suitors(w, target).First());
            PlaySeason(w);

            Assert.DoesNotContain(target.id, w.TransferRequests.Keys);
        }

        [Fact]
        public void 팔리면_요청도_사라진다()
        {
            var w = Make(606);
            PlaySeason(w);
            Assert.NotEmpty(w.TransferRequests);

            var p = w.Player(w.TransferRequests.Keys.First())!;
            SeasonRollover.Release(w, w.MyClub!, p);

            Assert.DoesNotContain(p.id, w.TransferRequests.Keys);
        }

        [Fact]
        public void 시즌이_바뀌면_묵은_요청이_정리된다()
        {
            var w = Make(606);
            PlaySeason(w);
            SeasonRollover.StartNextSeason(w);

            Assert.Empty(w.TransferRequests);
            Assert.Empty(w.RejectedRequests);
        }

        // ── 국가대표 ──────────────────────────────────────────────────────

        [Fact]
        public void 대표는_잘하는_선수에게_간다()
        {
            var w = Make();
            PlaySeason(w);

            var capped = w.Players.Values.Where(p => p.clubId > 0 && p.caps > 0).ToList();
            var uncapped = w.Players.Values.Where(p => p.clubId > 0 && p.caps == 0).ToList();

            Assert.NotEmpty(capped);
            Assert.True(capped.Average(GameWorld.Ca) > uncapped.Average(GameWorld.Ca) + 20f,
                "대표 선수와 비대표 선수의 실력 차가 뚜렷하지 않다");
        }

        [Fact]
        public void 나라마다_정해진_인원만_뽑는다()
        {
            var w = Make();

            foreach (var nation in w.Nations)
            {
                var code = NameForge.ByCode(nation.Code).Nationality;
                var squad = NationalTeam.Squad(w, code);

                Assert.Equal(BalanceConfig.NationalSquadSize, squad.Count);
                Assert.Equal(squad.Count, squad.Distinct().Count());
                Assert.All(squad, p => Assert.Equal(code, p.nationality));

                // 골키퍼 없는 대표팀이 나오면 안 된다.
                Assert.True(squad.Count(p => (p.positionFlags & Position.GK) != 0) >= 1);
            }
        }

        [Fact]
        public void 소집은_체력을_쓴다()
        {
            var w = Make();
            var code = NameForge.ByCode(w.Nations[0].Code).Nationality;
            var squad = NationalTeam.Squad(w, code);
            var before = squad.ToDictionary(p => p.id, p => p.condition);

            NationalTeam.RunBreak(w, new Random(7));

            foreach (var p in squad)
                Assert.True(p.condition < before[p.id],
                    $"{p.FullName} 이 A매치를 치르고도 멀쩡하다");
        }

        [Fact]
        public void 소집되지_않은_선수는_영향이_없다()
        {
            var w = Make();
            var code = NameForge.ByCode(w.Nations[0].Code).Nationality;
            var called = NationalTeam.Squad(w, code).Select(p => p.id).ToHashSet();

            var bystander = w.Players.Values.First(p =>
                p.clubId > 0 && p.nationality == code && !called.Contains(p.id));
            float before = bystander.condition;

            NationalTeam.RunBreak(w, new Random(7));

            Assert.Equal(before, bystander.condition, 3);
            Assert.Equal(0, bystander.caps);
        }

        [Fact]
        public void 대표_경력은_몸값에_붙는다()
        {
            var plain = new PlayerData { caps = 0 };
            var capped = new PlayerData { caps = BalanceConfig.CapValueCeiling };

            Assert.Equal(1f, NationalTeam.ValueBonus(plain), 3);
            Assert.Equal(1f + BalanceConfig.CapValueBonus, NationalTeam.ValueBonus(capped), 3);

            // 한도를 넘겨도 더 붙지 않는다.
            Assert.Equal(NationalTeam.ValueBonus(capped),
                NationalTeam.ValueBonus(new PlayerData { caps = 999 }), 3);
        }

        [Fact]
        public void A매치는_정해진_주차에만_열린다()
        {
            var w = Make();
            int before = w.Players.Values.Sum(p => p.caps);

            // A매치 주간이 아닌 라운드에서는 출전 수가 늘지 않아야 한다.
            Assert.False(NationalTeam.IsBreak(1));
            Assert.True(NationalTeam.IsBreak(NationalTeam.BreakMatchdays[0]));

            GameEngine.PlayNextRound(w);
            Assert.Equal(before, w.Players.Values.Sum(p => p.caps));
        }

        // ── 세이브 ────────────────────────────────────────────────────────

        [Fact]
        public void 요청과_대표_기록이_세이브를_왕복한다()
        {
            var w = Make(606);
            PlaySeason(w);
            Assert.NotEmpty(w.TransferRequests);

            var requested = w.TransferRequests.Keys.First();
            var rejectTarget = w.Player(w.TransferRequests.Keys.Last())!;
            TransferRequests.Reject(w, rejectTarget);

            var capped = w.Players.Values.First(p => p.caps > 0);
            int caps = capped.caps;

            var back = SaveGame.Deserialize(SaveGame.Serialize(w, "스쿼드")).world!;

            Assert.Contains(requested, back.TransferRequests.Keys);
            Assert.Equal(w.TransferRequests[requested].reason, back.TransferRequests[requested].reason);
            Assert.Contains(rejectTarget.id, back.RejectedRequests.Keys);
            Assert.Equal(1, back.RejectedRequests[rejectTarget.id].rejections);
            Assert.Equal(caps, back.Player(capped.id)!.caps);
        }
    }
}
