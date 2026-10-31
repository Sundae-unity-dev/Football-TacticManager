using System;
using System.Collections.Generic;
using System.Linq;
using FootballTactician.Data;
using FootballTactician.Diagnostics;

namespace FootballTactician.Simulation
{
    /// <summary>
    /// 재개 가능한 경기. 5분 블록 단위로 진행하며, 중간에 교체와 지시 변경을 받는다.
    ///
    /// MatchEngine.Simulate 도 이 클래스를 거쳐 돌아간다. 경로를 둘로 두면
    /// 화면에서 치른 경기와 자동으로 돌린 경기의 결과 분포가 서로 달라진다.
    /// </summary>
    public sealed class MatchSession
    {
        public const int BlockMinutes = 5;
        public const int RegularBlocks = 18;        // 5분 × 18 = 90분
        public const int StoppageBlocks = 2;        // 추가 시간
        public const int TotalBlocks = RegularBlocks + StoppageBlocks;
        public const int HalfTimeBlock = RegularBlocks / 2;

        readonly MatchSimContext _ctx;
        readonly Func<int, PlayerData> _getPlayer;
        readonly Random _rng;

        MatchEngine.TeamState _home, _away;
        int _homePoss;
        int _homeGoals, _awayGoals;
        int _block;

        public List<MatchEvent> Events { get; } = new();
        public MatchStats HomeStats { get; } = new();
        public MatchStats AwayStats { get; } = new();

        public int HomeGoals => _homeGoals;
        public int AwayGoals => _awayGoals;
        public int HomePossession => _homePoss;

        public int HomeClubId => _ctx.home.clubId;
        public int AwayClubId => _ctx.away.clubId;

        /// <summary>
        /// 이 경기의 컨텍스트. 경기 후처리(스탯 반영·체력 소모)가 자동 진행과 같은 함수를 쓰도록 내준다.
        /// 라인업은 교체를 거친 현재 상태다.
        /// </summary>
        public MatchSimContext Context => _ctx;

        /// <summary>현재까지 흘러간 분. 아직 시작 전이면 0.</summary>
        public int Minute => _block <= RegularBlocks
            ? _block * BlockMinutes
            : 90 + (_block - RegularBlocks) * BlockMinutes;

        public bool Finished => _block >= TotalBlocks;
        public bool AtHalfTime => _block == HalfTimeBlock;
        public bool AtKickOff => _block == 0;

        /// <summary>남은 교체 횟수.</summary>
        public int SubsLeft(bool home) => Math.Max(0, _ctx.rules.maxSubstitutions - SubsUsed(home));
        public int SubsUsed(bool home) => home ? _homeSubs : _awaySubs;
        int _homeSubs, _awaySubs;

        MatchSession(MatchSimContext ctx, Func<int, PlayerData> getPlayer)
        {
            _ctx = ctx;
            _getPlayer = getPlayer;
            _rng = ctx.seed >= 0 ? new Random(ctx.seed) : new Random();
            Rebuild();
            HomeStats.possession = _homePoss;
            AwayStats.possession = 100 - _homePoss;
        }

        public static MatchSession Begin(MatchSimContext ctx, Func<int, PlayerData> getPlayer)
            => new MatchSession(ctx, getPlayer);

        /// <summary>
        /// 라인업이 바뀌면 전력을 처음부터 다시 계산한다.
        /// 환경 보정과 멘탈리티는 전력에 곱해지므로 순서를 킥오프 때와 똑같이 지켜야 한다.
        /// </summary>
        void Rebuild()
        {
            // 쌓인 피로는 넘겨받는다. 새로 만들면 0 이 되어, 지시를 한 번 바꾸는 것만으로
            // 체력이 공짜로 회복된다. 경기 중 지시 변경은 매 경기 여러 번 일어난다.
            float homeFatigue = _home?.fatigue ?? 0f;
            float awayFatigue = _away?.fatigue ?? 0f;

            using (SimProfile.Measure("      · 팀 상태"))
            {
                _home = MatchEngine.BuildTeamState(_ctx.home, _getPlayer);
                _away = MatchEngine.BuildTeamState(_ctx.away, _getPlayer);
            }
            _home.fatigue = homeFatigue;
            _away.fatigue = awayFatigue;

            // 순서가 중요하다. 환경 보정이 attackPower 를 올리고, 멘탈리티가 그로부터
            // effectiveAttack 을 계산한다. 뒤집으면 홈 보정이 사라지거나 두 번 걸린다.
            // 전술 보정은 두 팀을 같이 봐야 해서(궁합) 멘탈리티보다 먼저 한 번만 돈다.
            MatchEngine.ApplyEnvironment(_home, _away, _ctx.weather, _ctx.pitch);
            MatchEngine.ApplyTactics(_home, _away);
            MatchEngine.ApplyMentality(_home);
            MatchEngine.ApplyMentality(_away);

            _homePoss = MatchEngine.CalcPossession(_home, _away);
        }

