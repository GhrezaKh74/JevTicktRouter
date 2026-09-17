using JevTicketRouter.Domain.Decisions;

namespace JevTicketRouter.Infrastructure.Decisions;

/// <summary>
/// The outcome of choosing a provider at startup: which engine runs, and why.
/// <para>
/// Recorded as a value rather than decided inline so the choice is unit-testable without a DI
/// container, and so the reason can be logged once and surfaced on the health endpoint.
/// </para>
/// </summary>
/// <param name="Provider">The engine that will actually run.</param>
/// <param name="Requested">The provider that was asked for.</param>
/// <param name="Model">
/// The model that will answer. Provider-specific, so the health endpoint reports the local model
/// name in local mode rather than the Jev one it is not going to use.
/// </param>
/// <param name="Reason">Why this engine was chosen, in words safe to log.</param>
public sealed record DecisionEngineSelection(
    AiProvider Provider,
    AiProvider Requested,
    string Model,
    string Reason)
{
    /// <summary>True when the running engine is not the one that was requested.</summary>
    public bool FellBack => Provider != Requested;
}
