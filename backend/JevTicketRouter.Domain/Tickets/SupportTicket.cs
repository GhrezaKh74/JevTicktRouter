namespace JevTicketRouter.Domain.Tickets;

/// <summary>
/// A support ticket as submitted by a requester. The text may be Persian, English, or a mix of both.
/// </summary>
/// <param name="Title">Short summary of the problem.</param>
/// <param name="Description">Full free-text description.</param>
/// <param name="RequesterRole">Who raised the ticket.</param>
public sealed record SupportTicket(string Title, string Description, RequesterRole RequesterRole);