        // ── 진행 ──────────────────────────────────────────────────────────

        /// <summary>5분 한 블록을 진행한다.</summary>
        public void AdvanceBlock()
        {
            if (Finished) return;

            int startMinute = _block < RegularBlocks
                ? _block * BlockMinutes + 1
                : 91 + (_block - RegularBlocks) * BlockMinutes;

            using (SimProfile.Measure("      · 블록 진행"))
                MatchEngine.SimulateBlock(
                    _home, _away, _homePoss, startMinute,
                    Events, HomeStats, AwayStats, ref _homeGoals, ref _awayGoals, _rng);

            _block++;

            // 체력이 떨어진 선수를 자동으로 뺀다. 감독이 직접 다루는 쪽은 건드리지 않는다.
            if (_block is >= 12 and < RegularBlocks)
            {
                using var subSpan = SimProfile.Measure("      · 자동 교체");
                if (AutoSubstitutions.Home) TryAutoSub(true);
                if (AutoSubstitutions.Away) TryAutoSub(false);
            }
        }

        /// <summary>자동 교체를 맡길지. 감독이 직접 교체하는 쪽은 꺼 둔다.</summary>
        public (bool Home, bool Away) AutoSubstitutions = (true, true);

        public void AdvanceToHalfTime()
        {
            while (_block < HalfTimeBlock) AdvanceBlock();
        }

        public void AdvanceToEnd()
        {
            while (!Finished) AdvanceBlock();
        }

        /// <summary>경기를 마치고 결과를 만든다. 두 번 불러도 같은 결과가 나온다.</summary>
        public MatchResultData Finish()
        {
            AdvanceToEnd();

            using (SimProfile.Measure("      · 마무리 집계"))
            {
                MatchEngine.FillDerivedStats(_home, _away, _homePoss, HomeStats, AwayStats);
                MatchEngine.ApplyCardsFromEvents(Events, HomeStats, AwayStats, _ctx.home.clubId);
            }

            return new MatchResultData
            {
                homeGoals = _homeGoals,
                awayGoals = _awayGoals,
                events = Events,
                homeStats = HomeStats,
                awayStats = AwayStats,
            };
        }

        // ── 교체 ──────────────────────────────────────────────────────────

        public enum SubResult { Ok, NoSubsLeft, NotOnPitch, NotOnBench, SamePlayer }

        /// <summary>선발 한 명을 벤치 한 명으로 바꾼다.</summary>
        public SubResult Substitute(bool home, int outPlayerId, int inPlayerId)
        {
            if (outPlayerId == inPlayerId) return SubResult.SamePlayer;
            if (SubsLeft(home) <= 0) return SubResult.NoSubsLeft;

            var lineup = home ? _ctx.home : _ctx.away;
            var slot = lineup.tactics.slots.FirstOrDefault(s => s.assignedPlayerId == outPlayerId);
            if (slot == null) return SubResult.NotOnPitch;
            if (!lineup.benchIds.Contains(inPlayerId)) return SubResult.NotOnBench;

            slot.assignedPlayerId = inPlayerId;
            lineup.benchIds.Remove(inPlayerId);
            lineup.benchIds.Add(outPlayerId);     // 교체되어 나간 선수는 다시 들어오지 못한다

            if (home) _homeSubs++; else _awaySubs++;

            Events.Add(new MatchEvent
            {
                minute = Math.Max(1, Minute),
                eventType = MatchEventType.Substitution,
                clubId = lineup.clubId,
                playerId = outPlayerId,
                assistPlayerId = inPlayerId,
            });

            // 교체로 나간 선수는 벤치 목록에 남지만 다시 투입되면 안 되므로 표시해 둔다.
            _usedUp.Add(outPlayerId);
            Rebuild();
            return SubResult.Ok;
        }

        readonly HashSet<int> _usedUp = new();

        /// <summary>지금 교체로 들어갈 수 있는 선수들.</summary>
        public IEnumerable<PlayerData> AvailableBench(bool home)
        {
            var lineup = home ? _ctx.home : _ctx.away;
            foreach (int id in lineup.benchIds)
            {
                if (_usedUp.Contains(id)) continue;
                var p = _getPlayer(id);
                if (p != null && !p.IsInjured) yield return p;
            }
        }

