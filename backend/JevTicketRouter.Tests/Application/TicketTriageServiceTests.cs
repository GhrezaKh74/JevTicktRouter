using FluentAssertions;
using JevTicketRouter.Application.Decisions;
using JevTicketRouter.Application.Tickets;
using JevTicketRouter.Application.Tickets.Dtos;
using JevTicketRouter.Domain.Decisions;
using JevTicketRouter.Domain.Redaction;
using JevTicketRouter.Domain.Tickets;
using JevTicketRouter.Domain.Triage;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using NSubstitute;

namespace JevTicketRouter.Tests.Application;

/// <summary>
/// Covers the triage pipeline against a mocked <see cref="IDecisionEngine"/>: what the engine is
/// asked, the provenance that comes back, and the redaction applied on the way out.
/// <para>
/// The engine is a substitute rather than a real provider, which is the point of the abstraction:
/// these assertions hold identically whether Jev, a local model, or the mock is configured.
/// </para>
/// </summary>
public sealed class TicketTriageServiceTests
{
    private readonly IDecisionEngine _engine = Substitute.For<IDecisionEngine>();

    [Fact]
    public async Task TriageAsync_AsksTheEngineExactlyOnce()
    {
        GivenDecision();

        await CreateService().TriageAsync(Request(), CancellationToken.None);

        await _engine.Received(1).EvaluateAsync(Arg.Any<TicketInput>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task TriageAsync_WithAConfidentTicket_ReturnsEngineValuesUntouched()
    {
        GivenDecision();

        var response = await CreateService().TriageAsync(Request(), CancellationToken.None);

        response.Category.Value.Should().Be(nameof(TicketCategory.AccessRequest));
        response.Category.Origin.Should().Be(nameof(DecisionOrigin.JevModel));
        response.TargetTeam.Value.Should().Be(nameof(TargetTeam.IdentityAccess));
        response.NeedsHumanReview.Value.Should().BeFalse();
        response.AppliedRules.Should().BeEmpty();
        response.TicketId.Should().NotBeNullOrWhiteSpace();
    }

    [Fact]
    public async Task TriageAsync_WithSensitiveData_RedactsTheTicketTextFromTheResponse()
    {
        const string secret = "my card number is 6037991234567890";
        GivenDecision(sensitiveProbability: 0.96);

        var response = await CreateService().TriageAsync(
            Request(description: $"Please help, {secret} and it was charged twice."),
            CancellationToken.None);

        response.ContainsSensitiveData.Value.Should().BeTrue();
        response.Jev.StateSummary.Redacted.Should().BeTrue();
        response.Jev.StateSummary.Description.Should().Be(SensitiveTextRedactor.RedactedPlaceholder);
        response.Jev.StateSummary.Title.Should().Be(SensitiveTextRedactor.RedactedPlaceholder);
        response.Jev.StateSummary.Description.Should().NotContain("6037991234567890");
        response.AppliedRules.Should().Contain(rule => rule.Id == TriageRuleEngine.SensitiveDataRedactionRuleId);
    }

    [Fact]
    public async Task TriageAsync_WithoutSensitiveData_EchoesTheTicketTextBack()
    {
        GivenDecision();

        var response = await CreateService().TriageAsync(
            Request(description: "The reporting portal fails to save a record."),
            CancellationToken.None);

        response.Jev.StateSummary.Redacted.Should().BeFalse();
        response.Jev.StateSummary.Description.Should().Contain("reporting portal");
    }

    [Fact]
    public async Task TriageAsync_WhenARuleOverrides_SaysSoOnTheField()
    {
        GivenDecision(category: TicketCategory.SecurityConcern);

        var response = await CreateService().TriageAsync(Request(), CancellationToken.None);

        response.NeedsHumanReview.Value.Should().BeTrue();
        response.NeedsHumanReview.ModelValue.Should().BeFalse();
        response.NeedsHumanReview.Origin.Should().Be(nameof(DecisionOrigin.BusinessRule));
        response.NeedsHumanReview.WasOverridden.Should().BeTrue();
    }

    [Theory]
    [InlineData(AiProvider.Jev, true)]
    [InlineData(AiProvider.Local, true)]
    [InlineData(AiProvider.Mock, false)]
    public async Task TriageAsync_ReportsWhicheverProviderDecided(AiProvider provider, bool isLive)
    {
        GivenDecision(provider: provider, isLive: isLive);

        var response = await CreateService().TriageAsync(Request(), CancellationToken.None);

        response.Jev.Provider.Should().Be(provider.ToString());
        response.Jev.IsLive.Should().Be(isLive);
    }

    [Fact]
    public async Task TriageAsync_TrimsTheTicketBeforeSendingIt()
    {
        GivenDecision();

        await CreateService().TriageAsync(
            Request(title: "  A padded title  "),
            CancellationToken.None);

        var sent = CapturedInput();

        sent.Title.Should().Be("A padded title");
        sent.RequesterRole.Should().Be(RequesterRole.InternalSupport);
    }

    [Fact]
    public async Task TriageAsync_PropagatesEngineFailures()
    {
        _engine
            .EvaluateAsync(Arg.Any<TicketInput>(), Arg.Any<CancellationToken>())
            .Returns<Task<DecisionResult>>(_ => throw new DecisionEngineException(
                "upstream is down",
                AiProvider.Local,
                DecisionFailureKind.Transport));

        var act = async () => await CreateService().TriageAsync(Request(), CancellationToken.None);

        await act.Should().ThrowAsync<DecisionEngineException>().WithMessage("upstream is down");
    }

    [Fact]
    public async Task TriageAsync_ForwardsTheCancellationToken()
    {
        GivenDecision();
        using var cts = new CancellationTokenSource();

        await CreateService().TriageAsync(Request(), cts.Token);

        await _engine.Received(1).EvaluateAsync(Arg.Any<TicketInput>(), cts.Token);
    }

    private TicketTriageService CreateService() => new(
        _engine,
        Options.Create(new TriageOptions()),
        NullLogger<TicketTriageService>.Instance);

    private TicketInput CapturedInput()
    {
        var calls = _engine.ReceivedCalls()
            .Where(call => call.GetMethodInfo().Name == nameof(IDecisionEngine.EvaluateAsync))
            .ToList();

        calls.Should().NotBeEmpty();

        return (TicketInput)calls[^1].GetArguments()[0]!;
    }

    private static TriageTicketRequest Request(
        string title = "Access to the reporting portal",
        string description = "A new analyst needs read-only access to the quarterly reporting portal.") =>
        new(title, description, RequesterRole.InternalSupport);

    private void GivenDecision(
        TicketCategory category = TicketCategory.AccessRequest,
        double sensitiveProbability = 0.02,
        AiProvider provider = AiProvider.Jev,
        bool isLive = true)
    {
        _engine.Provider.Returns(provider);
        _engine.IsLive.Returns(isLive);
        _engine
            .EvaluateAsync(Arg.Any<TicketInput>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(TestData.CleanAssessment(
                category: category,
                team: TargetTeam.IdentityAccess,
                priority: TicketPriority.Medium,
                sensitiveProbability: sensitiveProbability,
                provider: provider)));
    }
}
