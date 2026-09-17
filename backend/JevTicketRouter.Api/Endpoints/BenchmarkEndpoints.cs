using JevTicketRouter.Application.Benchmarking;
using Microsoft.AspNetCore.Http.HttpResults;

namespace JevTicketRouter.Api.Endpoints;

/// <summary>Maps the provider benchmark endpoint.</summary>
public static class BenchmarkEndpoints
{
    /// <summary>Registers <c>POST /api/benchmark</c>.</summary>
    /// <param name="app">The route builder.</param>
    public static IEndpointRouteBuilder MapBenchmarkEndpoints(this IEndpointRouteBuilder app)
    {
        ArgumentNullException.ThrowIfNull(app);

        app.MapPost("/api/benchmark", RunAsync)
            .WithName("RunBenchmark")
            .WithTags("Diagnostics")
            .WithSummary("Compare the configured AI providers over a fictional ticket corpus")
            .WithDescription(
                "Runs a fixed set of invented demo tickets against every configured provider and "
                    + "reports latency, schema-validity rate, and how often the providers reach the "
                    + "same final routing.\n\n"
                    + "The corpus is held in code and is entirely fictional: no submitted ticket is "
                    + "ever used. The report identifies cases by id only, and contains no ticket "
                    + "text, no model output, and no credential.")
            .Produces<BenchmarkReport>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status503ServiceUnavailable);

        return app;
    }

    private static async Task<Results<Ok<BenchmarkReport>, ProblemHttpResult>> RunAsync(
        IBenchmarkRunner runner,
        CancellationToken cancellationToken)
    {
        if (!runner.IsAvailable)
        {
            return TypedResults.Problem(
                title: "No provider is available to benchmark.",
                detail: "Configure a TypeSafe key, a local AI endpoint, or both, then try again.",
                statusCode: StatusCodes.Status503ServiceUnavailable);
        }

        var report = await runner.RunAsync(cancellationToken).ConfigureAwait(false);

        return TypedResults.Ok(report);
    }
}
