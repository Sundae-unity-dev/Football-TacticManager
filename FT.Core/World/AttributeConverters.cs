using System;
using System.Text.Json;
using System.Text.Json.Serialization;
using FootballTactician.Data;

namespace FootballTactician.World
{
    /// <summary>
    /// 능력치를 이름 없는 숫자 배열로 저장한다.
    ///
    /// 선수 한 명당 능력치가 45개다. JSON 으로 쓰면 "acceleration" 같은 이름이 값보다
    /// 자리를 많이 차지한다. 4,000명이면 이름만으로 2MB 를 넘는다.
    ///
    /// 순서가 포맷이다. 필드를 끼워 넣거나 지우면 기존 세이브를 잘못 읽는다.
    /// 바꿔야 하면 SaveGame.FormatVersion 을 올려야 한다.
    /// </summary>
    static class AttrJson
    {
        public static void WriteArray(Utf8JsonWriter w, ReadOnlySpan<int> values)
        {
            w.WriteStartArray();
            foreach (int v in values) w.WriteNumberValue(v);
            w.WriteEndArray();
        }

        /// <summary>날짜를 하나의 정수로 접는다. 20261017 처럼 읽힌다.</summary>
        public static int PackDate(GameDate d) => d.year * 10000 + d.month * 100 + d.day;

        public static GameDate UnpackDate(int packed)
            => new GameDate(packed / 10000, packed / 100 % 100, packed % 100);

        /// <summary>배열을 읽어 순서대로 채운다. 짧으면 남은 자리는 건드리지 않는다.</summary>
        public static int[] ReadArray(ref Utf8JsonReader r, int expected)
        {
            var values = new int[expected];
            if (r.TokenType != JsonTokenType.StartArray) return values;

            int i = 0;
            while (r.Read() && r.TokenType != JsonTokenType.EndArray)
                if (i < expected) values[i++] = r.GetInt32();

            return values;
        }
    }

    /// <summary>
    /// 날짜를 하나의 정수로 저장한다. 20261017 처럼 읽힌다.
    /// 계약 2개, 부상, 시즌 일정, 소식, 이적 기록마다 들어가서 {year,month,day} 로 쓰면
    /// 이름만으로 수십만 바이트가 된다.
    /// </summary>
    sealed class GameDateConverter : JsonConverter<GameDate>
    {
        public override void Write(Utf8JsonWriter w, GameDate d, JsonSerializerOptions o)
            => w.WriteNumberValue(AttrJson.PackDate(d));

        public override GameDate Read(ref Utf8JsonReader r, Type t, JsonSerializerOptions o)
            => AttrJson.UnpackDate(r.GetInt32());
    }

    sealed class TechnicalConverter : JsonConverter<TechnicalAttributes>
    {
        public override void Write(Utf8JsonWriter w, TechnicalAttributes a, JsonSerializerOptions o)
            => AttrJson.WriteArray(w, new[]
            {
                a.passing, a.dribbling, a.shooting, a.finishing, a.heading, a.crossing,
                a.tackling, a.marking, a.firstTouch, a.longPassing, a.setPieces,
            });

        public override TechnicalAttributes Read(ref Utf8JsonReader r, Type t, JsonSerializerOptions o)
        {
            var v = AttrJson.ReadArray(ref r, 11);
            return new TechnicalAttributes
            {
                passing = v[0], dribbling = v[1], shooting = v[2], finishing = v[3],
                heading = v[4], crossing = v[5], tackling = v[6], marking = v[7],
                firstTouch = v[8], longPassing = v[9], setPieces = v[10],
            };
        }
    }

    sealed class PhysicalConverter : JsonConverter<PhysicalAttributes>
    {
        public override void Write(Utf8JsonWriter w, PhysicalAttributes a, JsonSerializerOptions o)
            => AttrJson.WriteArray(w, new[]
            {
                a.speed, a.acceleration, a.stamina, a.strength, a.jumping, a.agility, a.balance,
            });

        public override PhysicalAttributes Read(ref Utf8JsonReader r, Type t, JsonSerializerOptions o)
        {
            var v = AttrJson.ReadArray(ref r, 7);
            return new PhysicalAttributes
            {
                speed = v[0], acceleration = v[1], stamina = v[2], strength = v[3],
                jumping = v[4], agility = v[5], balance = v[6],
            };
        }
    }

