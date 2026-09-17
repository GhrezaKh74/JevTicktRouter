using JevTicketRouter.Application.Jev.Contracts;

namespace JevTicketRouter.Application.Jev.Abstractions;

/// <summary>
/// Transport-level abstraction over the TypeSafe System One endpoint. Deliberately a thin, faithful
/// wrapper around the documented HTTP contract so it can be substituted in tests, and so the choice
/// between the live API and mock mode is invisible to the rest of the application.
/// </summary>
public interface IJevClient
{
    /// <summary>
    /// True when this client talks to the real TypeSafe API; false when it returns deterministic
    /// sample data because no API key is configured.
    /// </summary>
    bool IsLive { get; }

    /// <summary>Evaluates one state against a batch of questions in a single call.</summary>
    /// <param name="request">The System One request body.</param>
    /// <param name="cancellationToken">Cancels the in-flight HTTP call.</param>
    /// <returns>The structured answers, keyed by the request's question ids.</returns>
    /// <exception cref="JevClientException">The API was unreachable, rejected the call, or replied unusably.</exception>
    Task<JevSystemOneResponse> EvaluateAsync(
        JevSystemOneRequest request,
        CancellationToken cancellationToken);
}
