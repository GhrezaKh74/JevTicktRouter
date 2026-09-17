using System.Diagnostics;
using JevTicketRouter.Application.Benchmarking;
using JevTicketRouter.Application.Decisions;
using JevTicketRouter.Application.Tickets;
using JevTicketRouter.Domain.Decisions;
using JevTicketRouter.Domain.Triage;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace JevTicketRouter.Infrastructure.Benchmarking;

/// <summary>
/// Runs the fictional corpus against every registered decision engine and compares them.
/// <para>
/// The comparison set is decided at registration, so the benchmark measures whatever is actually
/// configured: with both a Jev key and a local endpoint set up it compares the two, and with only one
/// it reports that one's latency and schema-validity without a comparison.
/// </para>
/// <para>
/// Nothing about a ticket is retained. Tickets are identified by corpus id, failures record a reason
/// and never the model's output, and no credential appears anywhere in the report.
/// </para>
/// </summary>
public sealed class BenchmarkRunner : IBenchmarkRunner
{
    private readonly IReadOnlyList<IDecisionEngine> _engines;
    private readonly TriageOptions _options;
    private readonly ILogger<BenchmarkRunner> _logger;

    /// <summary>Creates the runner.</summary>
    /// <param name="engines">The explicit comparison set.</param>
    /// <param name="options">Thresholds, so the rules match production exactly.</param>
    /// <param name="logger">Structured logger.</param>
    public BenchmarkRunner(
        BenchmarkEngines engines,
        IOptions<TriageOptions> options,
        ILogger<BenchmarkRunner> logger)
    {
        ArgumentNullException.ThrowIfNull(engines);
        ArgumentNullException.ThrowIfNull(options);

        _engines = engines.Engines;
        _options = options.Value;
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <inheritdoc />
    public bool IsAvailable => _engines.Count > 0;

    /// <inheritdoc />
    public async Task<BenchmarkReport> RunAsync(CancellationToken cancellationToken)
    {
        var startedAt = DateTimeOffset.UtcNow;
        var notes = new List<string>();
        var summaries = new List<ProviderBenchmark>();
        var decisions = new Dictionary<AiProvider, Dictionary<string, TriageDecision>>();

        if (_engines.Count == 0)
        {
            notes.Add("No decision engine is configured, so there was nothing to measure.");
            return new BenchmarkReport(startedAt, BenchmarkTickets.All.Count, [], null, notes);
        }

        foreach (var engine in _engines)
        {
            var (summary, perTicket) = await MeasureAsync(engine, cancellationToken).ConfigureAwait(false);

            summaries.Add(summary);
            decisions[engine.Provider] = perTicket;
        }

        if (_engines.Count == 1)
        {
            notes.Add(
                $"Only the {_engines[0].Provider} provider is configured, so there is no second "
                    + "provider to compare against. Configure both a Jev key and a local endpoint to "
                    + "compare routing agreement.");
        }

        var agreement = BuildAgreement(decisions, notes);

        return new BenchmarkReport(startedAt, BenchmarkTickets.All.Count, summaries, agreement, notes);
    }

    private async Task<(ProviderBenchmark Summary, Dictionary<string, TriageDecision> Decisions)> MeasureAsync(
        IDecisionEngine engine,
        CancellationToken cancellationToken)
    {
        var latencies = new List<long>();
        var failures = new List<BenchmarkFailure>();
        var decisions = new Dictionary<string, TriageDecision>(StringComparer.Ordinal);
        var model = "unknown";

        foreach (var ticket in BenchmarkTickets.All)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var stopwatch = Stopwatch.StartNew();

            try
            {
                var result = await engine.EvaluateAsync(ticket.Input, cancellationToken).ConfigureAwait(false);
                stopwatch.Stop();

                latencies.Add(stopwatch.ElapsedMilliseconds);
                model = result.Model;
                decisions[ticket.Id] = TriageRuleEngine.Apply(result, _options.ToThresholds());
            }
            catch (DecisionEngineException exception)
            {
                stopwatch.Stop();

                // The reason is the engine's own safe message. The model's raw output is never kept.
                failures.Add(new BenchmarkFailure(ticket.Id, $"{exception.Kind}: {exception.Message}"));

                _logger.LogWarning(
                    "Benchmark: {Provider} failed on ticket {TicketId} ({Kind}).",
                    engine.Provider,
                    ticket.Id,
                    exception.Kind);
            }
        }

        var attempted = BenchmarkTickets.All.Count;
        var succeeded = latencies.Count;

        var summary = new ProviderBenchmark(
            engine.Provider,
            model,
            succeeded,
            failures.Count,
            attempted == 0 ? 0 : Math.Round((double)succeeded / attempted, 4),
            succeeded == 0 ? 0 : Math.Round(latencies.Average(), 1),
            succeeded == 0 ? 0 : latencies.Min(),
            succeeded == 0 ? 0 : latencies.Max(),
            failures);

        return (summary, decisions);
    }

    /// <summary>
    /// Compares the first two providers that produced results. With more than two configured, the
    /// remaining pairs are noted rather than silently dropped.
    /// </summary>
    private static RoutingAgreement? BuildAgreement(
        Dictionary<AiProvider, Dictionary<string, TriageDecision>> decisions,
        List<string> notes)
    {
        var providers = decisions.Keys.OrderBy(provider => provider).ToList();

        if (providers.Count < 2)
        {
            return null;
        }

        if (providers.Count > 2)
        {
            notes.Add(
                $"{providers.Count} providers ran; agreement is reported for "
                    + $"{providers[0]} against {providers[1]}.");
        }

        var left = providers[0];
        var right = providers[1];
        var leftDecisions = decisions[left];
        var rightDecisions = decisions[right];

        var shared = leftDecisions.Keys.Where(rightDecisions.ContainsKey).OrderBy(id => id).ToList();

        if (shared.Count == 0)
        {
            notes.Add($"{left} and {right} have no ticket in common where both succeeded.");
            return new RoutingAgreement(left, right, 0, 0, 0, 0, 0, 0, []);
        }

        var categoryMatches = 0;
        var teamMatches = 0;
        var priorityMatches = 0;
        var reviewMatches = 0;
        var fullMatches = 0;
        var disagreed = new List<string>();

        foreach (var id in shared)
        {
            var a = leftDecisions[id];
            var b = rightDecisions[id];

            var category = a.Category.Value == b.Category.Value;
            var team = a.TargetTeam.Value == b.TargetTeam.Value;
            var priority = a.Priority.Value == b.Priority.Value;
            var review = a.NeedsHumanReview.Value == b.NeedsHumanReview.Value;

            if (category)
            {
                categoryMatches++;
            }

            if (team)
            {
                teamMatches++;
            }

            if (priority)
            {
                priorityMatches++;
            }

            if (review)
            {
                reviewMatches++;
            }

            if (category && team && priority && review)
            {
                fullMatches++;
            }
            else
            {
                disagreed.Add(id);
            }
        }

        double Share(int count) => Math.Round((double)count / shared.Count, 4);

        return new RoutingAgreement(
            left,
            right,
            shared.Count,
            Share(categoryMatches),
            Share(teamMatches),
            Share(priorityMatches),
            Share(reviewMatches),
            Share(fullMatches),
            disagreed);
    }
}
