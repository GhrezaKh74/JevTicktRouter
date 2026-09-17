using System.Text.Json.Serialization;

namespace JevTicketRouter.Application.Jev.Contracts;

/// <summary>
/// A single typed question in a TypeSafe System One request. The three derived types map 1:1 to the
/// three primitives documented at <c>https://docs.typesafe.ai/primitives</c>, and the JSON
/// discriminator is the <c>type</c> field the API expects.
/// </summary>
[JsonPolymorphic(TypeDiscriminatorPropertyName = "type")]
[JsonDerivedType(typeof(JevNoulQuestion), "noul")]
[JsonDerivedType(typeof(JevChoiceQuestion), "choice")]
[JsonDerivedType(typeof(JevScoreQuestion), "score")]
public abstract record JevQuestion
{
    /// <summary>What the model should decide. A string, object, or array.</summary>
    [JsonPropertyName("instructions")]
    public required string Instructions { get; init; }
}

/// <summary>
/// A yes/no question. The answer is a probability from 0 (no) to 1 (yes) and carries no confidence.
/// </summary>
public sealed record JevNoulQuestion : JevQuestion
{
    /// <summary>Optional descriptions of what a yes and a no mean, keyed <c>true</c> and <c>false</c>.</summary>
    [JsonPropertyName("criteria")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public IReadOnlyDictionary<string, string>? Criteria { get; init; }
}

/// <summary>
/// Picks one option from a defined set. The answer carries the chosen option, the full probability
/// distribution, and a confidence value.
/// </summary>
public sealed record JevChoiceQuestion : JevQuestion
{
    /// <summary>Map of option name to a rubric description for that option.</summary>
    [JsonPropertyName("criteria")]
    public required IReadOnlyDictionary<string, string?> Criteria { get; init; }
}

/// <summary>
/// Rates the state against ordered levels. The answer carries a probability-weighted score, the
/// distribution over levels, a legend, and a confidence value.
/// </summary>
public sealed record JevScoreQuestion : JevQuestion
{
    /// <summary>Ordered level descriptions, lowest first. At least two are required by the API.</summary>
    [JsonPropertyName("criteria")]
    public required IReadOnlyList<string> Criteria { get; init; }
}
