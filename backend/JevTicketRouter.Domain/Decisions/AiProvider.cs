namespace JevTicketRouter.Domain.Decisions;

/// <summary>
/// Which decision engine produced an assessment.
/// <para>
/// The provider is recorded on every result so an operator can always tell which system made a call,
/// and so a benchmark can attribute a routing disagreement to the engine that caused it.
/// </para>
/// </summary>
public enum AiProvider
{
    /// <summary>TypeSafe Jev, called over the public System One API.</summary>
    Jev = 0,

    /// <summary>A self-hosted OpenAI-compatible endpoint inside the organisation's own network.</summary>
    Local = 1,

    /// <summary>Deterministic sample answers. No model, no network.</summary>
    Mock = 2,
}
