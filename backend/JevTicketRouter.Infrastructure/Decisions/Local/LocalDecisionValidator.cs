using System.Diagnostics.CodeAnalysis;
using System.Text.Json;
using JevTicketRouter.Domain.Decisions;
using JevTicketRouter.Domain.Triage;

namespace JevTicketRouter.Infrastructure.Decisions.Local;

/// <summary>The outcome of validating a local model's reply.</summary>
/// <param name="Result">The parsed decision, or null when validation failed.</param>
/// <param name="Error">Why validation failed, or null on success. Never contains ticket text.</param>
public readonly record struct LocalDecisionValidation(DecisionResult? Result, string? Error)
{
    /// <summary>True when the reply produced a usable decision.</summary>
    [MemberNotNullWhen(true, nameof(Result))]
    [MemberNotNullWhen(false, nameof(Error))]
    public bool IsValid => Result is not null;

    /// <summary>A successful validation.</summary>
    public static LocalDecisionValidation Success(DecisionResult result) => new(result, null);

    /// <summary>A failed validation.</summary>
    public static LocalDecisionValidation Failure(string error) => new(null, error);
}

/// <summary>
/// Turns the content of a local model's chat completion into a <see cref="DecisionResult"/>.
/// <para>
/// A local instruction-tuned model is far less disciplined than a purpose-built classifier: it may
/// wrap JSON in prose or a code fence, invent a label, omit a field, or return a confidence outside
/// 0-1. Every one of those is reported as a validation failure. Nothing is defaulted, inferred, or
/// filled in — a ticket routed on a guessed value would be worse than a ticket that failed loudly.
/// </para>
/// </summary>
public static class LocalDecisionValidator
{
    private static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.Web)
    {
        PropertyNameCaseInsensitive = true,
    };

    /// <summary>Validates and parses a model reply.</summary>
    /// <param name="content">The assistant message content.</param>
    /// <param name="model">The model id to record on the result.</param>
    public static LocalDecisionValidation Validate(string? content, string model)
    {
        if (string.IsNullOrWhiteSpace(content))
        {
            return LocalDecisionValidation.Failure("The local model returned an empty message.");
        }

        var json = ExtractJsonObject(content);

        if (json is null)
        {
            return LocalDecisionValidation.Failure(
                "The local model's reply did not contain a JSON object.");
        }

        LocalDecisionPayload? payload;
        try
        {
            payload = JsonSerializer.Deserialize<LocalDecisionPayload>(json, SerializerOptions);
        }
        catch (JsonException exception)
        {
            return LocalDecisionValidation.Failure(
                $"The local model's reply was not valid JSON: {exception.Message}");
        }

        if (payload is null)
        {
            return LocalDecisionValidation.Failure("The local model's reply deserialised to nothing.");
        }

        if (!TryParseEnum<TicketCategory>(payload.Category, out var category, out var categoryError))
        {
            return LocalDecisionValidation.Failure($"Field 'category': {categoryError}");
        }

        if (!TryParseEnum<TargetTeam>(payload.TargetTeam, out var team, out var teamError))
        {
            return LocalDecisionValidation.Failure($"Field 'target_team': {teamError}");
        }

        if (!TryParseEnum<TicketPriority>(payload.Priority, out var priority, out var priorityError))
        {
            return LocalDecisionValidation.Failure($"Field 'priority': {priorityError}");
        }

        if (!TryReadUnit(payload.CategoryConfidence, "category_confidence", out var categoryConfidence, out var error)
            || !TryReadUnit(payload.TargetTeamConfidence, "target_team_confidence", out var teamConfidence, out error)
            || !TryReadUnit(payload.PriorityConfidence, "priority_confidence", out var priorityConfidence, out error)
            || !TryReadUnit(
                payload.ContainsSensitiveDataProbability,
                "contains_sensitive_data_probability",
                out var sensitive,
                out error)
            || !TryReadUnit(
                payload.NeedsHumanReviewProbability,
                "needs_human_review_probability",
                out var review,
                out error))
        {
            return LocalDecisionValidation.Failure(error);
        }

        return LocalDecisionValidation.Success(new DecisionResult(
            category,
            categoryConfidence,
            team,
            teamConfidence,
            priority,
            priorityConfidence,
            (int)priority,
            sensitive,
            review,
            string.IsNullOrWhiteSpace(model) ? "unknown" : model,
            AiProvider.Local));
    }

    /// <summary>
    /// Finds the first balanced JSON object in the reply, so a model that wrapped its answer in a
    /// code fence or a sentence of preamble is still usable. Anything beyond that is a failure.
    /// </summary>
    private static string? ExtractJsonObject(string content)
    {
        var start = content.IndexOf('{', StringComparison.Ordinal);

        if (start < 0)
        {
            return null;
        }

        var depth = 0;
        var inString = false;
        var escaped = false;

        for (var i = start; i < content.Length; i++)
        {
            var c = content[i];

            if (escaped)
            {
                escaped = false;
                continue;
            }

            if (inString)
            {
                if (c == '\\')
                {
                    escaped = true;
                }
                else if (c == '"')
                {
                    inString = false;
                }

                continue;
            }

            switch (c)
            {
                case '"':
                    inString = true;
                    break;
                case '{':
                    depth++;
                    break;
                case '}':
                    depth--;
                    if (depth == 0)
                    {
                        return content[start..(i + 1)];
                    }

                    break;
                default:
                    break;
            }
        }

        // Unbalanced braces: a truncated reply, usually a hit token limit.
        return null;
    }

    private static bool TryParseEnum<TEnum>(string? value, out TEnum parsed, out string error)
        where TEnum : struct, Enum
    {
        parsed = default;

        if (string.IsNullOrWhiteSpace(value))
        {
            error = "missing.";
            return false;
        }

        if (!Enum.TryParse(value.Trim(), ignoreCase: true, out parsed) || !Enum.IsDefined(parsed))
        {
            error = $"'{value}' is not one of {string.Join(", ", Enum.GetNames<TEnum>())}.";
            return false;
        }

        error = string.Empty;
        return true;
    }

    private static bool TryReadUnit(double? value, string field, out double parsed, out string error)
    {
        parsed = 0;

        if (value is not { } number)
        {
            error = $"Field '{field}': missing.";
            return false;
        }

        if (double.IsNaN(number) || double.IsInfinity(number))
        {
            error = $"Field '{field}': not a finite number.";
            return false;
        }

        if (number is < 0 or > 1)
        {
            error = $"Field '{field}': {number} is outside the range 0 to 1.";
            return false;
        }

        parsed = number;
        error = string.Empty;
        return true;
    }
}
