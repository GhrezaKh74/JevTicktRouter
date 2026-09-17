using System.Text.Json;
using System.Text.Json.Serialization;

namespace JevTicketRouter.Infrastructure.Jev;

/// <summary>Shared JSON settings for talking to the TypeSafe API.</summary>
public static class JevJsonSerialization
{
    /// <summary>
    /// The TypeSafe API uses snake_case field names, which the contracts spell out explicitly with
    /// <see cref="JsonPropertyNameAttribute"/>; no naming policy is applied so those names win.
    /// </summary>
    public static JsonSerializerOptions Options { get; } = new(JsonSerializerDefaults.Web)
    {
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        PropertyNameCaseInsensitive = true,
        WriteIndented = false,
    };
}
