namespace JevTicketRouter.Domain.Triage;

/// <summary>
/// The final, authoritative routing decision for a ticket, after deterministic rules have run.
/// </summary>
/// <param name="Category">Final category.</param>
/// <param name="TargetTeam">Final owning team.</param>
/// <param name="Priority">Final priority.</param>
/// <param name="ContainsSensitiveData">Whether the ticket must be treated as containing sensitive data.</param>
/// <param name="NeedsHumanReview">Whether a human must review before the ticket is auto-routed.</param>
/// <param name="AppliedRules">Every deterministic rule that fired, in evaluation order.</param>
/// <param name="RoutingSummary">A one-line, human-readable statement of the outcome.</param>
public sealed record TriageDecision(
    DecidedField<TicketCategory> Category,
    DecidedField<TargetTeam> TargetTeam,
    DecidedField<TicketPriority> Priority,
    DecidedField<bool> ContainsSensitiveData,
    DecidedField<bool> NeedsHumanReview,
    IReadOnlyList<AppliedRule> AppliedRules,
    string RoutingSummary);
