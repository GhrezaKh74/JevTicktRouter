using JevTicketRouter.Application.Tickets;
using JevTicketRouter.Infrastructure.Decisions;
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

        app.MapGet(
                "/api/health",
                (DecisionEngineSelection selection, IOptions<TriageOptions> triageOptions) =>
                    TypedResults.Ok(new HealthResponse(
                        "Healthy",
                        selection.Provider.ToString(),
                        selection.Provider != Domain.Decisions.AiProvider.Mock,
                        selection.Model,
                        triageOptions.Value.MinimumConfidence)))
            .WithName("GetHealth")
            .WithTags("Diagnostics")
            .WithSummary("Report service status and which AI provider is active")
            .WithDescription(
                "Used by the dashboard to show the provider badge. Reports the provider name only — "
                    + "never a credential, an endpoint, or a configuration value that could leak one.")
            .Produces<HealthResponse>(StatusCodes.Status200OK);

        return app;
    }
}

/// <summary>The payload of <c>GET /api/health</c>.</summary>
/// <param name="Status">Always <c>Healthy</c> when the service is responding.</param>
/// <param name="Provider">The active provider: <c>Jev</c>, <c>Local</c>, or <c>Mock</c>.</param>
/// <param name="IsLive">False when answers come from deterministic sample data.</param>
/// <param name="Model">The model that will answer, for whichever provider is active.</param>
/// <param name="MinimumConfidence">The confidence threshold below which tickets are escalated.</param>
public sealed record HealthResponse(
    string Status,
    string Provider,
    bool IsLive,
    string Model,
    double MinimumConfidence);
