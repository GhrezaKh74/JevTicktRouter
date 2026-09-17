using System.ComponentModel.DataAnnotations;
using JevTicketRouter.Domain.Triage;

namespace JevTicketRouter.Application.Tickets;

/// <summary>
/// Configuration for the triage pipeline, bound from the <c>Triage</c> configuration section.
/// </summary>
public sealed class TriageOptions
{
    /// <summary>The configuration section name.</summary>
    public const string SectionName = "Triage";

    /// <summary>
    /// The model to send in the request's <c>model</c> field. <c>jev-latest</c> tracks the most recent
    /// stable release; pin a versioned id such as <c>jev-1.13.0</c> if confidence thresholds have been
    /// tuned against it.
    /// </summary>
    [Required]
    public string Model { get; set; } = "jev-latest";

    /// <summary>
    /// Minimum Jev confidence on category, target team, and priority. Anything lower escalates the
    /// ticket to a human.
    /// </summary>
    [Range(0d, 1d)]
    public double MinimumConfidence { get; set; } = 0.75;

    /// <summary>Noul probability at or above which the ticket counts as containing sensitive data.</summary>
    [Range(0d, 1d)]
    public double SensitiveDataProbabilityThreshold { get; set; } = 0.5;

    /// <summary>Noul probability at or above which Jev is treated as having asked for a human.</summary>
    [Range(0d, 1d)]
    public double HumanReviewProbabilityThreshold { get; set; } = 0.5;

    /// <summary>Projects these options onto the domain's threshold record.</summary>
    public TriageThresholds ToThresholds() => new(
        MinimumConfidence,
        SensitiveDataProbabilityThreshold,
        HumanReviewProbabilityThreshold);
}
