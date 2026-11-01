using System;
using System.Text.Json;
using System.Text.Json.Serialization;
using FootballTactician.Data;

namespace FootballTactician.World
{
    /// <summary>
    /// 전술 슬롯 하나를 이름 없는 배열로 접는다.
    ///
    /// 구단 한 곳이 2,403바이트인데 그중 1,227이 저장된 전술이고, 다시 그중 840이
    /// 슬롯 열한 개였다. 슬롯 하나가
    /// <c>{"slotIndex":1,"position":8,"role":{"role":8,"duty":1},"assignedPlayerId":17}</c>
    /// 로 76바이트인데, 값은 숫자 다섯 개뿐이다. 240구단 × 11자리면 이름만 180KB 다.
    ///
    /// <b>순서가 곧 포맷이다.</b> 바꾸려면 <see cref="SaveGame.FormatVersion"/> 을 올린다.
    /// 개인 지시는 감독이 손댄 자리에만 붙으므로, 없으면 아예 적지 않고 배열을 닫는다.
    /// </summary>
    sealed class FormationSlotConverter : JsonConverter<FormationSlot>
    {
        /// <summary>역할이 비어 있는 자리. 역할 번호에 쓸 수 없는 값이어야 한다.</summary>
        const int NoRole = -1;

        public override void Write(Utf8JsonWriter w, FormationSlot s, JsonSerializerOptions o)
        {
            w.WriteStartArray();

            w.WriteNumberValue(s.slotIndex);
            w.WriteNumberValue((int)s.position);
            w.WriteNumberValue(s.role == null ? NoRole : (int)s.role.role);
            w.WriteNumberValue(s.role == null ? 0 : (int)s.role.duty);
            w.WriteNumberValue(s.role == null ? 0 : s.role.suitability);
            w.WriteNumberValue(s.assignedPlayerId);

            // 좌표(attackingX…)는 [JsonIgnore] 다 — 프리셋에서 되살린다.
            if (s.customInstructions != null)
                JsonSerializer.Serialize(w, s.customInstructions, o);

            w.WriteEndArray();
        }

        public override FormationSlot Read(ref Utf8JsonReader r, Type t, JsonSerializerOptions o)
        {
            int depth = r.CurrentDepth;

            var s = new FormationSlot
            {
                slotIndex = Int(ref r),
                position = (Position)Int(ref r),
            };

            int role = Int(ref r);
            int duty = Int(ref r);
            int fit = Int(ref r);
            if (role != NoRole)
                s.role = new RoleAssignment
                {
                    role = (PlayerRole)role,
                    duty = (RoleDuty)duty,
                    suitability = fit,
                };

            s.assignedPlayerId = Int(ref r);

            r.Read();
            if (r.TokenType != JsonTokenType.EndArray)
                s.customInstructions = JsonSerializer.Deserialize<PlayerInstructions>(ref r, o);

            while (!(r.TokenType == JsonTokenType.EndArray && r.CurrentDepth == depth))
                r.Read();

            return s;
        }

        static int Int(ref Utf8JsonReader r) { r.Read(); return r.GetInt32(); }
    }
}
