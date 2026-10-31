using System;
using System.Collections.Generic;
using System.Linq;
using FootballTactician.Balance;
using FootballTactician.Data;

namespace FootballTactician.World
{
    /// <summary>
    /// 홈그로운 규정.
    ///
    /// <c>HomegrownStatus</c> 는 선수마다 달려 있었고 <c>LeagueRules.homegrownPlayersMin</c> 도
    /// 4로 적혀 있었지만, 아무도 읽지 않았다. 값은 채워지는데 읽히지 않는 마지막 자리였다.
    ///
    /// 규정이 없으면 돈만 있으면 외국 선수로 스쿼드를 다 채울 수 있고,
    /// 유스 아카데미와 어린 선수 육성이 <b>순수한 손해</b>가 된다. 규정이 그걸 막는다.
    ///
    /// 집행은 이미 있는 방식을 그대로 쓴다 — 급여 비중이 제재선을 넘으면 영입이 막히듯,
    /// 홈그로운이 모자라면 <b>외부 영입이 막힌다</b>. 유스 승격과 자국 선수는 언제나 된다.
    /// </summary>
    public static class Homegrown
    {
        /// <summary>
        /// 한 해가 지났다. 머문 햇수를 세고 자격을 갱신한다.
        ///
        /// 임대 나간 선수도 자국 리그에 있으면 햇수가 는다 — 보내 놓고 키우는 길이 막히면
        /// 임대가 반쪽이 된다.
        /// </summary>
        public static void AdvanceYear(GameWorld w)
        {
            foreach (var p in w.Players.Values)
            {
                p.homegrown ??= new HomegrownStatus();
                if (p.clubId <= 0) continue;

                // 스물한 살까지만 센다. 서른에 와서 3년을 채우면 홈그로운이 되는 것은 아니다.
                if (w.Age(p) > BalanceConfig.HomegrownMaxAge) continue;

                p.homegrown.yearsInNationalLeague++;

                // 임대 중이면 원소속 구단에 머문 것으로 치지 않는다.
                if (!w.Loans.ContainsKey(p.id))
                    p.homegrown.yearsAtCurrentClub++;

                Refresh(p);
            }
        }

        /// <summary>머문 햇수로 자격을 다시 매긴다.</summary>
        public static void Refresh(PlayerData p)
        {
            if (p.homegrown == null) return;

            p.homegrown.isHomegrown =
                p.homegrown.yearsInNationalLeague >= BalanceConfig.HomegrownYears;

            p.homegrown.isClubAcademy =
                p.homegrown.yearsAtCurrentClub >= BalanceConfig.HomegrownYears;
        }

        /// <summary>구단을 옮기면 구단 햇수만 사라진다. 자국 리그 햇수는 따라간다.</summary>
        public static void OnTransfer(GameWorld w, PlayerData p, ClubData from, ClubData to)
        {
            p.homegrown ??= new HomegrownStatus();
            p.homegrown.yearsAtCurrentClub = 0;

            // 다른 나라로 가면 자국 리그 햇수도 처음부터다.
            if (w.NationOf(from?.id ?? 0)?.Country != w.NationOf(to?.id ?? 0)?.Country)
                p.homegrown.yearsInNationalLeague = 0;

            Refresh(p);
        }

        /// <summary>아카데미에서 올라온 선수는 처음부터 자격을 갖는다.</summary>
        public static void MarkAcademyGraduate(PlayerData p)
        {
            p.homegrown = new HomegrownStatus
            {
                yearsInNationalLeague = BalanceConfig.HomegrownYears,
                yearsAtCurrentClub = BalanceConfig.HomegrownYears,
                isHomegrown = true,
                isClubAcademy = true,
            };
        }

