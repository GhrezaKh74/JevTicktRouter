namespace JevTicketRouter.Domain.Triage;

/// <summary>
/// How urgently the ticket must be handled. The numeric values are ordered and are used as the
/// level indices of the Jev <c>score</c> question that estimates priority.
/// </summary>
public enum TicketPriority
{
    /// <summary>No material impact; can wait for normal scheduling.</summary>
    Low = 0,

    /// <summary>A single user is impeded but can still work.</summary>
    Medium = 1,

    /// <summary>Work is blocked for a user or group, or money/compliance is at stake.</summary>
    High = 2,

    /// <summary>Widespread outage, active security incident, or severe financial exposure.</summary>
    Critical = 3,
}
