using System.Text.Json.Serialization;

namespace FootballTactician.World
{
    /// <summary>
    /// 세이브 직렬화를 컴파일 시점에 만들어 둔다.
    ///
    /// 브라우저에서 리플렉션 기반 직렬화가 느리다. 4,000명 규모의 세이브 한 번에
    /// JSON 3.3초 · gzip 69ms · base64 91ms 가 나왔다. 병목이 JSON 하나였다.
    /// </summary>
    [JsonSourceGenerationOptions(
        IncludeFields = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingDefault,
        WriteIndented = false)]
    [JsonSerializable(typeof(SaveGame.Envelope))]
    [JsonSerializable(typeof(GameWorld))]
    internal partial class SaveJsonContext : JsonSerializerContext
    {
    }
}
