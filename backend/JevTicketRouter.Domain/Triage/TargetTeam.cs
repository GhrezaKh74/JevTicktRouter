namespace JevTicketRouter.Domain.Triage;

/// <summary>The internal team that should own the ticket.</summary>
public enum TargetTeam
{
    /// <summary>Owns business applications and their day-to-day faults.</summary>
    ApplicationSupport = 0,

    /// <summary>Owns servers, network, databases, and platform availability.</summary>
    Infrastructure = 1,

    /// <summary>Owns accounts, roles, permissions, and authentication.</summary>
    IdentityAccess = 2,

    /// <summary>Owns security incidents, fraud, and data protection.</summary>
    Security = 3,

    /// <summary>Owns non-technical business processes and operational questions.</summary>
    BusinessOperations = 4,
}
