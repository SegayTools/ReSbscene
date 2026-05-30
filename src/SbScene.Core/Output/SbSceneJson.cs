using System.Text.Json;
using System.Text.Json.Serialization;

namespace SbScene.Core.Output;

public static class SbSceneJson
{
    public static JsonSerializerOptions CreateOptions(bool indented)
    {
        return new JsonSerializerOptions
        {
            WriteIndented = indented,
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            DictionaryKeyPolicy = JsonNamingPolicy.CamelCase,
        };
    }
}
