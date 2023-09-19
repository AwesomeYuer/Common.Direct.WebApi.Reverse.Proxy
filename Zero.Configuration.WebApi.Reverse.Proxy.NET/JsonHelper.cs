using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace Microshaoft;

public static class JsonHelper
{
    public static string AsJsonEscapeUnsafeRelaxedJson(this string @this, bool writeIndented = true)
    {
        return JsonSerializer
                        .Serialize
                                (
                                    JsonSerializer.Deserialize<JsonNode>(@this)
                                    , new JsonSerializerOptions()
                                    {
                                        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping
                                        , WriteIndented = writeIndented
                                    }
                                );
    }
}
