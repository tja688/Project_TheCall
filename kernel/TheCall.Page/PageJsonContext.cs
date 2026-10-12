using System.Runtime.Versioning;
using System.Collections.Generic;
using System.Text.Json.Serialization;
[assembly: SupportedOSPlatform("browser")]
namespace TheCall
{
    [JsonSerializable(typeof(IReadOnlyList<PersonalityDef>))]
    [JsonSerializable(typeof(IReadOnlyList<RaceDef>))]
    [JsonSerializable(typeof(IReadOnlyList<MoodDef>))]
    [JsonSerializable(typeof(IReadOnlyList<MonsterDetails>))]
    internal partial class PageJsonContext : JsonSerializerContext { }
}
