using JevTicketRouter.Domain.Tickets;

namespace JevTicketRouter.Application.Decisions;

/// <summary>
/// The ticket as a decision engine sees it.
/// <para>
/// A boundary type rather than the domain's <see cref="SupportTicket"/>: an engine is an external
/// integration, and giving it its own narrow contract keeps the domain entity free to grow fields
/// that no provider should ever be sent.
/// </para>
/// </summary>
/// <param name="Title">Short summary of the problem.</param>
/// <param name="Description">Full free-text description. Persian, English, or a mix.</param>
/// <param name="RequesterRole">Who raised the ticket.</param>
public sealed record TicketInput(string Title, string Description, RequesterRole RequesterRole)
{
    /// <summary>Projects a domain ticket onto the engine contract.</summary>
    public static TicketInput From(SupportTicket ticket)
    {
        ArgumentNullException.ThrowIfNull(ticket);

        return new TicketInput(ticket.Title, ticket.Description, ticket.RequesterRole);
    }
}
