using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
using JevTicketRouter.Domain.Triage;

namespace JevTicketRouter.Infrastructure.Decisions.Local;

/// <summary>The decision the local model is asked to return, before validation.</summary>
public sealed record LocalDecisionPayload
{
    /// <summary>One of the <see cref="TicketCategory"/> names.</summary>
    [JsonPropertyName("category")]
    public string? Category { get; init; }

    /// <summary>Self-reported confidence in the category, 0-1.</summary>
    [JsonPropertyName("category_confidence")]
    public double? CategoryConfidence { get; init; }

    /// <summary>One of the <see cref="TargetTeam"/> names.</summary>
    [JsonPropertyName("target_team")]
    public string? TargetTeam { get; init; }

    /// <summary>Self-reported confidence in the team, 0-1.</summary>
    [JsonPropertyName("target_team_confidence")]
    public double? TargetTeamConfidence { get; init; }

    /// <summary>One of the <see cref="TicketPriority"/> names.</summary>
    [JsonPropertyName("priority")]
    public string? Priority { get; init; }

    /// <summary>Self-reported confidence in the priority, 0-1.</summary>
    [JsonPropertyName("priority_confidence")]
    public double? PriorityConfidence { get; init; }

    /// <summary>Probability that the ticket contains sensitive data, 0-1.</summary>
    [JsonPropertyName("contains_sensitive_data_probability")]
    public double? ContainsSensitiveDataProbability { get; init; }

    /// <summary>Probability that the ticket needs a human, 0-1.</summary>
    [JsonPropertyName("needs_human_review_probability")]
    public double? NeedsHumanReviewProbability { get; init; }
}

/// <summary>
/// The JSON Schema sent to the local endpoint as <c>response_format</c>.
/// <para>
/// Built from the domain enums rather than written out by hand, so adding a category or a team
/// cannot leave the schema behind. Where the server honours <c>strict: true</c>, this constrains
/// decoding so an invalid label is not merely unlikely but unrepresentable. Where it does not, the
/// engine still validates the body itself — the schema is a narrowing, never the only check.
/// </para>
/// </summary>
public static class LocalDecisionSchema
{
    /// <summary>The schema name sent alongside the schema body.</summary>
    public const string SchemaName = "ticket_triage_decision";

    /// <summary>Builds the JSON Schema object.</summary>
    public static JsonObject Build()
    {
        var properties = new JsonObject
        {
            ["category"] = EnumProperty<TicketCategory>("The kind of request this ticket is."),
            ["category_confidence"] = UnitInterval("How certain you are about the category."),
            ["target_team"] = EnumProperty<TargetTeam>("The internal team that should own this ticket."),
            ["target_team_confidence"] = UnitInterval("How certain you are about the team."),
            ["priority"] = EnumProperty<TicketPriority>("How urgently this ticket must be handled."),
            ["priority_confidence"] = UnitInterval("How certain you are about the priority."),
            ["contains_sensitive_data_probability"] = UnitInterval(
                "Probability the ticket text quotes a personal, financial, or secret value."),
            ["needs_human_review_probability"] = UnitInterval(
                "Probability a human triage agent should review before this is routed automatically."),
        };

        return new JsonObject
        {
            ["type"] = "object",
            ["additionalProperties"] = false,
            ["required"] = new JsonArray([.. properties.Select(pair => (JsonNode)pair.Key)]),
            ["properties"] = properties,
        };
    }

    private static JsonObject EnumProperty<TEnum>(string description)
        where TEnum : struct, Enum =>
        new()
        {
            ["type"] = "string",
            ["description"] = description,
            ["enum"] = new JsonArray([.. Enum.GetNames<TEnum>().Select(name => (JsonNode)name)]),
        };

    private static JsonObject UnitInterval(string description) =>
        new()
        {
            ["type"] = "number",
            ["description"] = description,
            ["minimum"] = 0,
            ["maximum"] = 1,
        };
}
