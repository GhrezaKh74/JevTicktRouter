using System.Net;
using JevTicketRouter.Domain.Decisions;

namespace JevTicketRouter.Application.Decisions;

/// <summary>Why a decision engine call failed.</summary>
public enum DecisionFailureKind
{
    /// <summary>The provider could not be reached, or the call timed out.</summary>
    Transport = 0,

    /// <summary>The provider answered with an error status.</summary>
    Provider = 1,

    /// <summary>
    /// The provider answered, but the body could not be validated into a decision. Never retried and
    /// never guessed at: an unusable answer is reported, not invented.
    /// </summary>
    MalformedResponse = 2,

    /// <summary>The engine is misconfigured and refused to run at all.</summary>
    Configuration = 3,
}

/// <summary>
/// Raised when a decision engine fails. Carries the provider and the failure kind, so the API layer
/// can map it onto a sensible ProblemDetails response.
/// <para>
/// Messages are written to be safe to surface: they never contain an API key, and never contain
/// ticket text.
/// </para>
/// </summary>
public sealed class DecisionEngineException : Exception
{
    /// <summary>Creates a new instance.</summary>
    /// <param name="message">A message safe to return to a caller.</param>
    /// <param name="provider">The provider that failed.</param>
    /// <param name="kind">What sort of failure this was.</param>
    /// <param name="statusCode">The HTTP status returned, if the call got that far.</param>
    /// <param name="innerException">The underlying failure, if any.</param>
    public DecisionEngineException(
        string message,
        AiProvider provider,
        DecisionFailureKind kind,
        HttpStatusCode? statusCode = null,
        Exception? innerException = null)
        : base(message, innerException)
    {
        Provider = provider;
        Kind = kind;
        StatusCode = statusCode;
    }

    /// <summary>The provider that failed.</summary>
    public AiProvider Provider { get; }

    /// <summary>What sort of failure this was.</summary>
    public DecisionFailureKind Kind { get; }

    /// <summary>The HTTP status returned by the provider, or null for a transport or parse failure.</summary>
    public HttpStatusCode? StatusCode { get; }

    /// <summary>
    /// True when the failure is worth retrying later: transport faults, rate limiting, overload.
    /// A malformed response is deliberately not transient — retrying a model that cannot produce
    /// valid output only burns time, and a misconfiguration will not fix itself either.
    /// </summary>
    public bool IsTransient => Kind switch
    {
        DecisionFailureKind.Transport => true,
        DecisionFailureKind.Provider => (int?)StatusCode is 408 or 429 or 529 or (>= 500 and < 600),
        _ => false,
    };
}
