using JevTicketRouter.Domain.Decisions;

namespace JevTicketRouter.Application.Benchmarking;

/// <summary>
/// The result of comparing the configured providers over the fictional corpus.
/// <para>
/// Carries metrics only. No ticket text, no model output, and no credential is stored or returned.
/// </para>
/// </summary>
/// <param name="StartedAt">When the run began, UTC.</param>
/// <param name="TicketCount">How many tickets each provider was asked.</param>
/// <param name="Providers">Per-provider metrics.</param>
/// <param name="Agreement">
/// Pairwise routing agreement, or null when fewer than two providers were available to compare.
/// </param>
/// <param name="Notes">Anything an operator should know about the run, such as a skipped provider.</param>
public sealed record BenchmarkReport(
    DateTimeOffset StartedAt,
    int TicketCount,
    IReadOnlyList<ProviderBenchmark> Providers,
    RoutingAgreement? Agreement,
    IReadOnlyList<string> Notes);

/// <summary>Metrics for one provider over the whole corpus.</summary>
/// <param name="Provider">The provider measured.</param>
/// <param name="Model">The model id it reported.</param>
/// <param name="Succeeded">How many tickets produced a valid, schema-conforming decision.</param>
/// <param name="Failed">How many failed for any reason.</param>
/// <param name="SchemaValidRate">Succeeded divided by attempted, from 0 to 1.</param>
/// <param name="MeanLatencyMs">Mean wall-clock latency across successful calls.</param>
/// <param name="MinLatencyMs">Fastest successful call.</param>
/// <param name="MaxLatencyMs">Slowest successful call.</param>
/// <param name="Failures">One entry per failed ticket: its id and why it failed.</param>
public sealed record ProviderBenchmark(
    AiProvider Provider,
    string Model,
    int Succeeded,
    int Failed,
    double SchemaValidRate,
    double MeanLatencyMs,
    long MinLatencyMs,
    long MaxLatencyMs,
    IReadOnlyList<BenchmarkFailure> Failures);

/// <summary>A single failed evaluation. Identifies the ticket by id, never by content.</summary>
/// <param name="TicketId">The corpus id.</param>
/// <param name="Reason">Why it failed.</param>
public sealed record BenchmarkFailure(string TicketId, string Reason);

/// <summary>
/// How often two providers reached the same final routing, after the deterministic rules ran.
/// <para>
/// Compared on the final decision rather than the raw model output, because that is what actually
/// reaches a queue: two engines disagreeing on a confidence but landing on the same team and
/// priority is not a routing difference.
/// </para>
/// </summary>
/// <param name="Left">The first provider.</param>
/// <param name="Right">The second provider.</param>
/// <param name="Compared">Tickets where both providers succeeded.</param>
/// <param name="CategoryAgreement">Share of compared tickets with the same final category.</param>
/// <param name="TargetTeamAgreement">Share with the same final team.</param>
/// <param name="PriorityAgreement">Share with the same final priority.</param>
/// <param name="HumanReviewAgreement">Share with the same human-review outcome.</param>
/// <param name="FullRoutingAgreement">Share where all four matched.</param>
/// <param name="DisagreedTicketIds">Corpus ids where the final routing differed.</param>
public sealed record RoutingAgreement(
    AiProvider Left,
    AiProvider Right,
    int Compared,
    double CategoryAgreement,
    double TargetTeamAgreement,
    double PriorityAgreement,
    double HumanReviewAgreement,
    double FullRoutingAgreement,
    IReadOnlyList<string> DisagreedTicketIds);