    sealed class MentalConverter : JsonConverter<MentalAttributes>
    {
        public override void Write(Utf8JsonWriter w, MentalAttributes a, JsonSerializerOptions o)
            => AttrJson.WriteArray(w, new[]
            {
                a.vision, a.composure, a.leadership, a.courage, a.workRate, a.concentration,
                a.decisions, a.positioning, a.aggression, a.teamwork, a.offTheBall,
            });

        public override MentalAttributes Read(ref Utf8JsonReader r, Type t, JsonSerializerOptions o)
        {
            var v = AttrJson.ReadArray(ref r, 11);
            return new MentalAttributes
            {
                vision = v[0], composure = v[1], leadership = v[2], courage = v[3],
                workRate = v[4], concentration = v[5], decisions = v[6], positioning = v[7],
                aggression = v[8], teamwork = v[9], offTheBall = v[10],
            };
        }
    }

    sealed class GoalkeeperConverter : JsonConverter<GoalkeeperAttributes>
    {
        public override void Write(Utf8JsonWriter w, GoalkeeperAttributes a, JsonSerializerOptions o)
            => AttrJson.WriteArray(w, new[]
            {
                a.handling, a.diving, a.reflexes, a.positioning, a.communication,
                a.kicking, a.throwing, a.oneOnOne, a.aerialAbility, a.commandArea,
            });

        public override GoalkeeperAttributes Read(ref Utf8JsonReader r, Type t, JsonSerializerOptions o)
        {
            var v = AttrJson.ReadArray(ref r, 10);
            return new GoalkeeperAttributes
            {
                handling = v[0], diving = v[1], reflexes = v[2], positioning = v[3],
                communication = v[4], kicking = v[5], throwing = v[6], oneOnOne = v[7],
                aerialAbility = v[8], commandArea = v[9],
            };
        }
    }

    sealed class HiddenConverter : JsonConverter<HiddenAttributes>
    {
        public override void Write(Utf8JsonWriter w, HiddenAttributes a, JsonSerializerOptions o)
            => AttrJson.WriteArray(w, new[]
            {
                a.consistency, a.importantMatches, a.pressureHandling, a.injuryProneness,
                a.recoveryRate, a.professionalism, a.ambition, a.loyalty, a.temperament,
                a.sportsmanship, a.dirtiness, a.adaptability, a.versatility,
                a.controversy, a.influence,
            });

        public override HiddenAttributes Read(ref Utf8JsonReader r, Type t, JsonSerializerOptions o)
        {
            var v = AttrJson.ReadArray(ref r, 15);
            return new HiddenAttributes
            {
                consistency = v[0], importantMatches = v[1], pressureHandling = v[2],
                injuryProneness = v[3], recoveryRate = v[4], professionalism = v[5],
                ambition = v[6], loyalty = v[7], temperament = v[8], sportsmanship = v[9],
                dirtiness = v[10], adaptability = v[11], versatility = v[12],
                controversy = v[13], influence = v[14],
            };
        }
    }

    /// <summary>
    /// 계약을 이름 없는 숫자 배열로 저장한다.
    ///
    /// 선수 6,000명이면 계약도 6,000개다. JSON 시간은 바이트보다 <b>객체 수</b>에 좌우되므로
    /// 선수당 객체를 하나 줄이는 것이 크기를 줄이는 것보다 크게 먹힌다.
    ///
    /// 보너스·조항·임대 조건은 어디서도 설정하지 않아 늘 null 이다. 쓰이기 시작하면
    /// 여기서 함께 다루고 FormatVersion 을 올려야 한다.
    /// 순서가 곧 포맷이다 — 바꾸면 기존 세이브를 다른 값으로 읽는다.
    /// </summary>
    sealed class ContractConverter : JsonConverter<ContractData>
    {
        public override void Write(Utf8JsonWriter w, ContractData c, JsonSerializerOptions o)
            => AttrJson.WriteArray(w, new[]
            {
                c.clubId, c.weeklyWage,
                AttrJson.PackDate(c.startDate), AttrJson.PackDate(c.expiryDate),
                (int)c.status,
                c.agentId, c.agentFeePercent, c.signingBonus,
                c.imageRightsPercent, c.loyaltyBonus,
                c.hasNoShopClause ? 1 : 0,
            });

