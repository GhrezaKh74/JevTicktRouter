using System.Text.Json.Serialization;

namespace JevTicketRouter.Application.Jev.Contracts;

/// <summary>
/// The body of <c>POST https://api.typesafe.ai/v1/systemone</c>. One state, evaluated against a map
/// of named questions that all run in parallel in a single call.
/// </summary>
public sealed record JevSystemOneRequest
{
    /// <summary>
    /// The content to evaluate: a string, object, or array. This project sends a structured object so
    /// each part of the ticket keeps a descriptive name.
    /// </summary>
    [JsonPropertyName("state")]
    public required object State { get; init; }

    /// <summary>The model that handles the request, e.g. <c>jev-latest</c>.</summary>
    [JsonPropertyName("model")]
    public required string Model { get; init; }

    /// <summary>The questions to ask. Answers come back under these same keys.</summary>
    [JsonPropertyName("questions")]
    public required IReadOnlyDictionary<string, JevQuestion> Questions { get; init; }
}
