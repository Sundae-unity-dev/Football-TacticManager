using System;
using System.Text.Json;
using System.Text.Json.Serialization;
using FootballTactician.Data;

namespace FootballTactician.World
{
    /// <summary>
    /// 선수 한 명을 이름 없는 배열 하나로 접는다.
    ///
    /// 재 보니 브라우저에서 저장 한 번의 85%가 JSON 쓰기였고, 그중 81%가 선수였다
    /// (6,000명 3,564KB · 396ms. 압축은 80ms, base64 는 5ms 였다). 능력치는 이미
    /// 배열이었지만 선수 자신은 객체라, <c>"internationalGoals"</c> 같은 이름 스물다섯 개를
    /// 6,000번 적고 있었다 — 한 명당 594바이트 중 300바이트가 이름이었다.
    ///
    /// 이름을 빼면 바이트만 주는 것이 아니다. 멤버마다 한 번씩 하던 메타데이터 조회가
    /// 통째로 사라진다. 브라우저의 JSON 시간은 바이트보다 <b>쓰는 멤버 수</b>를 따른다.
    ///
    /// <b>순서가 곧 포맷이다.</b> 자리를 끼워 넣거나 빼면 기존 세이브를 다른 값으로 읽는다.
    /// 바꿔야 하면 <see cref="SaveGame.FormatVersion"/> 을 올린다.
    /// 뒤에 덧붙이는 것은 괜찮다 — 읽는 쪽이 모자란 자리를 기본값으로 둔다.
    ///
    /// 중첩된 묶음(능력치·계약·기록)은 각자의 변환기에 맡긴다. 여기서 다시 펼치면
    /// 같은 규칙이 두 곳에 생긴다.
    /// </summary>
    sealed class PlayerConverter : JsonConverter<PlayerData>
    {
        public override void Write(Utf8JsonWriter w, PlayerData p, JsonSerializerOptions o)
        {
            w.WriteStartArray();

            w.WriteNumberValue(p.id);
            w.WriteStringValue(p.firstName);
            w.WriteStringValue(p.lastName);
            w.WriteNumberValue(AttrJson.PackDate(p.dateOfBirth));
            w.WriteNumberValue((int)p.nationality);
            w.WriteNumberValue((int)p.secondNationality);
            w.WriteNumberValue((int)p.positionFlags);
            w.WriteNumberValue((int)p.preferredPosition);
            w.WriteNumberValue((int)p.preferredFoot);
            w.WriteNumberValue(p.height);
            w.WriteNumberValue(p.weight);
            w.WriteNumberValue(p.potential);
            w.WriteNumberValue((int)p.personality);
            w.WriteNumberValue(p.morale);
            w.WriteNumberValue(p.condition);
            w.WriteNumberValue(p.marketValue);
            w.WriteNumberValue(p.clubId);
            w.WriteNumberValue(p.isOnLoan ? 1 : 0);
            w.WriteNumberValue(p.loanedFromClubId ?? 0);     // 구단 id 는 1부터라 0 이 "없음"
            w.WriteNumberValue((int)p.status);
            w.WriteNumberValue((int)p.transferStatus);
            w.WriteNumberValue(p.caps);
            w.WriteNumberValue(p.internationalGoals);
            w.WriteNumberValue(p.reserveAppearances);

            Put<TechnicalAttributes>(w, p.technical, o);
            Put<PhysicalAttributes>(w, p.physical, o);
            Put<MentalAttributes>(w, p.mental, o);
            Put<GoalkeeperAttributes>(w, p.goalkeeper, o);
            Put<HiddenAttributes>(w, p.hidden, o);
            Put<ContractData>(w, p.contract, o);
            Put<HomegrownStatus>(w, p.homegrown, o);
            JsonSerializer.Serialize(w, p.workPermit, o);     // 외국인 영입에만 붙는다 — 대개 null
            Put<PlayerStats>(w, p.seasonStats, o);
            Put<PlayerStats>(w, p.careerStats, o);

            // 역할과 부상은 비어 있는 선수가 대부분이다 — null 은 네 글자로 끝난다.
            if (p.roleAssignment is { } role)
            {
                w.WriteStartArray();
                w.WriteNumberValue((int)role.role);
                w.WriteNumberValue((int)role.duty);
                w.WriteNumberValue(role.suitability);
                w.WriteEndArray();
            }
            else w.WriteNullValue();

            JsonSerializer.Serialize(w, p.injury, o);

            w.WriteEndArray();
        }

