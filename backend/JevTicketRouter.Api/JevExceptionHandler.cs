using System.Text.Json;
using JevTicketRouter.Application.Decisions;
using JevTicketRouter.Application.Jev.Abstractions;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;

namespace JevTicketRouter.Api;

/// <summary>
/// Turns failures from the decision engine into RFC 7807 ProblemDetails responses. Messages are
/// written for an operator reading the dashboard and never include the API key or the ticket text.
/// </summary>
public sealed class JevExceptionHandler : IExceptionHandler
{
    private readonly IProblemDetailsService _problemDetailsService;
    private readonly ILogger<JevExceptionHandler> _logger;

    /// <summary>Creates the handler.</summary>
    /// <param name="problemDetailsService">Writes the ProblemDetails payload.</param>
    /// <param name="logger">Structured logger.</param>
    public JevExceptionHandler(
        IProblemDetailsService problemDetailsService,
        ILogger<JevExceptionHandler> logger)
    {
        _problemDetailsService = problemDetailsService;
        _logger = logger;
    }

    /// <inheritdoc />
    public async ValueTask<bool> TryHandleAsync(
        HttpContext httpContext,
        Exception exception,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(httpContext);

        var (status, title, detail) = Describe(exception);

        if (status is null)
        {
            return false;
        }

        _logger.LogError(
            exception,
            "Request {Method} {Path} failed with {StatusCode}.",
            httpContext.Request.Method,
            httpContext.Request.Path,
            status);

        httpContext.Response.StatusCode = status.Value;

        return await _problemDetailsService.TryWriteAsync(new ProblemDetailsContext
        {
            HttpContext = httpContext,
            Exception = exception,
            ProblemDetails = new ProblemDetails
            {
                Status = status,
                Title = title,
                Detail = detail,
                Type = $"https://httpstatuses.io/{status}",
            },
        }).ConfigureAwait(false);
    }

    private static (int? Status, string Title, string Detail) Describe(Exception exception) => exception switch
    {
        // Minimal APIs surface an unreadable or unbindable JSON body as BadHttpRequestException.
        // Without this it would escape as an unhandled 500 rather than telling the caller what to fix.
        BadHttpRequestException => (
            StatusCodes.Status400BadRequest,
            "The request body could not be read.",
            "The request body is not valid JSON, or a field has a value the API does not recognise. "
                + "Check that requesterRole is one of BranchEmployee, Customer, or InternalSupport."),

        JsonException => (
            StatusCodes.Status400BadRequest,
            "The request body could not be read.",
            "The request body is not valid JSON."),

        // A malformed model reply is a gateway problem, not a server fault: the request was fine,
        // the upstream answer was not. It is reported plainly rather than retried or guessed at.
        DecisionEngineException { Kind: DecisionFailureKind.MalformedResponse } engine => (
            StatusCodes.Status502BadGateway,
            "The AI provider returned an unusable response.",
            engine.Message),

        DecisionEngineException { Kind: DecisionFailureKind.Configuration } engine => (
            StatusCodes.Status503ServiceUnavailable,
            "The AI provider is not configured correctly.",
            engine.Message),

        DecisionEngineException { IsTransient: true } engine => (
            StatusCodes.Status503ServiceUnavailable,
            "The triage service is temporarily unavailable.",
            engine.Message),

        DecisionEngineException engine => (
            StatusCodes.Status502BadGateway,
            "The triage service could not complete the request.",
            engine.Message),

        JevClientException { IsTransient: true } jev => (
            StatusCodes.Status503ServiceUnavailable,
            "The triage service is temporarily unavailable.",
            jev.Message),

        JevClientException jev => (
            StatusCodes.Status502BadGateway,
            "The triage service could not complete the request.",
            jev.Message),

        OperationCanceledException => (
            StatusCodesExtensions.Status499ClientClosedRequest,
            "The request was cancelled.",
            "The client cancelled the request before triage completed."),

        _ => (null, string.Empty, string.Empty),
    };
}

/// <summary>Status codes used here that <c>StatusCodes</c> does not define.</summary>
internal static class StatusCodesExtensions
{
    /// <summary>nginx's non-standard code for a client that hung up mid-request.</summary>
    public const int Status499ClientClosedRequest = 499;
}
