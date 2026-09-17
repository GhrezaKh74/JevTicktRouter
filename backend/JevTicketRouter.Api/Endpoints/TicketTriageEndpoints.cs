using FluentValidation;
using JevTicketRouter.Application.Jev.Abstractions;
using JevTicketRouter.Application.Tickets;
using JevTicketRouter.Application.Tickets.Dtos;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;

namespace JevTicketRouter.Api.Endpoints;

/// <summary>Maps the ticket triage endpoints.</summary>
public static class TicketTriageEndpoints
{
    /// <summary>Registers <c>POST /api/tickets/triage</c>.</summary>
    /// <param name="app">The route builder.</param>
    public static IEndpointRouteBuilder MapTicketTriageEndpoints(this IEndpointRouteBuilder app)
    {
        ArgumentNullException.ThrowIfNull(app);

        var tickets = app.MapGroup("/api/tickets").WithTags("Tickets");

        tickets.MapPost("/triage", TriageAsync)
            .WithName("TriageTicket")
            .WithSummary("Triage a support ticket")
            .WithDescription(
                "Evaluates the ticket with a single batched TypeSafe Jev call, then applies deterministic "
                + ".NET business rules that have the final say. The response reports, for every field, "
                + "what Jev proposed and whether a rule overrode it.")
            .Accepts<TriageTicketRequest>("application/json")
            .Produces<TriageTicketResponse>(StatusCodes.Status200OK)
            .ProducesValidationProblem()
            .ProducesProblem(StatusCodes.Status502BadGateway)
            .ProducesProblem(StatusCodes.Status503ServiceUnavailable);

        return app;
    }

    private static async Task<Results<Ok<TriageTicketResponse>, ValidationProblem>> TriageAsync(
        [FromBody] TriageTicketRequest request,
        IValidator<TriageTicketRequest> validator,
        ITicketTriageService triageService,
        CancellationToken cancellationToken)
    {
        var validation = await validator.ValidateAsync(request, cancellationToken).ConfigureAwait(false);

        if (!validation.IsValid)
        {
            return TypedResults.ValidationProblem(
                validation.ToDictionary(),
                title: "The ticket could not be accepted.",
                detail: "One or more fields failed validation. See the errors for details.");
        }

        var response = await triageService.TriageAsync(request, cancellationToken).ConfigureAwait(false);

        return TypedResults.Ok(response);
    }
}
