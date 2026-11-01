using System.Linq;
using FootballTactician.Data;

namespace FootballTactician.World
{
    /// <summary>
    /// "이 구단이 한 주에 내보내는 급여는 얼마인가" — 이 물음의 답이 있는 **한 곳**이다.
    ///
    /// 예전에는 답이 두 가지였다. 스태프를 고용하는 쪽은 급여 합계에 스태프 주급을
    /// <b>더했고</b>(StaffForge · StaffRoom), 다시 세는 쪽은 <b>선수만 세어 덮어썼다</b>
    /// (TransferMarket.RecalcWages · WeekCycle.PayWages). 그래서 이적이나 재계약을
    /// 한 번 하면 스태프 몫이 장부에서 통째로 사라졌다 — 3부 구단 하나에서 주당
    /// 185K€ 가 164K€ 로 떨어지고 급여 비중이 71% 에서 63% 로 내려앉았다.
    ///
    /// 더 나쁜 것은 <b>스태프 주급이 실제로 나가지 않았다</b>는 점이다. 주간 진행이
    /// 선수 급여만 뺐으므로, 좋은 스태프를 아무리 모아도 돈이 줄지 않았다.
    /// 값이 붙지 않은 선택은 선택이 아니다.
    ///
    /// 이제 급여는 <b>선수 + 스태프</b> 하나다. 세는 곳도 내는 곳도 이 파일을 쓴다.
    /// </summary>
    public static class ClubWages
    {
        /// <summary>선수 주급 합계. 임대 보낸 선수도 명단에 있으면 센다.</summary>
        public static long Players(GameWorld w, ClubData club)
            => club.playerIds?.Sum(id => (long)(w.Player(id)?.contract?.weeklyWage ?? 0)) ?? 0;

        /// <summary>스태프 주급 합계.</summary>
        public static long Staff(GameWorld w, ClubData club)
            => club.staffIds?.Sum(id =>
                   (long)(w.Staff.TryGetValue(id, out var s) ? s.contract?.weeklyWage ?? 0 : 0)) ?? 0;

        /// <summary>주당 급여 합계. 이 게임에서 "급여"는 선수와 스태프 둘이다.</summary>
        public static long Of(GameWorld w, ClubData club) => Players(w, club) + Staff(w, club);

        /// <summary>
        /// 생성 직후 급여가 수입을 감당할 수 있는 선까지 내린다.
        ///
        /// <b>세계가 제재선을 넘는 구단을 만들면 안 된다.</b> 3부 구단은 평균 급여 비중
        /// 81.5%(선수 73.2 + 스태프 8.3) 로 태어났다. 제재선이 70% 이므로 첫날부터
        /// 규정 위반이고, 재계약 화면의 모든 버튼이 죽는다 — 플레이어가 3부에서
        /// 시작하는 게임에서 이것은 벽이다.
        ///
        /// 수입은 평판의 3.4제곱으로 가파르게 떨어지는데 주급은 그만큼 떨어지지 않아
        /// 아래 등급일수록 비율이 벌어진다. 지수를 건드리면 세계의 돈이 통째로 움직이므로,
        /// <b>생성 단계에서 한 번만</b> 맞춰 넣는다. 그 뒤의 경제는 그대로다.
        ///
        /// 스태프 주급은 건드리지 않는다 — 사람마다 받는 값이 정해져 있고,
        /// 구단 사정으로 깎을 수 있는 쪽은 선수 계약이다.
        /// </summary>
        public static void FitToIncome(GameWorld w, float target)
        {
            foreach (var club in w.Clubs.Values)
            {
                long revenue = club.finances.annualRevenue;
                if (revenue <= 0) continue;

                long players = Players(w, club);
                long room = (long)(revenue * target / 52) - Staff(w, club);
                if (players <= room || players <= 0) { Recalc(w, club); continue; }

                float scale = room <= 0 ? 0.25f : room / (float)players;
                foreach (int id in club.playerIds)
                {
                    var c = w.Player(id)?.contract;
                    if (c == null) continue;
                    c.weeklyWage = System.Math.Max(200, (int)(c.weeklyWage * scale));
                }
                Recalc(w, club);
            }
        }

        /// <summary>
        /// 장부에 다시 적는다. 명단이나 계약이 바뀐 자리는 전부 이 한 줄을 부른다 —
        /// 부르지 않으면 급여 비중이 옛값으로 남아 제재선 판정이 어긋난다.
        /// </summary>
        public static void Recalc(GameWorld w, ClubData club)
        {
            if (club == null) return;
            long total = Of(w, club);
            club.finances.currentWageSpend = (int)(total > int.MaxValue ? int.MaxValue : total);
        }
    }
}
