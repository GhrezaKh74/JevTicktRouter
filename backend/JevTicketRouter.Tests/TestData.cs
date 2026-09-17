using JevTicketRouter.Domain.Decisions;
using JevTicketRouter.Domain.Tickets;
using JevTicketRouter.Domain.Triage;

namespace JevTicketRouter.Tests;

/// <summary>Shared builders so each test states only the values it actually cares about.</summary>
internal static class TestData
{
    /// <summary>A confident, unremarkable assessment that triggers no rule.</summary>
    public static DecisionResult CleanAssessment(
        TicketCategory category = TicketCategory.TechnicalIssue,
        TargetTeam team = TargetTeam.ApplicationSupport,
        TicketPriority priority = TicketPriority.Medium,
        double categoryConfidence = 0.95,
        double teamConfidence = 0.93,
        double priorityConfidence = 0.91,
        double sensitiveProbability = 0.02,
        double humanReviewProbability = 0.05,
        AiProvider provider = AiProvider.Jev) =>
        new(
            category,
            categoryConfidence,
            team,
            teamConfidence,
            priority,
            priorityConfidence,
            (int)priority,
            sensitiveProbability,
            humanReviewProbability,
            "jev-1.13.0",
            provider);

    public static SupportTicket Ticket(
        string title = "Reporting portal returns an error on save",
        string description = "Saving a record in the reporting portal fails with an unexpected error.",
        RequesterRole role = RequesterRole.BranchEmployee) =>
        new(title, description, role);
}
