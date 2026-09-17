using System.Net;

namespace JevTicketRouter.Application.Jev.Abstractions;

/// <summary>
/// Raised when a call to the TypeSafe API fails. Carries the HTTP status where one was received so
/// the API layer can map it onto a sensible ProblemDetails response.
/// </summary>
public sealed class JevClientException : Exception
{
    /// <summary>Creates a new instance.</summary>
    /// <param name="message">A message safe to surface to callers. Never contains the API key.</param>
    /// <param name="statusCode">The HTTP status returned by TypeSafe, if the call got that far.</param>
    /// <param name="innerException">The underlying transport failure, if any.</param>
    public JevClientException(string message, HttpStatusCode? statusCode = null, Exception? innerException = null)
        : base(message, innerException)
    {
        StatusCode = statusCode;
    }

    /// <summary>The HTTP status returned by TypeSafe, or null for a transport or timeout failure.</summary>
    public HttpStatusCode? StatusCode { get; }

    /// <summary>
    /// True when the failure is transient from the caller's point of view and the request is worth
    /// retrying later: rate limiting, overload, timeouts, and transport faults.
    /// </summary>
    public bool IsTransient => StatusCode is null
        or HttpStatusCode.TooManyRequests
        or HttpStatusCode.RequestTimeout
        or HttpStatusCode.ServiceUnavailable
        or HttpStatusCode.InternalServerError
        or HttpStatusCode.BadGateway
        or HttpStatusCode.GatewayTimeout
        or (HttpStatusCode)529;
}
