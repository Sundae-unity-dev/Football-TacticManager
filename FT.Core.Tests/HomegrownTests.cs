using System;
using System.Collections.Generic;
using System.Linq;
using FootballTactician.Balance;
using FootballTactician.Data;
using FootballTactician.World;
using Xunit;

namespace FT.Core.Tests
{
    /// <summary>
    /// 홈그로운 규정.
    ///
    /// <c>HomegrownStatus</c> 와 <c>LeagueRules.homegrownPlayersMin</c> 은 처음부터 있었지만
    /// 값이 채워지기만 하고 아무도 읽지 않았다. 규정이 없으면 돈만 있으면 스쿼드를 다 사 올 수 있고,
    /// 유스 아카데미가 순수한 손해가 된다.
    /// </summary>
    public class HomegrownTests
    {
        static GameWorld Make(int seed = 31415, int stars = 4)
        {
            var w = WorldGenerator.Create(seed);
            var club = w.Clubs.Values.First(c => c.reputation == stars);
            ManagerCareer.TakeCharge(w, club.id, "테스트");
            return w;
        }

        static void Seasons(GameWorld w, int n)
        {
            for (int s = 0; s < n; s++)
            {
                int guard = 0;
                while (!w.SeasonFinished && guard++ < 60)
                    if (GameEngine.PlayNextRound(w) == null) break;
                SeasonRollover.StartNextSeason(w);
            }
        }

        // ── 나라 찾기 ─────────────────────────────────────────────────────

        [Fact]
        public void 하부_리그_구단도_나라가_있다()
        {
            // NationOf 가 1부만 보고 있어 2·3부 구단은 나라가 없었다 — 3부를 넣을 때 놓친 자리다.
            var w = WorldGenerator.Create(7);

            Assert.All(w.Clubs.Values, c =>
                Assert.False(string.IsNullOrEmpty(w.NationOf(c.id)?.Country),
                    $"{c.name} 에 나라가 없다"));
        }

        // ── 기준선 ────────────────────────────────────────────────────────

        [Fact]
        public void 세계가_규정을_거의_지키며_시작한다()
        {
            // 심지 않으면 240구단이 전부 0명으로 시작해 이적 시장이 통째로 얼어붙는다.
            var w = WorldGenerator.Create(7);

            int short_ = w.Clubs.Values.Count(c => !Homegrown.Meets(w, c.id));
            Assert.True(short_ < w.Clubs.Count / 4,
                $"{short_} / {w.Clubs.Count} 구단이 규정 미달로 시작한다");
        }

        [Fact]
        public void 규정이_물리기는_한다()
        {
            // 아무도 걸리지 않는 규정은 규정이 아니다.
            var w = WorldGenerator.Create(7);
            Assert.Contains(w.Clubs.Values, c => !Homegrown.Meets(w, c.id));
        }

        [Fact]
        public void 이적_시장이_얼지_않는다()
        {
            var w = Make(606);
            int before = w.Transfers.Count;
            Seasons(w, 3);

            Assert.True(w.Transfers.Count > before,
                "규정을 넣은 뒤 세 시즌 동안 이적이 한 건도 없다");
        }

        // ── 규정 ──────────────────────────────────────────────────────────

        [Fact]
        public void 모자라면_외부_영입이_막힌다()
        {
            var w = Make();
            var club = w.MyClub!;

            foreach (var p in w.Squad(club.id)) p.homegrown.isClubAcademy = false;

            var outsider = w.Players.Values.First(p =>
                p.clubId > 0 && p.clubId != club.id && !Homegrown.IsHomegrown(p));

            Assert.False(Homegrown.Meets(w, club.id));
            Assert.False(Homegrown.CanSign(w, club.id, outsider));
        }

        [Fact]
        public void 모자라도_유스_출신은_데려올_수_있다()
        {
            // 막으면 규정을 지킬 길 자체가 막힌다.
            var w = Make();
            var club = w.MyClub!;
            foreach (var p in w.Squad(club.id)) p.homegrown.isClubAcademy = false;

            var insider = w.Players.Values.First(p =>
                p.clubId > 0 && p.clubId != club.id && Homegrown.IsHomegrown(p));

            Assert.True(Homegrown.CanSign(w, club.id, insider));
        }

        [Fact]
        public void 지키고_있으면_누구든_데려올_수_있다()
        {
            var w = Make();
            var club = w.MyClub!;
            Assert.True(Homegrown.Meets(w, club.id));

            var anyone = w.Players.Values.First(p => p.clubId > 0 && p.clubId != club.id);
            Assert.True(Homegrown.CanSign(w, club.id, anyone));
        }

        [Fact]
        public void 마지막_유스_출신은_팔_수_없다()
        {
            // 팔아 치우면 다음 영입이 통째로 막힌다.
            var w = Make();
            var club = w.MyClub!;

            var academy = w.Squad(club.id).Where(Homegrown.IsHomegrown).ToList();
            int need = Homegrown.RequiredFor(w, club.id);
            Assert.True(academy.Count >= need);

            // 딱 정원만큼만 남긴다.
            foreach (var p in academy.Skip(need)) p.homegrown.isClubAcademy = false;

            Assert.False(Homegrown.CanSell(w, club.id, academy[0]));

            var other = w.Squad(club.id).First(p => !Homegrown.IsHomegrown(p));
            Assert.True(Homegrown.CanSell(w, club.id, other));
        }

