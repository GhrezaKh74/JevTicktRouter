using System.Text.Json.Nodes;
using System.Text.Json.Serialization;

namespace JevTicketRouter.Infrastructure.Decisions.Local;

/// <summary>A request to an OpenAI-compatible <c>/chat/completions</c> endpoint.</summary>
public sealed record LocalChatRequest
{
    /// <summary>The model to run.</summary>
    [JsonPropertyName("model")]
    public required string Model { get; init; }

    /// <summary>The conversation. One system message and one user message.</summary>
    [JsonPropertyName("messages")]
    public required IReadOnlyList<LocalChatMessage> Messages { get; init; }

    /// <summary>Sampling temperature. 0 for a classification task.</summary>
    [JsonPropertyName("temperature")]
    public double Temperature { get; init; }

    /// <summary>Disables streaming; the engine wants one complete body.</summary>
    [JsonPropertyName("stream")]
    public bool Stream { get; init; }

    /// <summary>Structured-output constraint, when the endpoint supports one.</summary>
    [JsonPropertyName("response_format")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public JsonObject? ResponseFormat { get; init; }
}

/// <summary>One chat message.</summary>
/// <param name="Role">Either <c>system</c> or <c>user</c>.</param>
/// <param name="Content">The message text.</param>
public sealed record LocalChatMessage(
    [property: JsonPropertyName("role")] string Role,
    [property: JsonPropertyName("content")] string Content);

/// <summary>The response body of an OpenAI-compatible chat completion.</summary>
public sealed record LocalChatResponse
{
    /// <summary>The model that answered.</summary>
    [JsonPropertyName("model")]
    public string? Model { get; init; }

    /// <summary>Generated choices. The engine reads the first.</summary>
    [JsonPropertyName("choices")]
    public IReadOnlyList<LocalChatChoice>? Choices { get; init; }
}

/// <summary>One generated choice.</summary>
public sealed record LocalChatChoice
{
    /// <summary>The assistant's message.</summary>
    [JsonPropertyName("message")]
    public LocalChatResponseMessage? Message { get; init; }

    /// <summary>Why generation stopped. <c>length</c> means the reply was truncated.</summary>
    [JsonPropertyName("finish_reason")]
    public string? FinishReason { get; init; }
}

/// <summary>The assistant message inside a choice.</summary>
public sealed record LocalChatResponseMessage
{
    /// <summary>The generated text, expected to be the decision JSON.</summary>
    [JsonPropertyName("content")]
    public string? Content { get; init; }
}

/// <summary>The response of <c>GET /v1/models</c>, read only to improve a "no such model" error.</summary>
public sealed record LocalModelList
{
    /// <summary>One entry per model the endpoint can serve.</summary>
    [JsonPropertyName("data")]
    public IReadOnlyList<LocalModelEntry>? Data { get; init; }
}

/// <summary>One model the endpoint can serve.</summary>
public sealed record LocalModelEntry
{
    /// <summary>The model id, as it must be given to <c>LOCAL_AI_MODEL</c>.</summary>
    [JsonPropertyName("id")]
    public string? Id { get; init; }
}
