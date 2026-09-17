using System.Text.Json.Serialization;

namespace JevTicketRouter.Application.Jev.Contracts;

/// <summary>The response body of the TypeSafe System One evaluation endpoint.</summary>
public sealed record JevSystemOneResponse
{
    /// <summary>The versioned model id that performed the evaluation, e.g. <c>jev-1.13.0</c>.</summary>
    [JsonPropertyName("model")]
    public string Model { get; init; } = string.Empty;

    /// <summary>One answer per question, keyed by the ids used in the request.</summary>
    [JsonPropertyName("answers")]
    public IReadOnlyDictionary<string, JevAnswer> Answers { get; init; } =
        new Dictionary<string, JevAnswer>();

    /// <summary>Token usage for the request.</summary>
    [JsonPropertyName("usage")]
    public JevUsage? Usage { get; init; }
}

/// <summary>Token usage reported by the API. Only input tokens are billed.</summary>
public sealed record JevUsage
{
    /// <summary>Input tokens consumed.</summary>
    [JsonPropertyName("input_tokens")]
    public int InputTokens { get; init; }

    /// <summary>Output tokens produced.</summary>
    [JsonPropertyName("output_tokens")]
    public int OutputTokens { get; init; }
}

/// <summary>
/// A single answer. The API returns a different shape per question type; this is a permissive union
/// so one deserialisation target covers all three, with <see cref="Type"/> selecting the valid fields.
/// </summary>
public sealed record JevAnswer
{
    /// <summary>The answer type: <c>noul</c>, <c>choice</c>, or <c>score</c>.</summary>
    [JsonPropertyName("type")]
    public string Type { get; init; } = string.Empty;

    /// <summary>Noul answers only: the yes/no answer from 0 (no) to 1 (yes).</summary>
    [JsonPropertyName("noul")]
    public double? Noul { get; init; }

    /// <summary>Choice answers only: the highest-probability option.</summary>
    [JsonPropertyName("choice")]
    public string? Choice { get; init; }

    /// <summary>Score answers only: the probability-weighted position across the levels.</summary>
    [JsonPropertyName("score")]
    public double? Score { get; init; }

    /// <summary>Score answers only: each level number mapped back to its description.</summary>
    [JsonPropertyName("legend")]
    public IReadOnlyDictionary<string, string>? Legend { get; init; }

    /// <summary>Choice and score answers: the probability of each option or level. Sums to 1.</summary>
    [JsonPropertyName("probabilities")]
    public IReadOnlyDictionary<string, double>? Probabilities { get; init; }

    /// <summary>
    /// Choice and score answers: how certain the model is, from 0 to 1. Null for noul answers, which
    /// the TypeSafe API does not give a confidence.
    /// </summary>
    [JsonPropertyName("confidence")]
    public double? Confidence { get; init; }
}
