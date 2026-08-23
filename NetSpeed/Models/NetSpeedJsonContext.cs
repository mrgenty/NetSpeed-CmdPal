using System.Text.Json.Serialization;

namespace NetSpeed;

[JsonSourceGenerationOptions(PropertyNameCaseInsensitive = true)]
[JsonSerializable(typeof(LibreSpeedResult))]
internal partial class NetSpeedJsonContext : JsonSerializerContext
{
}
