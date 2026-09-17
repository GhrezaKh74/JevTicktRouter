using JevTicketRouter.Domain.Triage;

namespace JevTicketRouter.Application.Tickets.Dtos;

/// <summary>The result of triaging one ticket.</summary>
/// <param name="TicketId">A correlation id for this triage run.</param>
/// <param name="Category">Final category, with provenance.</param>
/// <param name="TargetTeam">Final owning team, with provenance.</param>
/// <param name="Priority">Final priority, with provenance.</param>
/// <param name="ContainsSensitiveData">Whether the ticket was flagged as containing sensitive data.</param>
/// <param name="NeedsHumanReview">Whether a human must review before routing.</param>
/// <param name="RoutingSummary">A one-line statement of the final routing outcome.</param>
/// <param name="AppliedRules">Every deterministic rule that fired, in order.</param>
/// <param name="Jev">Diagnostics about the Jev call, already sanitised.</param>
public sealed record TriageTicketResponse(
    string TicketId,
    DecidedFieldDto<string> Category,
    DecidedFieldDto<string> TargetTeam,
    DecidedFieldDto<string> Priority,
    DecidedFieldDto<bool> ContainsSensitiveData,
    DecidedFieldDto<bool> NeedsHumanReview,
    string RoutingSummary,
    IReadOnlyList<AppliedRuleDto> AppliedRules,
    JevDiagnosticsDto Jev);

/// <summary>A final field value together with what Jev proposed and who decided it.</summary>
/// <param name="Value">The final, authoritative value.</param>
/// <param name="ModelValue">What the Jev model proposed.</param>
/// <param name="Confidence">Jev's confidence from 0 to 1, or null for noul-backed fields.</param>
/// <param name="Origin">Either <c>JevModel</c> or <c>BusinessRule</c>.</param>
/// <param name="WasOverridden">True when a deterministic rule changed Jev's proposal.</param>
public sealed record DecidedFieldDto<T>(
    T Value,
    T ModelValue,
    double? Confidence,
    string Origin,
    bool WasOverridden);

/// <summary>A deterministic rule that fired.</summary>
/// <param name="Id">Stable rule identifier.</param>
/// <param name="Description">What the rule states.</param>
/// <param name="Effect">What the rule changed.</param>
public sealed record AppliedRuleDto(string Id, string Description, string Effect);

/// <summary>
/// Sanitised diagnostics about the decision-engine call, surfaced in the UI's developer-details
/// panel. Contains no ticket text and never any credential.
/// </summary>
/// <param name="Provider">Which engine decided: <c>Jev</c>, <c>Local</c>, or <c>Mock</c>.</param>
/// <param name="IsLive">False when the answer came from deterministic sample data.</param>
/// <param name="Model">The versioned model id that answered.</param>
/// <param name="LatencyMs">How long the evaluation took, in milliseconds.</param>
/// <param name="PriorityScore">The raw probability-weighted priority score across the levels.</param>
/// <param name="SensitiveDataProbability">The raw noul probability for the sensitive-data question.</param>
/// <param name="HumanReviewProbability">The raw noul probability for the human-review question.</param>
/// <param name="StateSummary">
/// What was sent to Jev as state, redacted when the ticket was flagged as sensitive.
/// </param>
public sealed record JevDiagnosticsDto(
    string Provider,
    bool IsLive,
    string Model,
    long LatencyMs,
    double PriorityScore,
    double SensitiveDataProbability,
    double HumanReviewProbability,
    JevStateSummaryDto StateSummary);

/// <summary>
/// The state that was evaluated, as echoed back to the client. The description is replaced with a
/// redaction placeholder whenever sensitive data was detected.
/// </summary>
/// <param name="Title">The ticket title, redacted when sensitive.</param>
/// <param name="Description">The ticket description, redacted when sensitive.</param>
/// <param name="RequesterRole">The requester role that was sent.</param>
/// <param name="Redacted">True when the text above has been replaced by a placeholder.</param>
public sealed record JevStateSummaryDto(
    string Title,
    string Description,
    string RequesterRole,
    bool Redacted);
