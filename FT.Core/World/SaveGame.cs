using System;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.Json.Serialization.Metadata;
using FootballTactician.Data;

namespace FootballTactician.World
{
    /// <summary>
    /// 세이브 직렬화. 모델이 전부 public 필드라 IncludeFields 를 켠다.
    /// 읽기 전용 계산 속성은 저장할 필요가 없어 리졸버에서 걸러낸다.
    ///
    /// 포맷은 gzip 한 JSON 이다. 2000명 규모에서 원본 수 MB 가 수백 KB 로 줄어 브라우저 저장소에 들어간다.
    /// 더 작은 포맷(MessagePack)은 이적 시장이 붙어 저장량이 늘어나는 P2 에서 다시 본다.
    /// </summary>
    public static class SaveGame
    {
        public const int FormatVersion = 5;   // 선수 한 명을 배열 하나로 접음

        static readonly JsonSerializerOptions Options = BuildOptions();

        static JsonSerializerOptions BuildOptions()
        {
            var o = new JsonSerializerOptions
            {
                IncludeFields = true,
                // 기본값은 적지 않는다. 선수 4,000명의 0 인 통계와 빈 목록이 원본의 절반을 차지한다.
                // 역직렬화하면 없는 필드는 기본값이 되므로 결과는 같다.
                DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingDefault,
                WriteIndented = false,
            };
            o.Converters.Add(new SlimResultConverter());
            o.Converters.Add(new GameDateConverter());

            // 능력치는 이름 없는 배열로. 이름이 값보다 자리를 많이 차지한다.
            o.Converters.Add(new TechnicalConverter());
            o.Converters.Add(new PhysicalConverter());
            o.Converters.Add(new MentalConverter());
            o.Converters.Add(new GoalkeeperConverter());
            o.Converters.Add(new HiddenConverter());
            o.Converters.Add(new ContractConverter());
            o.Converters.Add(new PlayerStatsConverter());
            o.Converters.Add(new StaffConverter());
            o.Converters.Add(new HomegrownConverter());

            // 선수 자신도 배열로. 세이브의 81%가 선수였다 — 자세한 이유는 PlayerConverter 에.
            o.Converters.Add(new PlayerConverter());

            // 구단에서 가장 큰 것은 저장된 전술이고, 그중 대부분이 슬롯 열한 개다.
            o.Converters.Add(new FormationSlotConverter());

            // 소스 생성 리졸버를 먼저 쓴다. 브라우저에서는 리플렉션 직렬화가 느려서,
            // 4,000명 규모의 세이브 한 번에 3.3초가 걸렸다(gzip 은 69ms 였다).
            // 소스 생성이 다루지 못하는 타입은 뒤의 리플렉션 리졸버가 받는다.
            o.TypeInfoResolver = JsonTypeInfoResolver.Combine(
                SaveJsonContext.Default,
                new DefaultJsonTypeInfoResolver { Modifiers = { DropReadOnlyProperties } });

            return o;
        }

        /// <summary>
        /// 경기 결과는 점수만 저장한다.
        ///
        /// 이벤트와 상세 기록은 지난 경기에서 다시 읽히지 않는다. 일정과 순위표는 점수만 쓰고,
        /// 타임라인은 방금 치른 경기에만 보여 주는데 그 값은 새로고침하면 어차피 사라진다.
        /// 3,040경기분을 전부 담으면 세이브가 2MB 가까이 불어난다.
        /// </summary>
        sealed class SlimResultConverter : JsonConverter<MatchResultData>
        {
            public override void Write(Utf8JsonWriter w, MatchResultData v, JsonSerializerOptions o)
            {
                w.WriteStartObject();
                w.WriteNumber("h", v.homeGoals);
                w.WriteNumber("a", v.awayGoals);
                w.WriteEndObject();
            }

            public override MatchResultData Read(
                ref Utf8JsonReader r, Type t, JsonSerializerOptions o)
            {
                int home = 0, away = 0;
                while (r.Read())
                {
                    if (r.TokenType == JsonTokenType.EndObject) break;
                    if (r.TokenType != JsonTokenType.PropertyName) continue;

                    string name = r.GetString();
                    r.Read();
                    if (name == "h") home = r.GetInt32();
                    else if (name == "a") away = r.GetInt32();
                }

                return new MatchResultData
                {
                    homeGoals = home,
                    awayGoals = away,
                    events = new List<MatchEvent>(),
                    homeStats = new MatchStats(),
                    awayStats = new MatchStats(),
                };
            }
        }