        // ── 자격 ──────────────────────────────────────────────────────────

        [Fact]
        public void 유스_승격생은_처음부터_자격이_있다()
        {
            var w = Make(606);
            var club = w.MyClub!;
            int before = w.Squad(club.id).Count(Homegrown.IsHomegrown);

            Seasons(w, 2);

            Assert.True(w.Squad(club.id).Count(Homegrown.IsHomegrown) > 0);
            Assert.True(w.Players.Values.Count(Homegrown.IsHomegrown) > before);
        }

        [Fact]
        public void 구단을_옮기면_구단_햇수만_사라진다()
        {
            var w = Make();
            var p = w.Squad(w.ManagedClubId).First(Homegrown.IsHomegrown);
            var from = w.MyClub!;
            var to = w.Clubs.Values.First(c =>
                c.id != from.id && w.NationOf(c.id)?.Code == w.NationOf(from.id)?.Code);

            int national = p.homegrown.yearsInNationalLeague;
            Homegrown.OnTransfer(w, p, from, to);

            Assert.Equal(0, p.homegrown.yearsAtCurrentClub);
            Assert.False(p.homegrown.isClubAcademy);
            Assert.Equal(national, p.homegrown.yearsInNationalLeague);   // 같은 나라면 따라간다
        }

        [Fact]
        public void 다른_나라로_가면_자국_햇수도_사라진다()
        {
            var w = Make();
            var p = w.Squad(w.ManagedClubId).First();
            var from = w.MyClub!;
            var to = w.Clubs.Values.First(c => w.NationOf(c.id)?.Code != w.NationOf(from.id)?.Code);

            p.homegrown.yearsInNationalLeague = 5;
            Homegrown.OnTransfer(w, p, from, to);

            Assert.Equal(0, p.homegrown.yearsInNationalLeague);
        }

        [Fact]
        public void 나이_든_선수는_햇수가_쌓이지_않는다()
        {
            // 서른에 와서 3년을 채우면 홈그로운이 되는 것은 아니다.
            var w = Make();
            var p = w.Squad(w.ManagedClubId).First();
            p.dateOfBirth = new GameDate(w.Date.year - 30, 1, 1);
            p.homegrown = new HomegrownStatus();

            Homegrown.AdvanceYear(w);

            Assert.Equal(0, p.homegrown.yearsAtCurrentClub);
        }

        [Fact]
        public void 한_해가_지나면_햇수가_는다()
        {
            var w = Make();
            var p = w.Squad(w.ManagedClubId).First(x => w.Age(x) <= BalanceConfig.HomegrownMaxAge);
            int before = p.homegrown.yearsAtCurrentClub;

            Homegrown.AdvanceYear(w);

            Assert.Equal(before + 1, p.homegrown.yearsAtCurrentClub);
        }

        [Fact]
        public void 홈그로운이_세이브를_왕복한다()
        {
            var w = Make(606);
            Seasons(w, 2);

            var back = SaveGame.Deserialize(SaveGame.Serialize(w, "홈그로운")).world!;

            foreach (var (id, p) in w.Players)
            {
                var b = back.Players[id];
                Assert.Equal(p.homegrown?.isClubAcademy ?? false, b.homegrown?.isClubAcademy ?? false);
                Assert.Equal(p.homegrown?.yearsAtCurrentClub ?? 0, b.homegrown?.yearsAtCurrentClub ?? 0);
                Assert.Equal(p.homegrown?.yearsInNationalLeague ?? 0, b.homegrown?.yearsInNationalLeague ?? 0);
            }

            Assert.Equal(Homegrown.CountIn(w, w.ManagedClubId),
                         Homegrown.CountIn(back, back.ManagedClubId));
        }

        [Fact]
        public void 실제_이적에서도_구단_햇수가_끊긴다()
        {
            // 함수만 만들어 두고 호출부에 연결하지 않으면 테스트는 통과하고 게임은 틀린다 —
            // 실제로 OnTransfer 가 TransferMarket 에 연결돼 있지 않았다.
            var w = Make(606);
            Seasons(w, 3);

            // 이적으로 들어온 선수는 그 구단 유스 출신일 수 없다.
            foreach (var t in w.Transfers.TakeLast(40))
            {
                var p = w.Player(t.playerId);
                if (p == null || p.clubId <= 0) continue;
                if (p.homegrown?.isClubAcademy != true) continue;

                // 아카데미 자격이 살아 있다면 이적 뒤에 3년을 더 머문 경우뿐이다.
                Assert.True(p.homegrown.yearsAtCurrentClub >= BalanceConfig.HomegrownYears,
                    $"{p.FullName} 이 이적했는데 구단 햇수가 {p.homegrown.yearsAtCurrentClub} 이다");
            }
        }
    }
}
