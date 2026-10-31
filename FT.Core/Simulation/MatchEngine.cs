using System;
using System.Collections.Generic;
using FootballTactician.Data;

namespace FootballTactician.Simulation
{
    /// <summary>
    /// 경기 시뮬레이션 엔진. Unity 의존성 없음 — 단위 테스트 가능.
    /// </summary>
    public static class MatchEngine
    {
        // ── 진입점 ────────────────────────────────────────────────────────────

        /// <summary>
        /// 경기를 끝까지 시뮬레이션하고 결과를 반환한다.
        /// 화면에서 한 블록씩 진행하는 경로와 같은 코드를 쓴다 — 경로가 둘이면 결과 분포가 어긋난다.
        /// </summary>
        public static MatchResultData Simulate(
            MatchSimContext ctx,
            Func<int, PlayerData> getPlayer)
            => MatchSession.Begin(ctx, getPlayer).Finish();

        /// <summary>
        /// 라인업의 공격·수비·중원·골키퍼 전력. 화면의 전력 표시와 밸런스 측정이 같은 값을 보게 한다.
        /// 멘탈리티까지 적용한 값이며, 홈 보정은 빠져 있다.
        /// </summary>
        public static (float attack, float defense, float midfield, float gk) TeamPower(
            MatchLineup lineup, Func<int, PlayerData> getPlayer)
        {
            var ts = BuildTeamState(lineup, getPlayer);
            ApplyMentality(ts);
            return (ts.effectiveAttack, ts.effectiveDefense, ts.midfieldPower, ts.gkPower);
        }

        /// <summary>경기 결과를 선수 시즌 스탯과 평점에 반영한다.</summary>
        public static void ApplyResult(
            MatchResultData result,
            MatchSimContext ctx,
            Func<int, PlayerData> getPlayer)
        {
            // 선수ID → 이벤트 카운트 (한 번 순회로 4개 카테고리 동시 집계)
            // 라인업 22명 × 평균 5~10 이벤트 → Dictionary 단일 패스가 (이전 4×N×11 = 44N) 대비 큰 절감
            var tally = TallyPlayerEvents(result.events);

            ApplyLineup(ctx.home, ctx, result.homeGoals, result.awayGoals, tally, getPlayer);
            ApplyLineup(ctx.away, ctx, result.awayGoals, result.homeGoals, tally, getPlayer);
        }

        private struct PlayerTally { public int goals, assists, yellows, reds; }

        private static Dictionary<int, PlayerTally> TallyPlayerEvents(List<MatchEvent> events)
        {
            var tally = new Dictionary<int, PlayerTally>();
            foreach (var e in events)
            {
                switch (e.eventType)
                {
                    // 세트피스 득점도 선수의 골이다. 빼면 팀 점수와 득점왕 기록이 어긋난다.
                    case MatchEventType.Goal:
                    case MatchEventType.CornerKickGoal:
                    case MatchEventType.FreeKickGoal:
                    case MatchEventType.PenaltyScored:
                        if (e.playerId >= 0)
                        {
                            tally.TryGetValue(e.playerId, out var t);
                            t.goals++;
                            tally[e.playerId] = t;
                        }
                        if (e.assistPlayerId.HasValue && e.assistPlayerId.Value >= 0)
                        {
                            int aid = e.assistPlayerId.Value;
                            tally.TryGetValue(aid, out var t2);
                            t2.assists++;
                            tally[aid] = t2;
                        }
                        break;
                    case MatchEventType.YellowCard:
                    case MatchEventType.SecondYellow:
                        if (e.playerId >= 0)
                        {
                            tally.TryGetValue(e.playerId, out var t);
                            t.yellows++;
                            tally[e.playerId] = t;
                        }
                        break;
                    case MatchEventType.RedCard:
                        if (e.playerId >= 0)
                        {
                            tally.TryGetValue(e.playerId, out var t);
                            t.reds++;
                            tally[e.playerId] = t;
                        }
                        break;
                }
            }
            return tally;
        }