        /// <summary>
        /// 세계를 만들 때 자격을 심는다.
        ///
        /// 없으면 240구단이 전부 홈그로운 0명으로 시작해 <b>이적 시장이 통째로 얼어붙는다</b>.
        /// 규정이 게임을 막는 것이 아니라 게임을 만들어야 한다.
        ///
        /// 실제 축구가 그렇듯, 자국 선수 대부분은 그 리그에서 자랐다.
        /// 구단마다 몇은 그 구단 아카데미 출신이다.
        /// </summary>
        public static void SeedWorld(GameWorld w, Random rng)
        {
            foreach (var club in w.Clubs.Values.OrderBy(c => c.id))
            {
                // 나라 이름(한글)과 Nationality enum 을 견주면 영원히 맞지 않는다.
                var code = w.NationOf(club.id)?.Code;
                if (code == null) continue;
                var local = NameForge.ByCode(code).Nationality;

                var squad = w.Squad(club.id).OrderBy(p => p.id).ToList();

                int academy = 0;
                foreach (var p in squad)
                {
                    p.homegrown ??= new HomegrownStatus();

                    if (p.nationality != local) continue;

                    p.homegrown.yearsInNationalLeague = BalanceConfig.HomegrownYears;

                    // 어린 자국 선수 몇은 이 구단에서 자랐다.
                    if (academy < BalanceConfig.HomegrownSeedAcademy && w.Age(p) <= 24 && rng.Next(100) < 55)
                    {
                        p.homegrown.yearsAtCurrentClub = BalanceConfig.HomegrownYears;
                        academy++;
                    }

                    Refresh(p);
                }
            }
        }

        // ── 규정 ──────────────────────────────────────────────────────────

        /// <summary>
        /// 규정이 세는 것은 <b>이 구단 유스 출신</b>이다.
        ///
        /// 국적으로 세면 이 세계에서는 규정이 영원히 물리지 않는다 —
        /// 구단 스쿼드가 전부 자국 선수라 25명 모두가 자격을 갖는다.
        /// 클럽 유스로 세야 아카데미가 실제로 의미를 갖는다.
        /// </summary>
        public static bool IsHomegrown(PlayerData p) => p.homegrown?.isClubAcademy ?? false;

        /// <summary>자국 리그에서 자랐는가. 지금은 표시용이고 규정은 클럽 유스로 센다.</summary>
        public static bool IsNationallyTrained(PlayerData p) => p.homegrown?.isHomegrown ?? false;

        public static int CountIn(GameWorld w, int clubId)
            => w.Squad(clubId).Count(IsHomegrown);

        public static int RequiredFor(GameWorld w, int clubId)
            => w.LeagueOf(clubId)?.rules?.homegrownPlayersMin ?? BalanceConfig.HomegrownDefaultMin;

        public static bool Meets(GameWorld w, int clubId)
            => CountIn(w, clubId) >= RequiredFor(w, clubId);

        /// <summary>
        /// 이 선수를 데려와도 규정을 지키는가.
        /// 홈그로운 선수는 언제나 데려올 수 있다 — 막으면 규정을 지킬 길이 막힌다.
        /// </summary>
        public static bool CanSign(GameWorld w, int clubId, PlayerData incoming)
            => IsHomegrown(incoming) || Meets(w, clubId);

        /// <summary>
        /// 이 선수를 내보내도 규정을 지키는가.
        /// 마지막 홈그로운을 팔아 치우면 다음 영입이 통째로 막힌다.
        /// </summary>
        public static bool CanSell(GameWorld w, int clubId, PlayerData outgoing)
            => !IsHomegrown(outgoing)
            || CountIn(w, clubId) - 1 >= RequiredFor(w, clubId);

        public static string Shortfall(GameWorld w, int clubId)
        {
            int have = CountIn(w, clubId);
            int need = RequiredFor(w, clubId);
            return have >= need
                ? $"{have}명 — 규정을 지키고 있습니다"
                : $"{have}/{need}명 — {need - have}명이 모자라 외부 영입이 막혀 있습니다";
        }
    }
}
