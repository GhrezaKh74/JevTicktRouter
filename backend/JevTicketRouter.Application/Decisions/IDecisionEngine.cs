using JevTicketRouter.Domain.Decisions;
using JevTicketRouter.Domain.Triage;

namespace JevTicketRouter.Application.Decisions;

/// <summary>
/// The application's single point of contact with any AI decision provider.
/// <para>
/// Everything above this interface — the triage service, the deterministic rules, the API contract,
/// the React client — is provider-agnostic. Swapping TypeSafe Jev for a model running inside the
/// organisation's own network is a configuration change, not a code change, which is the whole point
/// of the abstraction: the cloud provider is for evaluation, local inference is the intended
/// production architecture for restricted data.
/// </para>
/// </summary>
public interface IDecisionEngine
{
    /// <summary>Which provider this engine represents.</summary>
    AiProvider Provider { get; }

    /// <summary>
    /// True when this engine reaches a real model; false when it returns deterministic sample data.
    /// </summary>
    bool IsLive { get; }

    /// <summary>Evaluates one ticket and returns a structured proposal.</summary>
    /// <param name="input">The ticket to evaluate.</param>
    /// <param name="cancellationToken">Cancels the in-flight call.</param>
    /// <returns>The engine's proposal. Never authoritative on its own.</returns>
    /// <exception cref="DecisionEngineException">
    /// The provider was unreachable, rejected the call, or replied with something that could not be
    /// safely parsed. A malformed reply is always an error — it is never guessed at or filled in.
    /// </exception>
    Task<DecisionResult> EvaluateAsync(TicketInput input, CancellationToken cancellationToken);
}