        private static void ApplyLineup(
            MatchLineup lineup, MatchSimContext ctx,
            int goalsFor, int goalsAgainst,
            Dictionary<int, PlayerTally> tally,
            Func<int, PlayerData> getPlayer)
        {
            bool cleanSheet = goalsAgainst == 0;

            PlayerData motmCandidate = null;
            float      motmRating    = 0f;

            foreach (var slot in lineup.tactics.slots)
            {
                var p = getPlayer(slot.assignedPlayerId);
                if (p == null) continue;

                tally.TryGetValue(p.id, out var t);

                float rating = CalculateRating(slot.position, t.goals, t.assists, t.yellows, t.reds, goalsAgainst, cleanSheet);

                p.seasonStats ??= new PlayerStats();
                p.seasonStats.appearances++;
                p.seasonStats.goals         += t.goals;
                p.seasonStats.assists       += t.assists;
                p.seasonStats.yellowCards   += t.yellows;
                p.seasonStats.redCards      += t.reds;
                p.seasonStats.minutesPlayed += 90;
                if ((slot.position & Position.GK) != 0 && cleanSheet)
                    p.seasonStats.cleanSheets++;

                var mr = new MatchRating
                {
                    matchId       = ctx.fixtureId,
                    rating        = rating,
                    goals         = t.goals,
                    assists       = t.assists,
                    minutesPlayed = 90,
                };
                p.AddMatchRating(mr);

                float ratingSum = 0;
                foreach (var r in p.recentRatings) ratingSum += r.rating;
                p.seasonStats.averageRating = ratingSum / p.recentRatings.Count;

                p.InvalidateAbilityCache();

                // MOTM 후보: 별도 순회 없이 ApplyLineup 안에서 추적
                if (rating > motmRating) { motmRating = rating; motmCandidate = p; }
            }

            if (motmCandidate != null && motmRating >= 8.0f && motmCandidate.recentRatings.Count > 0)
                motmCandidate.recentRatings[motmCandidate.recentRatings.Count - 1].manOfTheMatch = true;
        }

        // ── 팀 상태 ───────────────────────────────────────────────────────────

        internal sealed class TeamState
        {
            public int        clubId;
            public TacticsData tactics;
            public PlayerData[] starters = new PlayerData[11];
            public PlayerData   goalkeeper;   // GK 슬롯을 명시적으로 보유 (배열 인덱스에 의존하지 않음)

            public float attackPower;
            public float defensePower;
            public float midfieldPower;
            public float gkPower;

            // ApplyMentality 적용 후 사용
            public float effectiveAttack;
            public float effectiveDefense;

            /// <summary>팀 지시에서 나온 보정. 상대를 본 궁합까지 반영된 값이다.</summary>
            public TacticalProfile profile = TacticalProfile.Neutral;

            /// <summary>오프사이드 트랩 숙련. 상대가 트랩을 걸 때 쓰인다.</summary>
            public float trapSkill;

            /// <summary>세트피스에서의 제공권. 공격 쪽과 수비 쪽을 따로 본다.</summary>
            public float aerialThreat;
            public float aerialDefence;

            /// <summary>골키퍼의 박스 장악력. 코너에서는 전체 능력보다 이쪽이 중요하다.</summary>
            public float gkCommand;

            /// <summary>달려드는 움직임. 아웃스윙과 낮은 크로스가 여기에 걸린다.</summary>
            public float movementThreat;

            /// <summary>선수별 슈팅 질 배수. 많이 때리라고 한 선수는 하나하나가 나쁘다.</summary>
            public Dictionary<int, float> shotQualityOf = new();

            /// <summary>선수 개인 지시의 팀 단위 합계.</summary>
            public float piPossession;
            public float piQuality;
            public float piFatigue;

            /// <summary>경기 중 쌓인 피로. 0 = 쌩쌩함, 1 = 완전히 지침.</summary>
            public float fatigue;

            /// <summary>지금 체력이 경기력에 곱해지는 값.</summary>
            public float FatigueFactor => TacticalProfile.FatigueFactor(fatigue);

            public (PlayerData p, float w)[] attackerWeights;
            public (PlayerData p, float w)[] creatorWeights;
            public (PlayerData p, float w)[] cardRiskWeights;
        }

