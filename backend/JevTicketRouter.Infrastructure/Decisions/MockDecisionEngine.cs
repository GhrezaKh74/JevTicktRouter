using JevTicketRouter.Application.Decisions;
using JevTicketRouter.Application.Jev;
using JevTicketRouter.Application.Jev.Abstractions;
using JevTicketRouter.Application.Tickets;
using JevTicketRouter.Domain.Decisions;
using JevTicketRouter.Domain.Tickets;
using JevTicketRouter.Domain.Triage;
using JevTicketRouter.Infrastructure.Jev;
using Microsoft.Extensions.Options;

namespace JevTicketRouter.Infrastructure.Decisions;

/// <summary>
/// The development engine: deterministic sample answers, no model and no network.
/// <para>
/// It reuses <see cref="MockJevClient"/> rather than scoring tickets a second time, so there is one
/// implementation of the demo heuristics and mock answers keep the exact shape the real API
/// documents. Used when neither a Jev key nor a local endpoint is configured.
/// </para>
/// </summary>
public sealed class MockDecisionEngine : IDecisionEngine
{
    private readonly MockJevClient _client;
    private readonly TriageOptions _options;

    /// <summary>Creates the engine.</summary>
    /// <param name="client">The deterministic Jev-shaped mock.</param>
    /// <param name="options">Model name used when building the question set.</param>
    public MockDecisionEngine(MockJevClient client, IOptions<TriageOptions> options)
    {
        ArgumentNullException.ThrowIfNull(options);

        _client = client ?? throw new ArgumentNullException(nameof(client));
        _options = options.Value;
    }

    /// <inheritdoc />
    public AiProvider Provider => AiProvider.Mock;

    /// <inheritdoc />
    public bool IsLive => false;

    /// <inheritdoc />
    public async Task<DecisionResult> EvaluateAsync(TicketInput input, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(input);

        var ticket = new SupportTicket(input.Title, input.Description, input.RequesterRole);
        var request = JevTriageQuestions.BuildRequest(ticket, _options.Model);

        var response = await _client.EvaluateAsync(request, cancellationToken).ConfigureAwait(false);

        try
        {
            return JevAnswerMapper.Map(response, AiProvider.Mock);
        }
        catch (JevClientException exception)
        {
            throw new DecisionEngineException(
                exception.Message,
                AiProvider.Mock,
                DecisionFailureKind.MalformedResponse,
                innerException: exception);
        }
    }
}