        public override ContractData Read(ref Utf8JsonReader r, Type t, JsonSerializerOptions o)
        {
            var v = AttrJson.ReadArray(ref r, 11);
            return new ContractData
            {
                clubId = v[0], weeklyWage = v[1],
                startDate = AttrJson.UnpackDate(v[2]), expiryDate = AttrJson.UnpackDate(v[3]),
                status = (ContractStatus)v[4],
                agentId = v[5], agentFeePercent = v[6], signingBonus = v[7],
                imageRightsPercent = v[8], loyaltyBonus = v[9],
                hasNoShopClause = v[10] != 0,
            };
        }
    }

    /// <summary>
    /// 시즌 기록. 소수점 셋은 1000배 정수로 담는다 — 화면에 소수 둘까지만 쓴다.
    /// </summary>
    sealed class PlayerStatsConverter : JsonConverter<PlayerStats>
    {
        const int Scale = 1000;

        public override void Write(Utf8JsonWriter w, PlayerStats s, JsonSerializerOptions o)
            => AttrJson.WriteArray(w, new[]
            {
                s.appearances, s.goals, s.assists, s.yellowCards, s.redCards,
                (int)MathF.Round(s.averageRating * Scale),
                (int)MathF.Round(s.minutesPlayed),
                (int)MathF.Round(s.xG * Scale),
                (int)MathF.Round(s.xA * Scale),
                s.cleanSheets,
                (int)MathF.Round(s.progressiveCarries),
                (int)MathF.Round(s.pressureSuccessRate * Scale),
            });

        public override PlayerStats Read(ref Utf8JsonReader r, Type t, JsonSerializerOptions o)
        {
            var v = AttrJson.ReadArray(ref r, 12);
            return new PlayerStats
            {
                appearances = v[0], goals = v[1], assists = v[2],
                yellowCards = v[3], redCards = v[4],
                averageRating = v[5] / (float)Scale,
                minutesPlayed = v[6],
                xG = v[7] / (float)Scale,
                xA = v[8] / (float)Scale,
                cleanSheets = v[9],
                progressiveCarries = v[10],
                pressureSuccessRate = v[11] / (float)Scale,
            };
        }
    }


    /// <summary>
    /// 스태프 한 명을 [이름, 성, 숫자…] 로 접는다.
    ///
    /// 스태프 2,540명이 저마다 common·coaching·scouting·medical·analytics·contract 를
    /// 들고 있으면 객체가 1만 개 늘어, 계약·시즌기록을 접어 번 것을 그대로 토해 낸다.
    ///
    /// 역할마다 채워지는 능력 묶음이 하나뿐이므로, 역할을 알면 뒤 숫자가 무엇인지 안다.
    /// 순서가 포맷이다 — 바꾸려면 SaveGame.FormatVersion 을 올려야 한다.
    /// </summary>
    sealed class StaffConverter : JsonConverter<StaffData>
    {
        public override void Write(Utf8JsonWriter w, StaffData s, JsonSerializerOptions o)
        {
            w.WriteStartArray();
            w.WriteStringValue(s.firstName);
            w.WriteStringValue(s.lastName);

            w.WriteNumberValue(s.id);
            w.WriteNumberValue(s.age);
            w.WriteNumberValue((int)s.nationality);
            w.WriteNumberValue((int)s.role);
            w.WriteNumberValue(s.reputation);
            w.WriteNumberValue(s.clubId);

            w.WriteNumberValue(s.common?.motivation ?? 0);
            w.WriteNumberValue(s.common?.manManagement ?? 0);

            w.WriteNumberValue(s.contract?.weeklyWage ?? 0);
            w.WriteNumberValue(s.contract == null ? 0 : AttrJson.PackDate(s.contract.expiryDate));

            // 역할에 해당하는 묶음만 쓴다.
            if (StaffRoom.IsCoach(s.role))
            {
                var c = s.coaching;
                w.WriteNumberValue(c?.tacticalKnowledge ?? 0);
                w.WriteNumberValue(c?.technicalCoaching ?? 0);
                w.WriteNumberValue(c?.physicalCoaching ?? 0);
                w.WriteNumberValue(c?.mentalCoaching ?? 0);
                w.WriteNumberValue(c?.gkCoaching ?? 0);
            }
            else if (StaffRoom.IsScout(s.role))
            {
                var c = s.scouting;
                w.WriteNumberValue(c?.judgingPlayerAbility ?? 0);
                w.WriteNumberValue(c?.judgingPlayerPotential ?? 0);
                w.WriteNumberValue(c?.adaptability ?? 0);
            }
            else if (StaffRoom.IsMedical(s.role))
            {
                var c = s.medical;
                w.WriteNumberValue(c?.physioTherapy ?? 0);
                w.WriteNumberValue(c?.sportsScience ?? 0);
            }
            else
            {
                w.WriteNumberValue(s.analytics?.dataAnalysis ?? 0);
            }

            w.WriteEndArray();
        }

