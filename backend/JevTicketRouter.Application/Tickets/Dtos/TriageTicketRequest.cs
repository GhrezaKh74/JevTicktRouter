using JevTicketRouter.Domain.Tickets;

namespace JevTicketRouter.Application.Tickets.Dtos;

/// <summary>The request body of <c>POST /api/tickets/triage</c>.</summary>
/// <param name="Title">Short summary of the problem. 3-200 characters.</param>
/// <param name="Description">Full description of the problem. 10-5000 characters. Persian or English.</param>
/// <param name="RequesterRole">Who raised the ticket.</param>
public sealed record TriageTicketRequest(
    string Title,
    string Description,
    RequesterRole RequesterRole);
