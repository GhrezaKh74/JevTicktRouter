using FluentAssertions;
using JevTicketRouter.Application.Benchmarking;
using JevTicketRouter.Application.Decisions;
using JevTicketRouter.Application.Tickets;
using JevTicketRouter.Domain.Decisions;
using JevTicketRouter.Domain.Triage;
using JevTicketRouter.Infrastructure.Benchmarking;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace JevTicketRouter.Tests.Decisions;

/// <summary>
/// Covers the provider benchmark with scripted engines.
/// <para>
/// Scripted rather than real, because a meaningful Jev-versus-Local comparison needs both a TypeSafe
/// key and a running local model. These tests pin the measurement logic — latency, schema-validity,
/// routing agreement — and the promise that no ticket content is ever retained.
/// </para>
/// </summary>
public sealed class BenchmarkRunnerTests
{
    [Fact]
    public async Task RunAsync_WithOneProvider_ReportsItsMetricsAndNoComparison()
    {
        var report = await RunAsync(new ScriptedEngine(AiProvider.Local, "local-model"));

        report.Providers.Should().ContainSingle();
        report.Providers[0].Provider.Should().Be(AiProvider.Local);
        report.Providers[0].Succeeded.Should().Be(BenchmarkTickets.All.Count);
        report.Providers[0].SchemaValidRate.Should().Be(1);
        report.Agreement.Should().BeNull();
        report.Notes.Should().Contain(note => note.Contains("no second provider"));
    }

    [Fact]
    public async Task RunAsync_WithTwoAgreeingProviders_ReportsFullAgreement()
    {
        var report = await RunAsync(
            new ScriptedEngine(AiProvider.Jev, "jev-1.13.0"),
            new ScriptedEngine(AiProvider.Local, "local-model"));

        report.Agreement.Should().NotBeNull();
        report.Agreement!.Compared.Should().Be(BenchmarkTickets.All.Count);
        report.Agreement.FullRoutingAgreement.Should().Be(1);
        report.Agreement.DisagreedTicketIds.Should().BeEmpty();
    }

    [Fact]
    public async Task RunAsync_WithDisagreeingProviders_NamesTheTicketsThatDiffer()
    {
        var disagreeing = new ScriptedEngine(AiProvider.Local, "local-model")
        {
            TeamOverride = TargetTeam.Infrastructure,
        };

        var report = await RunAsync(new ScriptedEngine(AiProvider.Jev, "jev-1.13.0"), disagreeing);

        report.Agreement!.TargetTeamAgreement.Should().Be(0);
        report.Agreement.CategoryAgreement.Should().Be(1, "only the team was changed");
        report.Agreement.FullRoutingAgreement.Should().Be(0);
        report.Agreement.DisagreedTicketIds.Should().HaveCount(BenchmarkTickets.All.Count);
    }

    [Fact]
    public async Task RunAsync_CountsAFailingTicketAgainstTheSchemaValidRate()
    {
        var flaky = new ScriptedEngine(AiProvider.Local, "local-model")
        {
            FailTicketId = BenchmarkTickets.All[0].Id,
        };

        var report = await RunAsync(flaky);
        var summary = report.Providers[0];

        summary.Failed.Should().Be(1);
        summary.Succeeded.Should().Be(BenchmarkTickets.All.Count - 1);
        summary.SchemaValidRate.Should().BeLessThan(1);
        summary.Failures.Should().ContainSingle()
            .Which.TicketId.Should().Be(BenchmarkTickets.All[0].Id);
    }

    [Fact]
    public async Task RunAsync_ComparesOnlyTicketsWhereBothProvidersSucceeded()
    {
        var flaky = new ScriptedEngine(AiProvider.Local, "local-model")
        {
            FailTicketId = BenchmarkTickets.All[0].Id,
        };

        var report = await RunAsync(new ScriptedEngine(AiProvider.Jev, "jev-1.13.0"), flaky);

        report.Agreement!.Compared.Should().Be(BenchmarkTickets.All.Count - 1);
    }

