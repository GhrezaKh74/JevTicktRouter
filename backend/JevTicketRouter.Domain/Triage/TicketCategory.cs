namespace JevTicketRouter.Domain.Triage;

/// <summary>The nature of the request.</summary>
public enum TicketCategory
{
    /// <summary>Something is broken, erroring, or behaving incorrectly.</summary>
    TechnicalIssue = 0,

    /// <summary>A question about an existing service, process, or status.</summary>
    ServiceInquiry = 1,

    /// <summary>A request for access, permissions, accounts, or credentials.</summary>
    AccessRequest = 2,

    /// <summary>A suspected security incident, vulnerability, fraud, or data exposure.</summary>
    SecurityConcern = 3,

    /// <summary>A general question that does not fit the other categories.</summary>
    GeneralQuestion = 4,
}