        internal static TeamState BuildTeamState(
            MatchLineup lineup, Func<int, PlayerData> getPlayer)
        {
            var ts = new TeamState { clubId = lineup.clubId, tactics = lineup.tactics };
            var slots = lineup.tactics.slots;

            float sumAtk = 0, sumDef = 0, sumMid = 0;
            int outfield = 0;

            // 열한 명으로 잡고 시작한다. 빈 목록으로 두면 4·8·16 으로 두 번씩 다시 잡는다.
            var attackers = new List<(PlayerData, float)>(11);
            var creators  = new List<(PlayerData, float)>(11);
            var cardRisks = new List<(PlayerData, float)>(11);

            for (int i = 0; i < slots.Count && i < 11; i++)
            {
                var slot = slots[i];
                var p    = getPlayer(slot.assignedPlayerId);
                if (p == null) continue;

                ts.starters[i] = p;

                // 컨디션과 사기는 팀 전원에 곱해지는 값이라 폭을 좁게 둔다.
                // 품질 비율이 3제곱으로 증폭되므로, 여기서 25% 를 흔들면 득점 기대가 2배로 벌어진다.
                // 증폭은 지수에 맡기고 이 배수는 완만하게 간다.
                float cond   = 0.82f + 0.18f * (p.condition / 100f);   // 0.82 ~ 1.00
                float morale = 1f + (p.morale - 60f) / 600f;           // 0.90 ~ 1.07

                if ((slot.position & Position.GK) != 0)
                {
                    var (gkFit, _, _) = RoleEffect(slot.role, p);
                    ts.gkPower    = p.goalkeeper.Average() * cond * morale * gkFit;
                    ts.goalkeeper = p;
                    continue;
                }

                outfield++;
                float atk = AtkAbility(p);
                float def = DefAbility(p);
                float mid = MidAbility(p);
                var (aw, dw, mw) = PositionWeights(slot.position);

                // 역할 — 적합도가 기여 전체에 곱해지고, 임무가 공격과 수비 사이에서 옮긴다.
                //        팀 전체의 역할 균형은 아래에서 공격에 한 번 더 곱한다.
                // 임무는 총량을 바꾸지 않으므로 전원을 공격 임무로 둬도 팀이 세지지 않는다.
                var (fit, dutyAtk, dutyDef) = RoleEffect(slot.role, p);
                float eff = cond * morale * fit;
                aw *= dutyAtk;
                dw *= dutyDef;

                // 개인 지시. 지정하지 않았으면 중립이라 아무것도 바뀌지 않는다.
                // 합산 앞에 와야 한다 — 뒤에 두면 수비 기여에 닿지 못한다.
                var pi = PlayerInstructionEffect.From(slot.customInstructions);
                dw *= pi.defence;

                sumAtk += atk * aw * eff;
                sumDef += def * dw * eff;
                sumMid += mid * mw * eff;

                if (aw >= 0.4f) attackers.Add((p, aw * atk * pi.shotShare));
                // 지시가 없는 선수는 1 이고, 1 은 곱해도 같다 — 넣지 않는다.
                // 받는 쪽이 TryGetValue 로 보고 없으면 건너뛰므로 결과가 같다.
                if (pi.shotQuality != 1f) ts.shotQualityOf[p.id] = pi.shotQuality;
                creators.Add((p, (mid + atk * 0.5f) * mw * pi.creatorShare));
                cardRisks.Add((p, (p.mental.aggression + p.mental.workRate) * 0.5f * pi.cardRisk));
            }

            if (outfield > 0)
            {
                // 만들어 줄 역할과 넣어 줄 역할이 한쪽으로 쏠리면 공격이 겉돈다.
                // 이게 없으면 선수마다 적합도가 가장 높은 역할을 고르는 것이 늘 정답이라,
                // 역할 고르기가 선택이 아니라 작업이 된다.
                float balance = RoleFit.BalanceMultiplier(slots);

                ts.attackPower   = sumAtk / outfield * balance;
                ts.defensePower  = sumDef / outfield;
                ts.midfieldPower = sumMid / outfield;
            }

            // 공격자 목록이 비면 선발 전원을 동등 가중치로 폴백
            if (attackers.Count == 0)
                for (int i = 1; i < 11; i++)
                    if (ts.starters[i] != null) attackers.Add((ts.starters[i], 1f));

            ts.aerialThreat  = SetPieces.AerialThreat(ts.starters);
            ts.aerialDefence = SetPieces.AerialDefence(ts.starters, ts.goalkeeper);
            ts.gkCommand      = SetPieces.GkCommand(ts.goalkeeper, ts.gkPower);
            ts.movementThreat = SetPieces.MovementThreat(ts.starters);

            // 개인 지시의 팀 단위 합계. 전원이 Default 면 (0, 1, 0) 이라 중립이다.
            (ts.piPossession, ts.piQuality, ts.piFatigue) =
                PlayerInstructionEffect.TeamTotals(slots);

            ts.attackerWeights = attackers.ToArray();
            ts.creatorWeights  = creators.ToArray();
            ts.cardRiskWeights = cardRisks.ToArray();
            return ts;
        }

        // ── 능력치 계산 ───────────────────────────────────────────────────────

        private static float AtkAbility(PlayerData p) =>
            (p.technical.finishing + p.technical.shooting +
             p.technical.dribbling + p.mental.offTheBall) * 0.25f;