    [Fact]
    public async Task RunAsync_NeverRetainsTicketContent()
    {
        // The report is what leaves the server. It must identify a case by id and nothing more.
        var flaky = new ScriptedEngine(AiProvider.Local, "local-model")
        {
            FailTicketId = BenchmarkTickets.All[0].Id,
        };

        var report = await RunAsync(flaky);
        var serialised = System.Text.Json.JsonSerializer.Serialize(report);

        foreach (var ticket in BenchmarkTickets.All)
        {
            serialised.Should().NotContain(ticket.Input.Title);
            serialised.Should().NotContain(ticket.Input.Description);
        }
    }

    [Fact]
    public async Task RunAsync_ReportsEveryTicketAsAttempted()
    {
        var report = await RunAsync(new ScriptedEngine(AiProvider.Mock, "mock"));

        report.TicketCount.Should().Be(BenchmarkTickets.All.Count);
        report.StartedAt.Should().BeCloseTo(DateTimeOffset.UtcNow, TimeSpan.FromMinutes(1));
    }

    [Fact]
    public void IsAvailable_IsFalseWithNoEngines()
    {
        var runner = new BenchmarkRunner(
            new BenchmarkEngines([]),
            Options.Create(new TriageOptions()),
            NullLogger<BenchmarkRunner>.Instance);

        runner.IsAvailable.Should().BeFalse();
    }

    [Fact]
    public async Task RunAsync_WithNoEngines_ReportsThatThereWasNothingToMeasure()
    {
        var runner = new BenchmarkRunner(
            new BenchmarkEngines([]),
            Options.Create(new TriageOptions()),
            NullLogger<BenchmarkRunner>.Instance);

        var report = await runner.RunAsync(CancellationToken.None);

        report.Providers.Should().BeEmpty();
        report.Notes.Should().Contain(note => note.Contains("nothing to measure"));
    }

    [Fact]
    public void BenchmarkTickets_AreFictionalAndCoverBothLanguages()
    {
        BenchmarkTickets.All.Should().HaveCountGreaterThan(3);
        BenchmarkTickets.All.Select(ticket => ticket.Id).Should().OnlyHaveUniqueItems();
        BenchmarkTickets.All.Should().Contain(ticket => ticket.Input.Description.Any(c => c >= 0x0600 && c <= 0x06FF));
    }

    private static async Task<BenchmarkReport> RunAsync(params IDecisionEngine[] engines)
    {
        var runner = new BenchmarkRunner(
            new BenchmarkEngines(engines),
            Options.Create(new TriageOptions()),
            NullLogger<BenchmarkRunner>.Instance);

        runner.IsAvailable.Should().BeTrue();

        return await runner.RunAsync(CancellationToken.None);
    }

    /// <summary>An engine with scripted answers, so the measurement logic can be pinned exactly.</summary>
    private sealed class ScriptedEngine(AiProvider provider, string model) : IDecisionEngine
    {
        public AiProvider Provider { get; } = provider;

        public bool IsLive => true;

        /// <summary>Returns a different team, to produce a routing disagreement.</summary>
        public TargetTeam TeamOverride { get; init; } = TargetTeam.ApplicationSupport;

        /// <summary>Fails on exactly this corpus id.</summary>
        public string? FailTicketId { get; init; }

        public Task<DecisionResult> EvaluateAsync(TicketInput input, CancellationToken cancellationToken)
        {
            var ticket = BenchmarkTickets.All.First(candidate => candidate.Input == input);

            if (ticket.Id == FailTicketId)
            {
                throw new DecisionEngineException(
                    "scripted failure",
                    Provider,
                    DecisionFailureKind.MalformedResponse);
            }

            return Task.FromResult(new DecisionResult(
                TicketCategory.TechnicalIssue,
                0.93,
                TeamOverride,
                0.91,
                TicketPriority.Medium,
                0.88,
                1.0,
                0.02,
                0.05,
                model,
                Provider));
        }
    }
}