        public IEnumerable<(FormationSlot slot, PlayerData player)> OnPitch(bool home)
        {
            var lineup = home ? _ctx.home : _ctx.away;
            foreach (var slot in lineup.tactics.slots.OrderBy(s => s.slotIndex))
            {
                var p = _getPlayer(slot.assignedPlayerId);
                if (p != null) yield return (slot, p);
            }
        }

        /// <summary>
        /// 가장 지친 선수를 같은 자리에 맞는 벤치 선수로 바꾼다.
        /// 교체가 없으면 체력이 떨어진 팀이 후반에 일방적으로 밀린다.
        /// </summary>
        void TryAutoSub(bool home)
        {
            // 난수는 교체가 실제로 성립할 때만 뽑는다. 먼저 뽑으면 교체할 일이 없는 경기에서도
            // 난수 흐름이 밀려, 같은 시드로 돌린 측정값이 교체 기능 유무에 따라 달라진다.
            if (SubsLeft(home) <= 0) return;

            // 한 경기에 스물네 번 불린다(블록 12~17 × 두 팀). 전에는 그때마다 LINQ 세 겹으로
            // 정렬했고, 그 비용이 실제 경기 진행(블록 18회)보다 두 배였다.
            // 교체는 대부분 성립하지 않으므로 — 지친 선수가 없으면 거기서 끝난다 —
            // 정렬 없이 한 번 훑어 같은 선수를 집는다.
            var lineup = home ? _ctx.home : _ctx.away;
            var slots = lineup.tactics.slots;

            // 자리 번호 순으로 본다. OnPitch 가 slotIndex 로 정렬해 내주므로,
            // 컨디션이 같은 선수가 둘이면 번호가 앞선 쪽이 뽑혀야 같은 결과가 된다.
            // 삽입 정렬이라 같은 번호끼리도 원래 순서를 지킨다(안정 정렬).
            Span<int> order = stackalloc int[slots.Count];
            for (int i = 0; i < slots.Count; i++)
            {
                int j = i;
                while (j > 0 && slots[order[j - 1]].slotIndex > slots[i].slotIndex)
                {
                    order[j] = order[j - 1];
                    j--;
                }
                order[j] = i;
            }

            FormationSlot tiredSlot = null;
            PlayerData tired = null;

            foreach (int i in order)
            {
                var slot = slots[i];
                if ((slot.position & Position.GK) != 0) continue;

                var p = _getPlayer(slot.assignedPlayerId);
                if (p == null) continue;
                if (tired != null && !(p.condition < tired.condition)) continue;

                tiredSlot = slot;
                tired = p;
            }

            if (tired == null || tired.condition > 62f) return;

            // 벤치도 같은 규칙으로 — 적합도가 같으면 벤치 명단에서 앞선 쪽.
            PlayerData replacement = null;
            float bestFit = 0f;

            foreach (int id in lineup.benchIds)
            {
                if (_usedUp.Contains(id)) continue;
                var p = _getPlayer(id);
                if (p == null || p.IsInjured) continue;
                if ((p.positionFlags & Position.GK) != 0) continue;

                float fit = Suitability(p, tiredSlot.position);
                if (replacement != null && !(fit > bestFit)) continue;

                replacement = p;
                bestFit = fit;
            }

            if (replacement == null) return;

            // 지친 주전보다 못하면 그대로 둔다.
            if (replacement.AbilityScore * (replacement.condition / 100f)
                <= tired.AbilityScore * (tired.condition / 100f)) return;

            if (_rng.NextDouble() > 0.35) return;      // 한 블록에 몰아서 바꾸지 않는다

            Substitute(home, tired.id, replacement.id);
        }

        static float Suitability(PlayerData p, Position slot)
            => p.AbilityScore + ((p.positionFlags & slot) != 0 ? 4f : 0f);

        // ── 경기 중 지시 ──────────────────────────────────────────────────

        public void SetMentality(bool home, TeamMentality mentality)
        {
            var lineup = home ? _ctx.home : _ctx.away;
            lineup.tactics.instructions.mentality = mentality;
            Rebuild();
        }

        public void SetPressing(bool home, int intensity)
        {
            var lineup = home ? _ctx.home : _ctx.away;
            lineup.tactics.instructions.pressingIntensity = Math.Clamp(intensity, 1, 5);
            Rebuild();
        }

        public void SetDefensiveLine(bool home, DefensiveLine line)
        {
            var lineup = home ? _ctx.home : _ctx.away;
            lineup.tactics.instructions.defensiveLine = line;
            Rebuild();
        }

        public TeamInstructions Instructions(bool home)
            => (home ? _ctx.home : _ctx.away).tactics.instructions;
    }
}