        private static float DefAbility(PlayerData p) =>
            (p.technical.tackling + p.technical.marking +
             p.mental.positioning + p.mental.concentration) * 0.25f;

        private static float MidAbility(PlayerData p) =>
            (p.technical.passing + p.mental.vision +
             p.mental.workRate   + p.mental.decisions) * 0.25f;

        /// <summary>
        /// 슬롯에 지정된 역할의 효과. 역할이 없으면 아무것도 바꾸지 않는다 —
        /// 역할을 지정하지 않은 전술이 불리해지면 안 된다.
        /// </summary>
        private static (float fit, float dutyAtk, float dutyDef) RoleEffect(
            RoleAssignment role, PlayerData p)
        {
            if (role == null) return (1f, 1f, 1f);
            if (!RoleDatabase.All.TryGetValue(role.role, out var def)) return (1f, 1f, 1f);

            var (da, dd) = RoleFit.DutyShift(def, role.duty);
            return (RoleFit.Multiplier(p, def), da, dd);
        }

        /// <summary>세트피스 하나를 처리한다. 열린 플레이와 달리 제공권과 키커가 결과를 정한다.</summary>
        private static void ResolveSetPiece(
            TeamState att, TeamState def, SetPieces.Kind kind,
            int minute, List<MatchEvent> events, MatchStats attStats,
            ref int attGoals, Random rng)
        {
            var taker = SetPieces.Taker(att.starters, att.tactics, kind);
            var delivery = SetPieces.DeliveryOf(att.tactics, kind);

            if (kind == SetPieces.Kind.Corner) attStats.corners++;

            float xg = SetPieces.Xg(
                kind,
                SetPieces.TakerQuality(taker, kind),
                att.aerialThreat, def.aerialDefence,
                delivery,
                kind == SetPieces.Kind.Corner ? def.gkCommand : def.gkPower,
                // 움직임 우위는 상대 수비의 위치 선정과 겨룬다.
                def.movementThreat <= 0f ? 1f : att.movementThreat / def.movementThreat);

            attStats.shots++;
            attStats.xG += xg;
            if (kind == SetPieces.Kind.Penalty) attStats.bigChances++;

            if (kind == SetPieces.Kind.Penalty)
                events.Add(new MatchEvent
                {
                    minute = minute,
                    eventType = MatchEventType.PenaltyAwarded,
                    clubId = att.clubId,
                    playerId = taker?.id ?? 0,
                });

            bool scored = rng.NextDouble() < xg;

            if (scored)
            {
                // 페널티와 프리킥은 키커가 넣는다. 코너는 머리로 받는 선수가 넣는다.
                int scorer = kind == SetPieces.Kind.Corner
                    ? PickWeighted(SetPieces.ScorerWeights(att.starters, kind), rng)
                    : taker?.id ?? PickWeighted(att.attackerWeights, rng);

                int? assister = kind == SetPieces.Kind.Corner && taker != null && taker.id != scorer
                    ? taker.id
                    : (int?)null;

                events.Add(new MatchEvent
                {
                    minute = minute + rng.Next(0, 5),
                    eventType = kind switch
                    {
                        SetPieces.Kind.Penalty => MatchEventType.PenaltyScored,
                        SetPieces.Kind.FreeKick => MatchEventType.FreeKickGoal,
                        _ => MatchEventType.CornerKickGoal,
                    },
                    clubId = att.clubId,
                    playerId = scorer,
                    assistPlayerId = assister,
                });

                attGoals++;
                attStats.shotsOnTarget++;
                return;
            }

            if (kind == SetPieces.Kind.Penalty)
            {
                bool saved = rng.NextDouble() < 0.55;
                events.Add(new MatchEvent
                {
                    minute = minute,
                    eventType = saved ? MatchEventType.PenaltySaved : MatchEventType.PenaltyMissed,
                    clubId = att.clubId,
                    playerId = taker?.id ?? 0,
                });
                if (saved) attStats.shotsOnTarget++;
                attStats.bigChancesMissed++;
                return;
            }

            if (rng.NextDouble() < 0.33) attStats.shotsOnTarget++;

            // 짧게 처리하면 득점 기대는 낮지만 공을 잃지 않는다.
            // 일정 확률로 그대로 열린 플레이 찬스가 이어진다 — 이것이 짧게 가는 이유다.
            if (kind == SetPieces.Kind.Corner
                && delivery == SetPieceDelivery.ShortPass
                && rng.NextDouble() < Balance.BalanceConfig.ShortRetainChance)
            {
                attStats.shots--;      // 이어진 공격에서 다시 센다
                ResolveOpenPlay(att, def, minute, events, attStats, ref attGoals, rng);
            }
        }

