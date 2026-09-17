namespace JevTicketRouter.Domain.Triage;

/// <summary>Where a final field value came from.</summary>
public enum DecisionOrigin
{
    /// <summary>The value is exactly what the Jev model returned.</summary>
    JevModel = 0,

    /// <summary>A deterministic .NET business rule overrode or supplied the value.</summary>
    BusinessRule = 1,
}
