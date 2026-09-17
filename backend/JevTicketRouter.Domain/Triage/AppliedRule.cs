namespace JevTicketRouter.Domain.Triage;

/// <summary>A deterministic rule that fired, recorded so the UI and logs can explain the outcome.</summary>
/// <param name="Id">Stable identifier, e.g. <c>SECURITY_ESCALATION</c>.</param>
/// <param name="Description">Human-readable statement of the rule.</param>
/// <param name="Effect">What the rule changed about the decision.</param>
public sealed record AppliedRule(string Id, string Description, string Effect);