        /// <summary>FullName, CurrentAbility 처럼 되돌려 쓸 수 없는 값은 저장하지 않는다.</summary>
        static void DropReadOnlyProperties(JsonTypeInfo info)
        {
            if (info.Kind != JsonTypeInfoKind.Object) return;
            for (int i = info.Properties.Count - 1; i >= 0; i--)
                if (info.Properties[i].Set == null)
                    info.Properties.RemoveAt(i);
        }

        public sealed class Envelope
        {
            public int version = FormatVersion;
            public string savedAt = "";
            public string label = "";
            public GameWorld world;
        }

        public static byte[] Serialize(GameWorld w, string label)
        {
            var env = new Envelope
            {
                version = FormatVersion,
                savedAt = DateTime.UtcNow.ToString("o"),
                label = label ?? "",
                world = w,
            };

            byte[] json = JsonSerializer.SerializeToUtf8Bytes(env, Options);
            using var ms = new MemoryStream();
            using (var gz = new GZipStream(ms, CompressionLevel.Optimal, leaveOpen: true))
                gz.Write(json, 0, json.Length);
            return ms.ToArray();
        }

        /// <summary>
        /// 통째로 읽기 전에 포맷 번호만 먼저 본다. 못 찾으면 0.
        ///
        /// 포맷이 달라지면 변환기가 먼저 터진다 — 자리가 밀린 배열을 만나 "FormationSlot 으로
        /// 바꿀 수 없다" 같은 소리를 한다. 그래서 예전 세이브를 연 사람은 포맷이 달라졌다는
        /// 안내 대신 알 수 없는 오류를 봤다. 숫자 하나는 변환기 없이 훑을 수 있다.
        /// </summary>
        static int PeekVersion(ReadOnlySpan<byte> json)
        {
            var r = new Utf8JsonReader(json);
            if (!r.Read() || r.TokenType != JsonTokenType.StartObject) return 0;

            while (r.Read() && r.TokenType == JsonTokenType.PropertyName)
            {
                bool isVersion = r.ValueTextEquals("version");
                if (!r.Read()) return 0;

                if (isVersion)
                    return r.TokenType == JsonTokenType.Number && r.TryGetInt32(out int v) ? v : 0;

                r.Skip();      // world 는 통째로 건너뛴다 — 여는 괄호만 세고 지나간다
            }
            return 0;
        }

        public static Envelope Deserialize(byte[] packed)
        {
            using var src = new MemoryStream(packed);
            using var gz = new GZipStream(src, CompressionMode.Decompress);
            using var outMs = new MemoryStream();
            gz.CopyTo(outMs);
            byte[] json = outMs.ToArray();

            // 포맷 검사가 먼저다. 읽고 나서 보면 변환기가 먼저 터진다.
            int version = PeekVersion(json);
            if (version != FormatVersion)
                throw new InvalidDataException($"세이브 포맷이 맞지 않습니다 (파일 {version}, 현재 {FormatVersion}).");

            var env = JsonSerializer.Deserialize<Envelope>(json, Options);
            if (env?.world == null) throw new InvalidDataException("세이브를 읽을 수 없습니다.");

            Rehydrate(env.world);
            return env;
        }

        public static string ToBase64(GameWorld w, string label) => Convert.ToBase64String(Serialize(w, label));

        public static Envelope FromBase64(string s) => Deserialize(Convert.FromBase64String(s));

        /// <summary>역직렬화 뒤 캐시와 참조를 되살린다.</summary>
        static void Rehydrate(GameWorld w)
        {
            foreach (var p in w.Players.Values)
            {
                p.recentRatings ??= new();
                p.InvalidateAbilityCache();
            }
            foreach (var league in w.Leagues.Values)
                Standings.Recalculate(league);

            w.GrowthCarry ??= new();
            w.Objectives ??= new();
            w.Inbox ??= new();

            RestoreSlotCoordinates(w);
        }

        /// <summary>
        /// 전술 슬롯 좌표를 포메이션 프리셋에서 되살린다.
        /// 저장하지 않는 값이라 불러온 직후에는 전부 0 이고, 그대로 두면
        /// 전술 보드의 선수가 전부 왼쪽 아래 구석에 겹쳐 그려진다.
        /// </summary>
        static void RestoreSlotCoordinates(GameWorld w)
        {
            foreach (var club in w.Clubs.Values)
            foreach (var tac in club.savedTactics ?? new List<TacticsData>())
            {
                var preset = FormationDatabase.All
                    .FirstOrDefault(p => p.shape == tac.formationShape);
                if (preset == null) continue;

                foreach (var slot in tac.slots)
                {
                    var t = preset.slots.FirstOrDefault(x => x.slotIndex == slot.slotIndex);
                    if (t == null) continue;

                    slot.attackingX = t.attackingX;
                    slot.attackingY = t.attackingY;
                    slot.defendingX = t.defendingX;
                    slot.defendingY = t.defendingY;
                }
            }
        }