        public override PlayerData Read(ref Utf8JsonReader r, Type t, JsonSerializerOptions o)
        {
            int depth = r.CurrentDepth;          // 이 배열의 닫는 괄호를 알아보려고 잡아 둔다

            var p = new PlayerData
            {
                id = Int(ref r),
                firstName = Str(ref r),
                lastName = Str(ref r),
                dateOfBirth = AttrJson.UnpackDate(Int(ref r)),
                nationality = (Nationality)Int(ref r),
                secondNationality = (Nationality)Int(ref r),
                positionFlags = (Position)Int(ref r),
                preferredPosition = (Position)Int(ref r),
                preferredFoot = (PreferredFoot)Int(ref r),
                height = Int(ref r),
                weight = Int(ref r),
                potential = Int(ref r),
                personality = (Personality)Int(ref r),
                morale = Flt(ref r),
                condition = Flt(ref r),
                marketValue = Int(ref r),
                clubId = Int(ref r),
                isOnLoan = Int(ref r) != 0,
            };

            int from = Int(ref r);
            p.loanedFromClubId = from == 0 ? null : from;

            p.status = (PlayerStatus)Int(ref r);
            p.transferStatus = (TransferStatus)Int(ref r);
            p.caps = Int(ref r);
            p.internationalGoals = Int(ref r);
            p.reserveAppearances = Int(ref r);

            p.technical = Obj<TechnicalAttributes>(ref r, o);
            p.physical = Obj<PhysicalAttributes>(ref r, o);
            p.mental = Obj<MentalAttributes>(ref r, o);
            p.goalkeeper = Obj<GoalkeeperAttributes>(ref r, o);
            p.hidden = Obj<HiddenAttributes>(ref r, o);
            p.contract = Obj<ContractData>(ref r, o);
            p.homegrown = Obj<HomegrownStatus>(ref r, o);
            p.workPermit = Plain<WorkPermitStatus>(ref r, o);
            p.seasonStats = Obj<PlayerStats>(ref r, o);
            p.careerStats = Obj<PlayerStats>(ref r, o);

            r.Read();
            if (r.TokenType == JsonTokenType.StartArray)
            {
                p.roleAssignment = new RoleAssignment
                {
                    role = (PlayerRole)Int(ref r),
                    duty = (RoleDuty)Int(ref r),
                    suitability = Int(ref r),
                };
                r.Read();                        // 역할 배열의 닫는 괄호
            }

            p.injury = Plain<InjuryData>(ref r, o);

            // 변환기는 값의 마지막 토큰에 서서 끝내야 한다. 뒤에 자리가 더 있어도
            // (나중 판에서 덧붙였다면) 이 배열의 닫는 괄호까지 건너뛴다.
            while (!(r.TokenType == JsonTokenType.EndArray && r.CurrentDepth == depth))
                r.Read();

            return p;
        }

        static int Int(ref Utf8JsonReader r) { r.Read(); return r.GetInt32(); }
        static float Flt(ref Utf8JsonReader r) { r.Read(); return r.GetSingle(); }
        static string Str(ref Utf8JsonReader r) { r.Read(); return r.GetString(); }

        static T Obj<T>(ref Utf8JsonReader r, JsonSerializerOptions o)
        {
            r.Read();
            if (r.TokenType == JsonTokenType.Null) return default;
            return Conv<T>.Of(o).Read(ref r, typeof(T), o);
        }

        /// <summary>전용 변환기가 없는 묶음(부상·취업허가)은 기본 직렬화에 맡긴다.</summary>
        static T Plain<T>(ref Utf8JsonReader r, JsonSerializerOptions o)
        {
            r.Read();
            return JsonSerializer.Deserialize<T>(ref r, o);
        }

        /// <summary>중첩 묶음 하나를 쓴다. 없으면 null 로.</summary>
        static void Put<T>(Utf8JsonWriter w, T value, JsonSerializerOptions o)
        {
            if (value == null) w.WriteNullValue();
            else Conv<T>.Of(o).Write(w, value, o);
        }

        /// <summary>
        /// 묶음마다 변환기를 한 번만 찾아 둔다.
        ///
        /// <c>JsonSerializer.Serialize</c> 를 중첩 묶음마다 다시 부르면 선수 한 명에
        /// 아홉 번씩, 6,000명이면 54,000번 타입 정보를 새로 뒤진다 — 이름을 지워 번 시간을
        /// 그대로 토해 낸다. 설정 객체가 바뀌면 다시 찾는다(테스트가 자기 설정을 쓴다).
        /// </summary>
        static class Conv<T>
        {
            sealed record Cached(JsonSerializerOptions Options, JsonConverter<T> Converter);

            static Cached _cached;

            public static JsonConverter<T> Of(JsonSerializerOptions o)
            {
                var c = _cached;
                if (c != null && ReferenceEquals(c.Options, o)) return c.Converter;

                var found = (JsonConverter<T>)o.GetConverter(typeof(T));
                _cached = new Cached(o, found);     // 참조 대입은 쪼개지지 않는다
                return found;
            }
        }
    }
}
