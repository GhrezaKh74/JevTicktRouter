namespace JevTicketRouter.Domain.Triage;

/// <summary>
/// The Jev model's view of a ticket, already mapped from the raw TypeSafe answers into domain types.
/// This is a proposal only: <see cref="TriageRuleEngine"/> has the final word.
/// </summary>
/// <param name="Category">The category Jev chose.</param>
/// <param name="CategoryConfidence">Jev's confidence in <paramref name="Category"/> (0-1).</param>
/// <param name="TargetTeam">The team Jev chose.</param>
/// <param name="TargetTeamConfidence">Jev's confidence in <paramref name="TargetTeam"/> (0-1).</param>
/// <param name="Priority">The priority level Jev scored highest.</param>
/// <param name="PriorityConfidence">Jev's confidence in <paramref name="Priority"/> (0-1).</param>
/// <param name="PriorityScore">
/// The raw probability-weighted position across the priority levels. Can fall between two levels.
/// </param>
/// <param name="SensitiveDataProbability">Jev's <c>noul</c> probability that the ticket contains sensitive data.</param>
/// <param name="HumanReviewProbability">Jev's <c>noul</c> probability that the ticket needs a human.</param>
/// <param name="Model">The versioned model id that answered, as reported by the API.</param>
public sealed record JevAssessment(
    TicketCategory Category,
    double CategoryConfidence,
    TargetTeam TargetTeam,
    double TargetTeamConfidence,
    TicketPriority Priority,
    double PriorityConfidence,
    double PriorityScore,
    double SensitiveDataProbability,
    double HumanReviewProbability,
    string Model);
