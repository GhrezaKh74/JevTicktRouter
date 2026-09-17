namespace JevTicketRouter.Domain.Tickets;

/// <summary>Who raised the ticket. Influences routing and priority.</summary>
public enum RequesterRole
{
    /// <summary>Staff working in a branch office.</summary>
    BranchEmployee = 0,

    /// <summary>An external customer.</summary>
    Customer = 1,

    /// <summary>A member of the internal support organisation.</summary>
    InternalSupport = 2,
}
