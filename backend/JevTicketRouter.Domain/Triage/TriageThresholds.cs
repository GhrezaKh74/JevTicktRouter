namespace JevTicketRouter.Domain.Triage;

/// <summary>
/// Tunable thresholds for the deterministic rule engine. Kept in the domain so the rules stay
/// testable without configuration plumbing.
/// </summary>
/// <param name="MinimumConfidence">
/// Below this confidence on category, target team, or priority, the ticket is escalated to a human.
/// </param>
/// <param name="SensitiveDataProbabilityThreshold">
/// A <c>noul</c> probability at or above this value is treated as "contains sensitive data".
/// </param>
/// <param name="HumanReviewProbabilityThreshold">
/// A <c>noul</c> probability at or above this value is treated as "Jev asked for a human".
/// </param>
public sealed record TriageThresholds(
    double MinimumConfidence = 0.75,
    double SensitiveDataProbabilityThreshold = 0.5,
    double HumanReviewProbabilityThreshold = 0.5)
{
    /// <summary>The defaults required by the product specification.</summary>
    public static TriageThresholds Default { get; } = new();
}
