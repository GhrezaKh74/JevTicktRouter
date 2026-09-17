using JevTicketRouter.Domain.Decisions;

namespace JevTicketRouter.Domain.Triage;

/// <summary>
/// A decision engine's view of a ticket, already mapped from whatever the provider returned into
/// domain types. This is a proposal only: <see cref="TriageRuleEngine"/> has the final word.
/// <para>
/// The shape is deliberately provider-neutral. TypeSafe Jev reports a calibrated confidence per
/// answer; a local instruction-tuned model is asked to self-report one. Both land here identically,
/// so the rule engine, the redaction path, and the API contract never learn which engine ran.
/// </para>
/// </summary>
/// <param name="Category">The category the engine chose.</param>
/// <param name="CategoryConfidence">Confidence in <paramref name="Category"/> (0-1).</param>
/// <param name="TargetTeam">The team the engine chose.</param>
/// <param name="TargetTeamConfidence">Confidence in <paramref name="TargetTeam"/> (0-1).</param>
/// <param name="Priority">The priority level the engine chose.</param>
/// <param name="PriorityConfidence">Confidence in <paramref name="Priority"/> (0-1).</param>
/// <param name="PriorityScore">
/// The raw probability-weighted position across the priority levels. Can fall between two levels.
/// </param>
/// <param name="SensitiveDataProbability">Probability that the ticket contains sensitive data.</param>
/// <param name="HumanReviewProbability">Probability that the ticket needs a human.</param>
/// <param name="Model">The model id that answered, as reported by the provider.</param>
/// <param name="Provider">Which engine produced this result.</param>
public sealed record DecisionResult(
    TicketCategory Category,
    double CategoryConfidence,
    TargetTeam TargetTeam,
    double TargetTeamConfidence,
    TicketPriority Priority,
    double PriorityConfidence,
    double PriorityScore,
    double SensitiveDataProbability,
    double HumanReviewProbability,
    string Model,
    AiProvider Provider);