        /// <summary>압축 전후 크기 — 저장 UI 에 보여준다.</summary>
        public static (int raw, int packed) Measure(GameWorld w)
            => (JsonBytes(w).Length, Serialize(w, "").Length);

        /// <summary>
        /// 시각과 이름표를 비운 봉투. 같은 세계면 같은 바이트가 나온다 —
        /// 크기를 재거나 지문을 뜨는 데 쓴다.
        /// </summary>
        static byte[] JsonBytes(GameWorld w)
            => JsonSerializer.SerializeToUtf8Bytes(new Envelope { world = w }, Options);

        /// <summary>
        /// 저장 바이트의 지문. 세이브는 모델 한 필드만 움직여도 조용히 달라지고,
        /// 그건 다음 판을 불러올 때야 드러난다 — 포맷을 건드렸는지 값으로 확인한다.
        /// </summary>
        public static string Fingerprint(GameWorld w)
        {
            ulong h = 14695981039346656037UL;
            foreach (byte b in JsonBytes(w))
            {
                h ^= b;
                h *= 1099511628211UL;
            }
            return h.ToString("x16");
        }

        /// <summary>
        /// 저장 비용을 단계별로 쪼갠다. 브라우저에서 저장이 느릴 때 어디를 고쳐야 할지 가린다.
        /// </summary>
        public static (long jsonMs, long gzipMs, long base64Ms, int rawBytes, int packedBytes)
            Profile(GameWorld w)
        {
            var env = new Envelope { version = FormatVersion, savedAt = "", label = "", world = w };

            var sw = System.Diagnostics.Stopwatch.StartNew();
            byte[] json = JsonSerializer.SerializeToUtf8Bytes(env, Options);
            long jsonMs = sw.ElapsedMilliseconds;

            sw.Restart();
            byte[] packed;
            using (var ms = new MemoryStream())
            {
                using (var gz = new GZipStream(ms, CompressionLevel.Optimal, leaveOpen: true))
                    gz.Write(json, 0, json.Length);
                packed = ms.ToArray();
            }
            long gzipMs = sw.ElapsedMilliseconds;

            sw.Restart();
            _ = Convert.ToBase64String(packed);
            long base64Ms = sw.ElapsedMilliseconds;

            return (jsonMs, gzipMs, base64Ms, json.Length, packed.Length);
        }

        /// <summary>
        /// 세이브의 어느 조각이 비싼지 — 조각마다 바이트와 시간을 따로 잰다.
        ///
        /// 브라우저에서 저장 한 번의 85%가 JSON 쓰기다(압축은 80ms, base64 는 5ms).
        /// 그 비용은 바이트가 아니라 <b>쓰는 멤버 수</b>를 따르므로, 줄이려면
        /// 어느 목록이 몇 개짜리인지부터 봐야 한다. 조각의 합은 전체와 정확히 같지 않다 —
        /// 봉투와 바깥 필드가 빠지고, 조각마다 여는 괄호가 더 붙는다.
        /// </summary>
        public static List<(string part, int count, int bytes, double ms)> Split(GameWorld w)
        {
            var rows = new List<(string, int, int, double)>();

            void Take<T>(string name, int count, T value)
            {
                var sw = System.Diagnostics.Stopwatch.StartNew();
                byte[] b = JsonSerializer.SerializeToUtf8Bytes(value, Options);
                rows.Add((name, count, b.Length, sw.Elapsed.TotalMilliseconds));
            }

            Take("선수", w.Players.Count, w.Players);
            Take("구단", w.Clubs.Count, w.Clubs);
            Take("리그(일정 포함)", w.Leagues.Count, w.Leagues);
            Take("스태프", w.Staff.Count, w.Staff);
            Take("컵", w.Cups.Count, w.Cups);
            Take("이적 기록", w.Transfers.Count, w.Transfers);
            Take("임대", w.Loans.Count, w.Loans);
            Take("받은 소식", w.Inbox.Count, w.Inbox);
            Take("시상", w.Awards.Count, w.Awards);
            Take("커리어", w.CareerHistory.Count, w.CareerHistory);

            rows.Sort((a, b) => b.Item4.CompareTo(a.Item4));
            return rows;
        }

        /// <summary>
        /// 한 선수가 실제로 어떤 JSON 이 되는지. 선수가 세이브의 80%라
        /// 무엇이 자리를 차지하는지는 한 명을 눈으로 보는 것이 가장 빠르다.
        /// </summary>
        public static string SampleJson(PlayerData p) => JsonSerializer.Serialize(p, Options);
    }
}