        public override StaffData Read(ref Utf8JsonReader r, Type t, JsonSerializerOptions o)
        {
            var s = new StaffData();
            if (r.TokenType != JsonTokenType.StartArray) return s;

            r.Read(); s.firstName = r.GetString();
            r.Read(); s.lastName = r.GetString();

            // 남은 숫자를 한꺼번에 거둔다 — 지역 함수에는 ref 매개변수를 들일 수 없고,
            // AttrJson.ReadArray 는 StartArray 에서 시작해야 해서 쓸 수 없다(이름 둘을 이미 읽었다).
            var v = new int[16];
            int n = 0;
            while (r.Read() && r.TokenType != JsonTokenType.EndArray)
                if (n < v.Length && r.TokenType == JsonTokenType.Number) v[n++] = r.GetInt32();

            int at = 0;
            int Next() => at < n ? v[at++] : 0;

            s.id = Next();
            s.age = Next();
            s.nationality = (Nationality)Next();
            s.role = (StaffRole)Next();
            s.reputation = Next();
            s.clubId = Next();

            s.common = new StaffAttributesCommon { motivation = Next(), manManagement = Next() };

            int wage = Next();
            int expiry = Next();
            if (wage > 0 || expiry > 0)
                s.contract = new ContractData
                {
                    weeklyWage = wage,
                    expiryDate = expiry > 0 ? AttrJson.UnpackDate(expiry) : default,
                };

            if (StaffRoom.IsCoach(s.role))
                s.coaching = new CoachingAttributes
                {
                    tacticalKnowledge = Next(),
                    technicalCoaching = Next(),
                    physicalCoaching = Next(),
                    mentalCoaching = Next(),
                    gkCoaching = Next(),
                };
            else if (StaffRoom.IsScout(s.role))
                s.scouting = new ScoutingAttributes
                {
                    judgingPlayerAbility = Next(),
                    judgingPlayerPotential = Next(),
                    adaptability = Next(),
                };
            else if (StaffRoom.IsMedical(s.role))
                s.medical = new MedicalAttributes { physioTherapy = Next(), sportsScience = Next() };
            else
                s.analytics = new AnalyticsAttributes { dataAnalysis = Next() };

            return s;
        }
    }

    /// <summary>
    /// 홈그로운 자격을 숫자 넷으로 접는다.
    ///
    /// 선수 6,000명이 저마다 들고 있으면 객체가 6,000개 늘어 세이브가 0.4MB 불어난다.
    /// 불리언 둘은 햇수에서 다시 계산할 수 있지만, 규정이 바뀌어도 세이브를 읽을 수 있게
    /// 그대로 적는다 — 네 숫자는 이름 넷보다 싸다.
    /// </summary>
    sealed class HomegrownConverter : JsonConverter<HomegrownStatus>
    {
        public override void Write(Utf8JsonWriter w, HomegrownStatus h, JsonSerializerOptions o)
            => AttrJson.WriteArray(w, new[]
            {
                h.yearsInNationalLeague,
                h.yearsAtCurrentClub,
                h.isHomegrown ? 1 : 0,
                h.isClubAcademy ? 1 : 0,
            });

        public override HomegrownStatus Read(ref Utf8JsonReader r, Type t, JsonSerializerOptions o)
        {
            var v = AttrJson.ReadArray(ref r, 4);
            return new HomegrownStatus
            {
                yearsInNationalLeague = v[0],
                yearsAtCurrentClub = v[1],
                isHomegrown = v[2] != 0,
                isClubAcademy = v[3] != 0,
            };
        }
    }
}
