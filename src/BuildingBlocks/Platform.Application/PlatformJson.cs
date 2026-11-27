using System.Text.Json;
using System.Text.Json.Serialization;
using Platform.Domain.Ids;

namespace Platform.Application;

/// <summary>The single JSON configuration used for outbox payloads, idempotency and audit snapshots.</summary>
public static class PlatformJson
{
    /// <summary>camelCase, enums as strings, strongly typed ids as plain UUIDs.</summary>
    public static JsonSerializerOptions Options { get; } = Create();

    private static JsonSerializerOptions Create()
    {
        var options = new JsonSerializerOptions(JsonSerializerDefaults.Web)
        {
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
            TypeInfoResolver = new System.Text.Json.Serialization.Metadata.DefaultJsonTypeInfoResolver(),
        };
        options.Converters.Add(new JsonStringEnumConverter(JsonNamingPolicy.CamelCase));
        options.Converters.Add(new StronglyTypedIdJsonConverterFactory());
        options.MakeReadOnly();
        return options;
    }
}