        private static (float atk, float def, float mid) PositionWeights(Position pos)
        {
            if ((pos & (Position.ST | Position.CF))           != 0) return (1.00f, 0.05f, 0.10f);
            if ((pos & (Position.LW | Position.RW | Position.CAM)) != 0) return (0.80f, 0.10f, 0.50f);
            if ((pos & (Position.LM | Position.RM))           != 0) return (0.60f, 0.20f, 0.70f);
            if ((pos & Position.CM)                           != 0) return (0.40f, 0.35f, 1.00f);
            if ((pos & Position.CDM)                          != 0) return (0.20f, 0.65f, 0.80f);
            if ((pos & (Position.LWB | Position.RWB))         != 0) return (0.30f, 0.65f, 0.50f);
            if ((pos & (Position.LB  | Position.RB))          != 0) return (0.15f, 0.80f, 0.40f);
            if ((pos & Position.CB)                           != 0) return (0.05f, 1.00f, 0.30f);
            return (0.10f, 0.50f, 0.50f);
        }

        // ── 멘탈리티 ─────────────────────────────────────────────────────────

        /// <summary>
        /// 두 팀의 지시를 보정값으로 바꾼다. 궁합이 있어 한쪽만 따로 계산할 수 없다.
        /// <see cref="ApplyEnvironment"/> 와 <see cref="ApplyMentality"/> 사이에서 부른다.
        /// </summary>
        internal static void ApplyTactics(TeamState home, TeamState away)
        {
            home.trapSkill = TacticalProfile.TrapSkill(home.starters);
            away.trapSkill = TacticalProfile.TrapSkill(away.starters);

            home.profile = TacticalProfile.FromInstructions(home.tactics.instructions);
            away.profile = TacticalProfile.FromInstructions(away.tactics.instructions);

            TacticalProfile.Resolve(ref home.profile, home.tactics.instructions,
                                    away.tactics.instructions, away.trapSkill);
            TacticalProfile.Resolve(ref away.profile, away.tactics.instructions,
                                    home.tactics.instructions, home.trapSkill);
        }

        internal static void ApplyMentality(TeamState ts)
        {
            // 공격과 수비 배수는 서로 역수여야 한다. 1.15 와 0.92 처럼 곱이 1 을 넘으면
            // 공격 멘탈리티가 공짜 상향이 되어, 상대가 누구든 공격적으로 두는 것이 정답이 된다.
            // 보상은 슈팅 수(SimulateAttack 의 mentalBonus)로 주고, 전력 배수는 거래로 남긴다.
            float am = ts.tactics.instructions.mentality switch
            {
                TeamMentality.VeryAttacking => 1.28f,
                TeamMentality.Attacking     => 1.14f,
                TeamMentality.Defensive     => 0.88f,
                TeamMentality.VeryDefensive => 0.78f,
                _                           => 1.00f,
            };
            float dm = 1f / am;
            ts.effectiveAttack  = ts.attackPower  * am;
            ts.effectiveDefense = ts.defensePower * dm;
        }

        // ── 환경 ─────────────────────────────────────────────────────────────

        internal static void ApplyEnvironment(
            TeamState home, TeamState away,
            WeatherCondition weather, PitchCondition pitch)
        {
            float wm = weather switch
            {
                WeatherCondition.HeavyRain => 0.90f,
                WeatherCondition.Snowy     => 0.85f,
                WeatherCondition.Windy     => 0.95f,
                _ => 1.0f,
            };
            float pm = pitch switch
            {
                PitchCondition.Heavy  => 0.92f,
                PitchCondition.Frozen => 0.88f,
                PitchCondition.Dry    => 0.96f,
                _ => 1.0f,
            };
            float m = wm * pm;
            home.attackPower   *= m; away.attackPower   *= m;
            home.midfieldPower *= m; away.midfieldPower *= m;

            // 홈 어드밴티지 — 관중, 이동 피로, 익숙한 경기장.
            // 이 함수는 ApplyMentality 보다 먼저 불려야 한다. 멘탈리티가 attackPower 로부터
            // effectiveAttack 을 다시 계산하므로, 여기서 attackPower 를 올려 두면 그대로 전달된다.
            // 수비에는 걸지 않는다 — 공격과 중원에만 붙는 것이 측정된 홈 승률의 전제다.
            float ha = FootballTactician.Balance.BalanceConfig.HomeAdvantage;
            home.attackPower    *= ha;
            home.midfieldPower  *= ha;
        }

        // ── 점유율 ────────────────────────────────────────────────────────────

