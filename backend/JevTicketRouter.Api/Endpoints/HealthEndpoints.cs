using JevTicketRouter.Application.Jev.Abstractions;
using JevTicketRouter.Application.Tickets;
using Microsoft.Extensions.Options;

namespace JevTicketRouter.Api.Endpoints;

/// <summary>Maps lightweight status endpoints used by the frontend and by uptime checks.</summary>
public static class HealthEndpoints
{
    /// <summary>Registers <c>GET /api/health</c>.</summary>
    /// <param name="app">The route builder.</param>
    public static IEndpointRouteBuilder MapHealthEndpoints(this IEndpointRouteBuilder app)
    {
        ArgumentNullException.ThrowIfNull(app);

        app.MapGet("/api/health", (IJevClient jevClient, IOptions<TriageOptions> triageOptions) =>
                TypedResults.Ok(new HealthResponse(
                    "Healthy",
                    jevClient.IsLive ? "Live" : "Mock",
                    triageOptions.Value.Model,
                    triageOptions.Value.MinimumConfidence)))
            .WithName("GetHealth")
            .WithTags("Diagnostics")
            .WithSummary("Report service status and whether Jev is live or mocked")
            .WithDescription(
                "Used by the dashboard to show the Live Jev / Mock mode badge. Never reveals credentials.")
            .Produces<HealthResponse>(StatusCodes.Status200OK);

        return app;
    }
}

/// <summary>The payload of <c>GET /api/health</c>.</summary>
/// <param name="Status">Always <c>Healthy</c> when the service is responding.</param>
/// <param name="JevMode">Either <c>Live</c> or <c>Mock</c>.</param>
/// <param name="Model">The configured model name.</param>
/// <param name="MinimumConfidence">The confidence threshold below which tickets are escalated.</param>
public sealed record HealthResponse(string Status, string JevMode, string Model, double MinimumConfidence);
