using System.Diagnostics;
using JevTicketRouter.Application.Decisions;
using JevTicketRouter.Application.Tickets.Dtos;
using JevTicketRouter.Domain.Redaction;
using JevTicketRouter.Domain.Tickets;
using JevTicketRouter.Domain.Triage;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace JevTicketRouter.Application.Tickets;

/// <summary>
/// The triage pipeline: ask the configured decision engine, run the deterministic rule engine over
/// its proposal, then redact anything the rules marked sensitive before it reaches a log or the
/// client.
/// <para>
/// This class is deliberately provider-agnostic. It never learns whether the proposal came from
/// TypeSafe Jev, from a model inside the organisation's network, or from the deterministic mock.
/// </para>
/// </summary>
public sealed class TicketTriageService : ITicketTriageService
{
    private const int LoggedTitleMaxLength = 120;

    private readonly IDecisionEngine _engine;
    private readonly TriageOptions _options;
    private readonly ILogger<TicketTriageService> _logger;

    /// <summary>Creates the service.</summary>
    /// <param name="engine">The configured decision engine.</param>
    /// <param name="options">Model name and rule thresholds.</param>
    /// <param name="logger">Structured logger. Ticket text is redacted before it reaches this.</param>
    public TicketTriageService(
        IDecisionEngine engine,
        IOptions<TriageOptions> options,
        ILogger<TicketTriageService> logger)
    {
        ArgumentNullException.ThrowIfNull(options);

        _engine = engine ?? throw new ArgumentNullException(nameof(engine));
        _options = options.Value;
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <inheritdoc />
    public async Task<TriageTicketResponse> TriageAsync(
        TriageTicketRequest request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        var ticketId = Guid.NewGuid().ToString("N")[..12];
        var ticket = new SupportTicket(
            request.Title.Trim(),
            request.Description.Trim(),
            request.RequesterRole);

        _logger.LogInformation(
            "Triage {TicketId} started. Role={RequesterRole} TitleLength={TitleLength} DescriptionLength={DescriptionLength} Provider={Provider}",
            ticketId,
            ticket.RequesterRole,
            ticket.Title.Length,
            ticket.Description.Length,
            _engine.Provider);

        var stopwatch = Stopwatch.StartNew();
        var assessment = await _engine
            .EvaluateAsync(TicketInput.From(ticket), cancellationToken)
            .ConfigureAwait(false);
        stopwatch.Stop();

        var decision = TriageRuleEngine.Apply(assessment, _options.ToThresholds());

        LogOutcome(ticketId, ticket, assessment, decision, stopwatch.ElapsedMilliseconds);

        return BuildResponse(ticketId, ticket, assessment, decision, stopwatch.ElapsedMilliseconds);
    }

    private void LogOutcome(
        string ticketId,
        SupportTicket ticket,
        DecisionResult assessment,
        TriageDecision decision,
        long latencyMs)
    {
        var sensitive = decision.ContainsSensitiveData.Value;

        // The title is masked for secret-looking patterns even when the ticket was not flagged; the
        // description is only ever logged as a length, never as text.
        _logger.LogInformation(
            "Triage {TicketId} completed in {LatencyMs}ms. Category={Category} Team={TargetTeam} "
                + "Priority={Priority} Sensitive={Sensitive} HumanReview={HumanReview} "
                + "Rules={AppliedRules} Provider={Provider} Model={Model} Title={Title}",
            ticketId,
            latencyMs,
            decision.Category.Value,
            decision.TargetTeam.Value,
            decision.Priority.Value,
            sensitive,
            decision.NeedsHumanReview.Value,
            string.Join(",", decision.AppliedRules.Select(rule => rule.Id)),
            assessment.Provider,
            assessment.Model,
            sensitive
                ? SensitiveTextRedactor.RedactedPlaceholder
                : SensitiveTextRedactor.Truncate(
                    SensitiveTextRedactor.MaskPatterns(ticket.Title),
                    LoggedTitleMaxLength));

        if (sensitive)
        {
            _logger.LogWarning(
                "Triage {TicketId} was flagged as containing sensitive data; ticket text is redacted from logs and from the API response.",
                ticketId);
        }
    }

    private TriageTicketResponse BuildResponse(
        string ticketId,
        SupportTicket ticket,
        DecisionResult assessment,
        TriageDecision decision,
        long latencyMs)
    {
        var sensitive = decision.ContainsSensitiveData.Value;

        var stateSummary = new JevStateSummaryDto(
            SensitiveTextRedactor.Redact(ticket.Title, sensitive),
            SensitiveTextRedactor.Redact(ticket.Description, sensitive),
            ticket.RequesterRole.ToString(),
            sensitive);

        return new TriageTicketResponse(
            ticketId,
            ToDto(decision.Category, static value => value.ToString()),
            ToDto(decision.TargetTeam, static value => value.ToString()),
            ToDto(decision.Priority, static value => value.ToString()),
            ToDto(decision.ContainsSensitiveData, static value => value),
            ToDto(decision.NeedsHumanReview, static value => value),
            decision.RoutingSummary,
            [.. decision.AppliedRules.Select(rule => new AppliedRuleDto(rule.Id, rule.Description, rule.Effect))],
            new JevDiagnosticsDto(
                assessment.Provider.ToString(),
                _engine.IsLive,
                assessment.Model,
                latencyMs,
                Math.Round(assessment.PriorityScore, 3),
                Math.Round(assessment.SensitiveDataProbability, 3),
                Math.Round(assessment.HumanReviewProbability, 3),
                stateSummary));
    }

    private static DecidedFieldDto<TOut> ToDto<TIn, TOut>(DecidedField<TIn> field, Func<TIn, TOut> project) =>
        new(
            project(field.Value),
            project(field.ModelValue),
            field.Confidence is { } confidence ? Math.Round(confidence, 3) : null,
            field.Origin.ToString(),
            field.WasOverridden);
}
