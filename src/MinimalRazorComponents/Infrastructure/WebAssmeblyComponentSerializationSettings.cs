using System.Text.Json.Serialization;
using System.Text.Json;

namespace MinimalRazorComponents.Infrastructure;

internal static class WebAssemblyComponentSerializationSettings
{
    public static readonly JsonSerializerOptions JsonSerializationOptions =
        new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            PropertyNameCaseInsensitive = true,
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
        };
}