        internal static int CalcPossession(TeamState home, TeamState away)
        {
            float pe = FootballTactician.Balance.BalanceConfig.PossessionExponent;
            float hm = MathF.Pow(home.midfieldPower + 0.1f, pe);
            float am = MathF.Pow(away.midfieldPower  + 0.1f, pe);
            float share = hm / (hm + am);

            // 압박 강도 차이로 미세 조정
            float pressDiff = (home.tactics.instructions.pressingIntensity
                             - away.tactics.instructions.pressingIntensity) / 5f;
            share += pressDiff * 0.05f;

            // 수비 라인, 빌드업, 전방 압박이 공을 쥐는 시간을 가른다.
            share += home.profile.possessionBonus - away.profile.possessionBonus;
            share += home.piPossession - away.piPossession;

            return (int)(SimMath.Clamp(share, 0.30f, 0.70f) * 100);
        }

        // ── 블록 시뮬레이션 (5분 단위) ───────────────────────────────────────

        internal static void SimulateBlock(
            TeamState home, TeamState away,
            int homePoss, int blockStart,
            List<MatchEvent> events,
            MatchStats hStats, MatchStats aStats,
            ref int homeGoals, ref int awayGoals,
            Random rng)
        {
            float hp = homePoss / 100f;
            float ap = 1f - hp;

            // 피로는 블록이 시작할 때 쌓는다. 0~1 로 정규화해 18블록(90분) 내내
            // 최대 강도로 뛰면 1 에 닿도록 18 로 나눈다.
            home.fatigue += (home.profile.fatiguePerBlock + home.piFatigue) / 18f;
            away.fatigue += (away.profile.fatiguePerBlock + away.piFatigue) / 18f;

            SimulateAttack(home, away, hp, blockStart,     events, hStats, ref homeGoals, rng);
            SimulateAttack(away, home, ap, blockStart + 2, events, aStats, ref awayGoals, rng);

            TryCard(home, blockStart,     events, rng);
            TryCard(away, blockStart + 1, events, rng);
        }

        // ── 공격 시도 ─────────────────────────────────────────────────────────

        private static void SimulateAttack(
            TeamState att, TeamState def,
            float possession, int minute,
            List<MatchEvent> events,
            MatchStats attStats,
            ref int attGoals,
            Random rng)
        {
            float mentalBonus = att.tactics.instructions.mentality switch
            {
                TeamMentality.VeryAttacking => +0.15f,
                TeamMentality.Attacking     => +0.08f,
                TeamMentality.Defensive     => -0.08f,
                TeamMentality.VeryDefensive => -0.15f,
                _ => 0f,
            };
            float attackRate = SimMath.Clamp(
                FootballTactician.Balance.BalanceConfig.MatchAttackBase
                + (possession - 0.5f) * 0.6f + mentalBonus + att.profile.attackRateBonus,
                0.28f, 0.93f);

            if (rng.NextDouble() >= attackRate) return;

            // 공격 시도의 일부는 열린 플레이 대신 세트피스로 끝난다.
            // 얹는 것이 아니라 대체하는 것이라 리그 득점이 흔들리지 않는다.
            if (SetPieces.Roll(rng) is { } kind)
            {
                ResolveSetPiece(att, def, kind, minute, events, attStats, ref attGoals, rng);
                return;
            }

            ResolveOpenPlay(att, def, minute, events, attStats, ref attGoals, rng);
        }

