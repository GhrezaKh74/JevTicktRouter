using JevTicketRouter.Application.Decisions;
using JevTicketRouter.Application.Jev;
using JevTicketRouter.Application.Jev.Abstractions;
using JevTicketRouter.Application.Jev.Contracts;
using JevTicketRouter.Application.Tickets;
using JevTicketRouter.Domain.Decisions;
using JevTicketRouter.Domain.Tickets;
using JevTicketRouter.Domain.Triage;
using Microsoft.Extensions.Options;

namespace JevTicketRouter.Infrastructure.Decisions;

/// <summary>
/// The TypeSafe Jev engine: one batched System One call using the Choice, Score, and Noul
/// primitives, mapped onto a provider-neutral <see cref="DecisionResult"/>.
/// <para>
/// This is a thin adapter over the existing <see cref="IJevClient"/> transport rather than a
/// reimplementation, so the documented request shape, the retry policy, and the strict answer
/// mapping all stay exactly where they were.
/// </para>
/// </summary>
public sealed class TypeSafeJevDecisionEngine : IDecisionEngine
{
    private readonly IJevClient _client;
    private readonly TriageOptions _options;
    private readonly AiProvider _provider;

    /// <summary>Creates the engine.</summary>
    /// <param name="client">The Jev transport.</param>
    /// <param name="options">Model name and thresholds.</param>
    /// <param name="selection">
    /// Which provider the resolver settled on. The same client and mapper serve both the hosted API
    /// and a self-hosted System One model, so the engine is told which one it is rather than
    /// guessing from its own configuration.
    /// </param>
    public TypeSafeJevDecisionEngine(
        IJevClient client,
        IOptions<TriageOptions> options,
        DecisionEngineSelection selection)
    {
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(selection);

        _client = client ?? throw new ArgumentNullException(nameof(client));
        _options = options.Value;
        _provider = selection.Provider;
    }

    /// <inheritdoc />
    public AiProvider Provider => _client.IsLive ? _provider : AiProvider.Mock;

    /// <inheritdoc />
    public bool IsLive => _client.IsLive;

    /// <inheritdoc />
    public async Task<DecisionResult> EvaluateAsync(TicketInput input, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(input);

        var ticket = new SupportTicket(input.Title, input.Description, input.RequesterRole);
        var request = JevTriageQuestions.BuildRequest(ticket, _options.Model);

        JevSystemOneResponse response;
        try
        {
            response = await _client.EvaluateAsync(request, cancellationToken).ConfigureAwait(false);
        }
        catch (JevClientException exception)
        {
            throw new DecisionEngineException(
                exception.Message,
                Provider,
                exception.StatusCode is null ? DecisionFailureKind.Transport : DecisionFailureKind.Provider,
                exception.StatusCode,
                exception);
        }

        try
        {
            return JevAnswerMapper.Map(response, Provider);
        }
        catch (JevClientException exception)
        {
            // The mapper rejects a missing question, an unknown option, or an out-of-range score
            // rather than routing on it.
            throw new DecisionEngineException(
                exception.Message,
                Provider,
                DecisionFailureKind.MalformedResponse,
                innerException: exception);
        }
    }
}
