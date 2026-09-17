using JevTicketRouter.Application.Tickets.Dtos;

namespace JevTicketRouter.Application.Tickets;

/// <summary>Orchestrates the triage of a single ticket.</summary>
public interface ITicketTriageService
{
    /// <summary>
    /// Evaluates a ticket with Jev, applies the deterministic rules, and returns the final decision.
    /// </summary>
    /// <param name="request">The submitted ticket. Assumed already validated.</param>
    /// <param name="cancellationToken">Cancels the Jev call.</param>
    Task<TriageTicketResponse> TriageAsync(TriageTicketRequest request, CancellationToken cancellationToken);
}