        /// <summary>열린 플레이 한 번. 세트피스 추첨은 거치지 않는다.</summary>
        private static void ResolveOpenPlay(
            TeamState att, TeamState def,
            int minute,
            List<MatchEvent> events,
            MatchStats attStats,
            ref int attGoals,
            Random rng)
        {
            // 공격 품질 비율 (1.0 = 동등)
            // 경기 중 피로는 공격 쪽만이 아니라 수비 쪽에도 걸린다. 압박을 세게 건 팀이
            // 후반에 무너지는 것이 압박의 대가다.
            float ratio = att.effectiveAttack * att.FatigueFactor
                        / MathF.Max(1f, def.effectiveDefense * def.FatigueFactor);
            // 기준 비율로 정규화한 뒤 지수를 적용한다. 그래야 지수가 전력 격차만 증폭하고
            // 동급 경기의 득점 수준은 건드리지 않는다.
            float qref = FootballTactician.Balance.BalanceConfig.QualityReference;
            float qr   = qref * MathF.Pow(ratio / qref, FootballTactician.Balance.BalanceConfig.QualityExponent);

            // 전술 보정은 지수를 통과한 뒤에 건다. 앞에 걸면 지수가 증폭해 폭이 몇 배가 된다.
            qr *= att.profile.qualityMul * att.piQuality;

            var bc = FootballTactician.Balance.BalanceConfig.BigChanceRate * att.profile.bigChanceMul;
            bool isBig = rng.NextDouble() < SimMath.Clamp(bc * qr, 0.03f, 0.45f);
            float xg   = isBig
                ? SimMath.Clamp(FootballTactician.Balance.BalanceConfig.BigChanceXgCoef * qr,
                                FootballTactician.Balance.BalanceConfig.BigChanceXgMin,
                                FootballTactician.Balance.BalanceConfig.BigChanceXgMax)
                : SimMath.Clamp(FootballTactician.Balance.BalanceConfig.NormalShotXgCoef * qr,
                                FootballTactician.Balance.BalanceConfig.NormalShotXgMin,
                                FootballTactician.Balance.BalanceConfig.NormalShotXgMax);

            // GK 보정. gkPower 는 골키퍼 능력치 평균(1~20)에 컨디션과 사기를 곱한 값이라
            // 1부 리그 주전 수준에서 1.0 이 되도록 맞춘다.
            // 전에는 100 으로 나눠 0.67 쯤이 나왔고, 모든 슈팅의 득점 확률이 1.5배로 부풀려져 있었다.
            float gkFactor = SimMath.Clamp(
                (1f - FootballTactician.Balance.BalanceConfig.GkSwing)
                + def.gkPower / FootballTactician.Balance.BalanceConfig.GkNeutralPower
                  * FootballTactician.Balance.BalanceConfig.GkSwing,
                0.45f, 1.45f);
            float goalProb = xg / gkFactor;

            // 누가 때리는지 먼저 정한다. "많이 때려라" 를 지시받은 선수는 슈팅이 늘지만
            // 하나하나의 질이 떨어지므로, 슈터를 정한 뒤에 질을 걸어야 한다.
            int scorer = PickWeighted(att.attackerWeights, rng);
            if (att.shotQualityOf.TryGetValue(scorer, out float sq))
            {
                xg *= sq;
                goalProb = xg / gkFactor;
            }

            attStats.shots++;
            // 기대 득점은 시도한 모든 슈팅에 쌓는다. 득점과 유효 슈팅에만 더하면
            // 화면에 보이는 xG 가 실제보다 한참 낮게 나온다.
            attStats.xG += xg;
            if (isBig) attStats.bigChances++;

            if (rng.NextDouble() < goalProb)
            {
                int? assister = rng.NextDouble() < 0.72
                    ? PickWeighted(att.creatorWeights, rng, exclude: scorer)
                    : (int?)null;
                if (assister == scorer) assister = null;

                events.Add(new MatchEvent
                {
                    minute         = minute + rng.Next(0, 5),
                    eventType      = MatchEventType.Goal,
                    clubId         = att.clubId,
                    playerId       = scorer,
                    assistPlayerId = assister,
                });
                attGoals++;
                attStats.shotsOnTarget++;
            }
            else
            {
                bool sot = rng.NextDouble() < 0.30f;
                if (isBig && !sot) attStats.bigChancesMissed++;

                if (sot)
                {
                    attStats.shotsOnTarget++;

                    if (rng.NextDouble() < 0.08f)
                    {
                        events.Add(new MatchEvent
                        {
                            minute    = minute,
                            eventType = MatchEventType.PostHit,
                            clubId    = att.clubId,
                            playerId  = -1,
                        });
                    }
                    else if (isBig && rng.NextDouble() < 0.50f)
                    {
                        events.Add(new MatchEvent
                        {
                            minute    = minute,
                            eventType = MatchEventType.BigSave,
                            clubId    = def.clubId,
                            playerId  = def.goalkeeper?.id ?? -1,
                        });
                    }
                }
            }
        }

        // ── 카드 ──────────────────────────────────────────────────────────────

