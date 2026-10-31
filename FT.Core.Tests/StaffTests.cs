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
    /// 스태프 — 네 갈래가 저마다 다른 것을 바꾸고, 대가가 있고, 기준선이 중립인지.
    /// </summary>
    public class StaffTests
    {
        static GameWorld Make(int seed = 31415, int stars = 4)
        {
            var w = WorldGenerator.Create(seed);
            var club = w.Clubs.Values.First(c => c.reputation == stars);
            ManagerCareer.TakeCharge(w, club.id, "테스트");
            return w;
        }

        // ── 생성 ──────────────────────────────────────────────────────────

        [Fact]
        public void 모든_구단이_스태프를_갖는다()
        {
            var w = WorldGenerator.Create(7);
            Assert.NotEmpty(w.Staff);
            Assert.All(w.Clubs.Values, c => Assert.NotEmpty(StaffRoom.Of(w, c.id)));
        }

        [Fact]
        public void 작은_구단도_네_갈래를_모두_갖는다()
        {
            // 코치부터 줄 세우면 작은 구단에 의료진이 통째로 없어 부상이 25% 많아진다.
            var w = WorldGenerator.Create(7);

            foreach (var c in w.Clubs.Values)
            {
                var roles = StaffRoom.Of(w, c.id).Select(s => s.role).ToList();
                Assert.Contains(roles, StaffRoom.IsCoach);
                Assert.Contains(roles, StaffRoom.IsScout);
                Assert.Contains(roles, StaffRoom.IsMedical);
                Assert.Contains(roles, StaffRoom.IsAnalyst);
            }
        }

        [Fact]
        public void 역할에_맞는_능력만_채워진다()
        {
            // 다 채우면 역할 구분이 장식이 된다.
            var w = WorldGenerator.Create(7);

            foreach (var s in w.Staff.Values)
            {
                Assert.NotNull(s.common);
                Assert.Equal(StaffRoom.IsCoach(s.role), s.coaching != null);
                Assert.Equal(StaffRoom.IsScout(s.role), s.scouting != null);
                Assert.Equal(StaffRoom.IsMedical(s.role), s.medical != null);
                Assert.Equal(StaffRoom.IsAnalyst(s.role), s.analytics != null);
            }
        }

        [Fact]
        public void 자유_스태프가_역할마다_있다()
        {
            // 한 역할만 비면 그 자리를 영영 못 채운다.
            var w = WorldGenerator.Create(7);

            foreach (StaffRole r in Enum.GetValues(typeof(StaffRole)))
                Assert.NotEmpty(StaffRoom.FreeAgents(w, r));
        }

        // ── 기준선 ────────────────────────────────────────────────────────

        [Fact]
        public void 평판대로_갖추면_힘이_1_언저리다()
        {
            // 기준을 고정하면 큰 구단이 네 갈래 모두에서 앞서고 스태프 운용이 평판의 그림자가 된다.
            var w = WorldGenerator.Create(7);

            foreach (int stars in new[] { 3, 4, 5 })
            {
                var clubs = w.Clubs.Values.Where(c => c.reputation == stars).ToList();
                if (clubs.Count == 0) continue;

                foreach (var (name, f) in new (string, Func<ClubData, float>)[]
                         {
                             ("코칭", c => StaffRoom.Coaching(w, c.id)),
                             ("스카우팅", c => StaffRoom.ScoutingPower(w, c.id)),
                             ("의료", c => StaffRoom.Medical(w, c.id)),
                             ("분석", c => StaffRoom.Analytics(w, c.id)),
                         })
                {
                    float avg = clubs.Average(f);
                    Assert.True(avg > 0.75f && avg < 1.30f,
                        $"{stars}성 {name} 평균이 {avg:F2} — 기준선이 밀렸다");
                }
            }
        }

        [Fact]
        public void 세계의_부상_기준선이_중립이다()
        {
            // 스태프를 넣었다고 세계 전체의 부상이 늘거나 줄면 안 된다.
            var w = WorldGenerator.Create(7);
            float avg = w.Clubs.Values.Average(c => StaffRoom.InjuryFactor(w, c.id));

            Assert.True(avg > 0.95f && avg < 1.06f, $"부상 배수 평균 {avg:F3} — 기준선이 밀렸다");
        }

        [Fact]
        public void 갈래끼리_따로_움직인다()
        {
            // 하나가 좋으면 다 좋다면 갈래를 나눈 뜻이 없다.
            var w = WorldGenerator.Create(7);
            var all = w.Clubs.Values.ToList();

            double Corr(Func<ClubData, double> a, Func<ClubData, double> b)
            {
                double ma = all.Average(a), mb = all.Average(b);
                double cov = all.Sum(c => (a(c) - ma) * (b(c) - mb));
                double sa = Math.Sqrt(all.Sum(c => Math.Pow(a(c) - ma, 2)));
                double sb = Math.Sqrt(all.Sum(c => Math.Pow(b(c) - mb, 2)));
                return sa * sb == 0 ? 0 : cov / (sa * sb);
            }

            Assert.True(Corr(c => StaffRoom.Coaching(w, c.id), c => StaffRoom.Medical(w, c.id)) < 0.6,
                "코칭과 의료가 같이 움직인다");
            Assert.True(Corr(c => StaffRoom.Coaching(w, c.id), c => StaffRoom.ScoutingPower(w, c.id)) < 0.6,
                "코칭과 스카우팅이 같이 움직인다");
        }

        [Fact]
        public void 머릿수보다_질이다()
        {
            // 평범한 코치를 쌓는 쪽이 낫다면 영입이 숫자 싸움이 된다.
            var w = Make();
            var club = w.MyClub!;
            club.finances.weeklyWageBudget = int.MaxValue / 2;

            void Clear()
            {
                foreach (int id in club.staffIds.ToList()) StaffRoom.Release(w, club, w.Staff[id]);
            }

            Clear();
            Assert.True(StaffRoom.Hire(w, club, Fake(w, StaffRole.HeadCoach, 5)));
            float good = StaffRoom.Coaching(w, club.id);

            Clear();
            Assert.True(StaffRoom.Hire(w, club, Fake(w, StaffRole.HeadCoach, 2)));
            Assert.True(StaffRoom.Hire(w, club, Fake(w, StaffRole.AssistantCoach, 2)));
            Assert.True(StaffRoom.Hire(w, club, Fake(w, StaffRole.PhysicalCoach, 2)));
            float many = StaffRoom.Coaching(w, club.id);

            Assert.True(good > many, $"좋은 하나({good:F2})보다 평범한 셋({many:F2})이 낫다");
        }

        static int _fakeId = 900000;

        static StaffData Fake(GameWorld w, StaffRole role, int rep)
        {
            int v = 4 + rep * 3;
            var s = new StaffData
            {
                id = _fakeId++,
                firstName = "테스트",
                lastName = $"{role}{rep}",
                age = 40,
                role = role,
                reputation = rep,
                common = new StaffAttributesCommon { motivation = v, manManagement = v },
            };

            if (StaffRoom.IsCoach(role))
                s.coaching = new CoachingAttributes
                {
                    tacticalKnowledge = v, technicalCoaching = v,
                    physicalCoaching = v, mentalCoaching = v, gkCoaching = v,
                };
            if (StaffRoom.IsScout(role))
                s.scouting = new ScoutingAttributes
                {
                    judgingPlayerAbility = v, judgingPlayerPotential = v, adaptability = v,
                };
            if (StaffRoom.IsMedical(role))
                s.medical = new MedicalAttributes { physioTherapy = v, sportsScience = v };
            if (StaffRoom.IsAnalyst(role))
                s.analytics = new AnalyticsAttributes { dataAnalysis = v };

            w.Staff[s.id] = s;
            return s;
        }

        // ── 대가 ──────────────────────────────────────────────────────────

        [Fact]
        public void 주급이_선수와_같은_지갑에서_나간다()
        {
            // 따로 떼어 놓으면 좋은 스태프를 안 쓸 이유가 없어진다.
            var w = Make();
            var club = w.MyClub!;
            int before = club.finances.currentWageSpend;

            // 자리가 비어 있는 역할을 고른다. 꽉 찬 역할이면 고용 자체가 막힌다.
            club.finances.weeklyWageBudget = int.MaxValue / 2;
            foreach (int id in club.staffIds.Where(i => w.Staff[i].role == StaffRole.RegionalScout).ToList())
                StaffRoom.Release(w, club, w.Staff[id]);

            before = club.finances.currentWageSpend;

            var s = StaffRoom.FreeAgents(w, StaffRole.RegionalScout).First();
            Assert.True(StaffRoom.Hire(w, club, s));

            Assert.Equal(before + StaffRoom.WeeklyWage(s), club.finances.currentWageSpend);

            StaffRoom.Release(w, club, s);
            Assert.Equal(before, club.finances.currentWageSpend);
        }

        [Fact]
        public void 주급_여유가_없으면_못_데려온다()
        {
            var w = Make();
            var club = w.MyClub!;
            club.finances.weeklyWageBudget = club.finances.currentWageSpend;

            var s = StaffRoom.FreeAgents(w, StaffRole.RegionalScout).First();
            Assert.Equal(StaffRoom.HireVerdict.NoBudget, StaffRoom.CanHire(w, club, s));
            Assert.False(StaffRoom.Hire(w, club, s));
        }

        [Fact]
        public void 한_역할에_정원이_있다()
        {
            // 없으면 코치를 무한히 쌓는다.
            var w = Make();
            var club = w.MyClub!;
            club.finances.weeklyWageBudget = int.MaxValue / 2;

            int slots = StaffRoom.SlotsFor(StaffRole.HeadCoach);
            foreach (int id in club.staffIds.Where(i => w.Staff[i].role == StaffRole.HeadCoach).ToList())
                StaffRoom.Release(w, club, w.Staff[id]);

            var pool = StaffRoom.FreeAgents(w, StaffRole.HeadCoach).ToList();
            for (int i = 0; i < slots; i++) Assert.True(StaffRoom.Hire(w, club, pool[i]));

            Assert.Equal(StaffRoom.HireVerdict.RoleFull, StaffRoom.CanHire(w, club, pool[slots]));
        }

        [Fact]
        public void 남의_스태프는_데려올_수_없다()
        {
            var w = Make();
            var club = w.MyClub!;
            var other = w.Clubs.Values.First(c => c.id != club.id && c.staffIds.Count > 0);
            var theirs = w.Staff[other.staffIds[0]];

            Assert.Equal(StaffRoom.HireVerdict.NotAvailable, StaffRoom.CanHire(w, club, theirs));
        }

        // ── 효과 ──────────────────────────────────────────────────────────

        [Fact]
        public void 코치가_없으면_성장이_느려진다()
        {
            var w = Make();
            var club = w.MyClub!;
            float before = StaffRoom.CoachingFactor(w, club.id);

            foreach (int id in club.staffIds.Where(i => StaffRoom.IsCoach(w.Staff[i].role)).ToList())
                StaffRoom.Release(w, club, w.Staff[id]);

            Assert.True(StaffRoom.CoachingFactor(w, club.id) < before);
            Assert.Equal(0f, StaffRoom.Coaching(w, club.id), 3);
        }

        [Fact]
        public void 의료진이_좋으면_덜_다치고_빨리_돌아온다()
        {
            var w = Make();
            var club = w.MyClub!;
            club.finances.weeklyWageBudget = int.MaxValue / 2;

            foreach (int id in club.staffIds.Where(i => StaffRoom.IsMedical(w.Staff[i].role)).ToList())
                StaffRoom.Release(w, club, w.Staff[id]);

            float bare = StaffRoom.InjuryFactor(w, club.id);

            StaffRoom.Hire(w, club, Fake(w, StaffRole.TeamDoctor, 5));
            StaffRoom.Hire(w, club, Fake(w, StaffRole.Physio, 5));

            Assert.True(StaffRoom.InjuryFactor(w, club.id) < bare);
            Assert.True(StaffRoom.RecoveryFactor(w, club.id) < 1f);
        }

        [Fact]
        public void 스카우트가_좋으면_보고서가_더_알려_준다()
        {
            var w = Make();
            var club = w.MyClub!;
            club.finances.weeklyWageBudget = int.MaxValue / 2;

            foreach (int id in club.staffIds.Where(i => StaffRoom.IsScout(w.Staff[i].role)).ToList())
                StaffRoom.Release(w, club, w.Staff[id]);

            float bare = StaffRoom.ReportValue(w, club.id);

            StaffRoom.Hire(w, club, Fake(w, StaffRole.HeadScout, 5));
            StaffRoom.Hire(w, club, Fake(w, StaffRole.RegionalScout, 5));

            Assert.True(StaffRoom.ReportValue(w, club.id) > bare);
            Assert.True(StaffRoom.ExtraScoutTrips(w, club.id) > 0);
        }

        [Fact]
        public void 좋은_스카우트면_관찰_횟수가_줄어든다()
        {
            // 보고서 한 장이 더 많이 알려 주면 다 보는 데 덜 걸린다.
            var w = Make();
            var club = w.MyClub!;
            club.finances.weeklyWageBudget = int.MaxValue / 2;

            var target = w.Players.Values.First(p =>
                p.clubId > 0 && p.caps == 0 && Scouting.Knowledge(w, p) < 0.1f);

            foreach (int id in club.staffIds.Where(i => StaffRoom.IsScout(w.Staff[i].role)).ToList())
                StaffRoom.Release(w, club, w.Staff[id]);
            int bare = Scouting.ReportsNeeded(w, target);

            StaffRoom.Hire(w, club, Fake(w, StaffRole.HeadScout, 5));
            StaffRoom.Hire(w, club, Fake(w, StaffRole.RegionalScout, 5));

            Assert.True(Scouting.ReportsNeeded(w, target) < bare);
        }

        [Fact]
        public void 분석가가_없으면_상대를_읽지_못한다()
        {
            var w = Make();
            var club = w.MyClub!;

            foreach (int id in club.staffIds.Where(i => StaffRoom.IsAnalyst(w.Staff[i].role)).ToList())
                StaffRoom.Release(w, club, w.Staff[id]);

            Assert.False(StaffRoom.ReadsOpponentStyle(w, club.id));

            club.finances.weeklyWageBudget = int.MaxValue / 2;
            StaffRoom.Hire(w, club, Fake(w, StaffRole.DataAnalyst, 5));
            Assert.True(StaffRoom.ReadsOpponentStyle(w, club.id));
        }

        [Fact]
        public void 형편없는_분석가로는_모자란다()
        {
            // 문턱이 늘 열려 있으면 분석가를 두는 결정이 없다.
            var w = Make(31415, stars: 5);
            var club = w.MyClub!;
            club.finances.weeklyWageBudget = int.MaxValue / 2;

            foreach (int id in club.staffIds.Where(i => StaffRoom.IsAnalyst(w.Staff[i].role)).ToList())
                StaffRoom.Release(w, club, w.Staff[id]);

            StaffRoom.Hire(w, club, Fake(w, StaffRole.DataAnalyst, 1));
            Assert.False(StaffRoom.ReadsOpponentStyle(w, club.id));
        }

        // ── 저장 ──────────────────────────────────────────────────────────

        [Fact]
        public void 스태프가_세이브를_왕복한다()
        {
            var w = Make();
            var back = SaveGame.Deserialize(SaveGame.Serialize(w, "스태프")).world!;

            Assert.Equal(w.Staff.Count, back.Staff.Count);

            foreach (var (id, s) in w.Staff)
            {
                var b = back.Staff[id];
                Assert.Equal(s.FullName, b.FullName);
                Assert.Equal(s.role, b.role);
                Assert.Equal(s.reputation, b.reputation);
                Assert.Equal(s.clubId, b.clubId);
                Assert.Equal(s.common.motivation, b.common.motivation);
                Assert.Equal(StaffRoom.WeeklyWage(s), StaffRoom.WeeklyWage(b));

                if (StaffRoom.IsCoach(s.role))
                    Assert.Equal(s.coaching.tacticalKnowledge, b.coaching.tacticalKnowledge);
                if (StaffRoom.IsAnalyst(s.role))
                    Assert.Equal(s.analytics.dataAnalysis, b.analytics.dataAnalysis);
            }
        }

        [Fact]
        public void 네_갈래의_힘이_세이브를_건너도_같다()
        {
            var w = Make();
            var back = SaveGame.Deserialize(SaveGame.Serialize(w, "스태프")).world!;
            int id = w.ManagedClubId;

            Assert.Equal(StaffRoom.Coaching(w, id), StaffRoom.Coaching(back, id), 3);
            Assert.Equal(StaffRoom.ScoutingPower(w, id), StaffRoom.ScoutingPower(back, id), 3);
            Assert.Equal(StaffRoom.Medical(w, id), StaffRoom.Medical(back, id), 3);
            Assert.Equal(StaffRoom.Analytics(w, id), StaffRoom.Analytics(back, id), 3);
        }
    }
}
