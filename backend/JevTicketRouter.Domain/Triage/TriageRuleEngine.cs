namespace JevTicketRouter.Domain.Triage;

/// <summary>
/// Applies the deterministic business rules that sit between the decision engine's assessment and
/// the final routing decision. The engine proposes; this code disposes, whichever provider ran. Every override is recorded as an
/// <see cref="AppliedRule"/> so the outcome can always be explained.
/// </summary>
public static class TriageRuleEngine
{
    /// <summary>Escalation triggered by the category or priority of the ticket itself.</summary>
    public const string SecurityEscalationRuleId = "SECURITY_OR_CRITICAL_ESCALATION";

    /// <summary>Escalation triggered by Jev reporting low confidence.</summary>
    public const string LowConfidenceRuleId = "LOW_CONFIDENCE_ESCALATION";

    /// <summary>Redaction triggered by detected sensitive data.</summary>
    public const string SensitiveDataRedactionRuleId = "SENSITIVE_DATA_REDACTION";

    /// <summary>Escalation the decision engine itself asked for.</summary>
    public const string ModelRequestedReviewRuleId = "MODEL_REQUESTED_REVIEW";

    /// <summary>
    /// Turns an engine assessment into the final decision.
    /// </summary>
    /// <param name="assessment">What the model proposed.</param>
    /// <param name="thresholds">Confidence and probability thresholds; defaults when null.</param>
    /// <returns>The final decision, including the provenance of every field.</returns>
    public static TriageDecision Apply(DecisionResult assessment, TriageThresholds? thresholds = null)
    {
        ArgumentNullException.ThrowIfNull(assessment);
        thresholds ??= TriageThresholds.Default;

        var rules = new List<AppliedRule>();

        // Jev's raw proposals. Category, team, and priority are never overridden by the rules below:
        // the rules govern the review and redaction flags, which is where the risk actually sits.
        var category = DecidedField<TicketCategory>.FromModel(assessment.Category, assessment.CategoryConfidence);
        var targetTeam = DecidedField<TargetTeam>.FromModel(assessment.TargetTeam, assessment.TargetTeamConfidence);
        var priority = DecidedField<TicketPriority>.FromModel(assessment.Priority, assessment.PriorityConfidence);

        // A noul answer carries no confidence, only a probability, so these two start with a null
        // confidence and are thresholded into booleans here.
        var modelWantsReview = assessment.HumanReviewProbability >= thresholds.HumanReviewProbabilityThreshold;
        var modelSeesSensitiveData =
            assessment.SensitiveDataProbability >= thresholds.SensitiveDataProbabilityThreshold;

        var containsSensitiveData = DecidedField<bool>.FromModel(modelSeesSensitiveData, confidence: null);
        var needsHumanReview = DecidedField<bool>.FromModel(modelWantsReview, confidence: null);

        if (modelWantsReview)
        {
            rules.Add(new AppliedRule(
                ModelRequestedReviewRuleId,
                "The decision engine estimated a high probability that this ticket needs a human.",
                $"needsHumanReview kept as true (noul probability {assessment.HumanReviewProbability:F3} " +
                $">= {thresholds.HumanReviewProbabilityThreshold:F2})."));
        }

        // Rule 1: security concerns and critical tickets always get a human.
        if (category.Value == TicketCategory.SecurityConcern || priority.Value == TicketPriority.Critical)
        {
            var reason = category.Value == TicketCategory.SecurityConcern && priority.Value == TicketPriority.Critical
                ? "category is SecurityConcern and priority is Critical"
                : category.Value == TicketCategory.SecurityConcern
                    ? "category is SecurityConcern"
                    : "priority is Critical";

            needsHumanReview = needsHumanReview.OverriddenBy(true);
            rules.Add(new AppliedRule(
                SecurityEscalationRuleId,
                "Security concerns and critical tickets always require human review.",
                $"needsHumanReview forced to true because {reason}."));
        }

        // Rule 2: low model confidence on any routing field means a human decides instead.
        var lowConfidenceFields = CollectLowConfidenceFields(assessment, thresholds.MinimumConfidence);
        if (lowConfidenceFields.Count > 0)
        {
            needsHumanReview = needsHumanReview.OverriddenBy(true);
            rules.Add(new AppliedRule(
                LowConfidenceRuleId,
                $"Model confidence below {thresholds.MinimumConfidence:F2} on any routing field requires human review.",
                $"needsHumanReview forced to true because {string.Join(", ", lowConfidenceFields)}."));
        }

        // Rule 3: sensitive data drives redaction of the description in logs and in the API response.
        if (containsSensitiveData.Value)
        {
            rules.Add(new AppliedRule(
                SensitiveDataRedactionRuleId,
                "Tickets flagged as containing sensitive data have their description redacted.",
                "The ticket description is masked in structured logs and omitted from the developer " +
                "details returned to the client."));
        }

        return new TriageDecision(
            category,
            targetTeam,
            priority,
            containsSensitiveData,
            needsHumanReview,
            rules,
            BuildRoutingSummary(targetTeam.Value, priority.Value, needsHumanReview.Value));
    }

    private static List<string> CollectLowConfidenceFields(DecisionResult assessment, double minimum)
    {
        var fields = new List<string>(3);

        if (assessment.CategoryConfidence < minimum)
        {
            fields.Add($"category confidence {assessment.CategoryConfidence:F3} < {minimum:F2}");
        }

        if (assessment.TargetTeamConfidence < minimum)
        {
            fields.Add($"targetTeam confidence {assessment.TargetTeamConfidence:F3} < {minimum:F2}");
        }

        if (assessment.PriorityConfidence < minimum)
        {
            fields.Add($"priority confidence {assessment.PriorityConfidence:F3} < {minimum:F2}");
        }

        return fields;
    }

    private static string BuildRoutingSummary(TargetTeam team, TicketPriority priority, bool needsHumanReview) =>
        needsHumanReview
            ? $"Queued for {team} at {priority} priority, held for human review before assignment."
            : $"Auto-routed to {team} at {priority} priority.";
}