        private static void TryCard(
            TeamState ts, int minute,
            List<MatchEvent> events, Random rng)
        {
            float press   = ts.tactics.instructions.pressingIntensity / 5f;
            float cardProb = 0.025f * (0.7f + 0.6f * press);

            if (rng.NextDouble() >= cardProb) return;

            int pid = PickWeighted(ts.cardRiskWeights, rng);
            if (pid < 0) return;

            bool alreadyBooked = false;
            foreach (var e in events)
            {
                if (e.clubId == ts.clubId && e.playerId == pid &&
                    e.eventType == MatchEventType.YellowCard)
                {
                    alreadyBooked = true;
                    break;
                }
            }

            events.Add(new MatchEvent
            {
                minute    = minute,
                eventType = alreadyBooked ? MatchEventType.SecondYellow : MatchEventType.YellowCard,
                clubId    = ts.clubId,
                playerId  = pid,
            });

            // 직접 퇴장 (매우 희귀)
            if (!alreadyBooked && rng.NextDouble() < 0.003f)
            {
                events.Add(new MatchEvent
                {
                    minute    = minute,
                    eventType = MatchEventType.RedCard,
                    clubId    = ts.clubId,
                    playerId  = pid,
                });
            }
        }

        // ── 파생 스탯 채우기 ──────────────────────────────────────────────────

        internal static void FillDerivedStats(
            TeamState home, TeamState away,
            int homePoss,
            MatchStats hStats, MatchStats aStats)
        {
            hStats.passes      = (int)(55 + homePoss * 0.8f);
            aStats.passes      = (int)(55 + (100 - homePoss) * 0.8f);
            hStats.passAccuracy = SimMath.Clamp(68f + home.midfieldPower * 0.25f, 55f, 95f);
            aStats.passAccuracy = SimMath.Clamp(68f + away.midfieldPower * 0.25f, 55f, 95f);

            hStats.tackles      = (int)(12 + (100 - homePoss) * 0.15f);
            aStats.tackles      = (int)(12 + homePoss * 0.15f);
            hStats.interceptions = (int)(hStats.tackles * 0.55f);
            aStats.interceptions = (int)(aStats.tackles * 0.55f);

            // 코너는 ResolveSetPiece 가 실제로 센다. 슈팅 수로 추정하면
            // 세트피스에서 나온 득점과 코너 수가 서로 어긋난다.
            hStats.fouls        = (int)(8 + home.tactics.instructions.pressingIntensity * 1.5f);
            aStats.fouls        = (int)(8 + away.tactics.instructions.pressingIntensity * 1.5f);

            hStats.aerialDuelsWon = (int)(5 + home.defensePower * 0.08f);
            aStats.aerialDuelsWon = (int)(5 + away.defensePower * 0.08f);
            hStats.clearances    = (int)(8 + (100 - homePoss) * 0.1f);
            aStats.clearances    = (int)(8 + homePoss * 0.1f);
        }

        internal static void ApplyCardsFromEvents(
            List<MatchEvent> events,
            MatchStats hStats, MatchStats aStats,
            int homeClubId)
        {
            foreach (var e in events)
            {
                var stats = e.clubId == homeClubId ? hStats : aStats;
                if (e.eventType == MatchEventType.YellowCard ||
                    e.eventType == MatchEventType.SecondYellow)
                    stats.yellowCards++;
                else if (e.eventType == MatchEventType.RedCard)
                    stats.redCards++;
            }
        }

        // ── 평점 계산 ─────────────────────────────────────────────────────────

        private static float CalculateRating(
            Position pos,
            int goals, int assists, int yellows, int reds,
            int goalsAgainst, bool cleanSheet)
        {
            float r = 6.5f;
            r += goals   * 1.2f;
            r += assists * 0.6f;
            r -= yellows * 0.3f;
            r -= reds    * 1.5f;

            bool isDefensive = (pos & (Position.GK | Position.CB |
                                       Position.LB  | Position.RB |
                                       Position.LWB | Position.RWB)) != 0;
            if (isDefensive)
            {
                if (cleanSheet)       r += 0.5f;
                if (goalsAgainst >= 3) r -= 0.4f;
                if ((pos & Position.GK) != 0 && cleanSheet) r += 0.3f;
            }

            return SimMath.Clamp(r, 1.0f, 10.0f);
        }

        // ── 유틸리티 ──────────────────────────────────────────────────────────

        private static int PickWeighted(
            (PlayerData p, float w)[] cands, Random rng, int exclude = -1)
        {
            if (cands == null || cands.Length == 0) return -1;

            float total = 0;
            foreach (var (p, w) in cands)
                if (p != null && p.id != exclude) total += w;

            if (total <= 0)
            {
                foreach (var (p, _) in cands)
                    if (p != null && p.id != exclude) return p.id;
                return -1;
            }

            double roll = rng.NextDouble() * total;
            foreach (var (player, weight) in cands)
            {
                if (player == null || player.id == exclude) continue;
                roll -= weight;
                if (roll <= 0) return player.id;
            }

            foreach (var (p, _) in cands)
                if (p != null && p.id != exclude) return p.id;
            return -1;
        }

    }
}
