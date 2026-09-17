using FluentAssertions;
using JevTicketRouter.Application.Jev;
using JevTicketRouter.Application.Jev.Abstractions;
using JevTicketRouter.Application.Jev.Contracts;
using JevTicketRouter.Application.Tickets;
using JevTicketRouter.Application.Tickets.Dtos;
using JevTicketRouter.Domain.Redaction;
using JevTicketRouter.Domain.Tickets;
using JevTicketRouter.Domain.Triage;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using NSubstitute;

namespace JevTicketRouter.Tests.Application;

/// <summary>
/// Covers the triage pipeline against a mocked <see cref="IJevClient"/>: the question batch that is
/// sent, the provenance that comes back, and the redaction applied on the way out.
/// </summary>
public sealed class TicketTriageServiceTests
{
    private readonly IJevClient _jevClient = Substitute.For<IJevClient>();

    [Fact]
    public async Task TriageAsync_SendsEveryQuestionInASingleBatchedCall()
    {
        GivenAnswers();
        var service = CreateService();

        await service.TriageAsync(Request(), CancellationToken.None);

        var request = await CapturedRequestAsync();

        request.Questions.Should().HaveCount(5);
        request.Questions.Keys.Should().BeEquivalentTo(
            JevTriageQuestions.CategoryQuestionId,
            JevTriageQuestions.TargetTeamQuestionId,
            JevTriageQuestions.PriorityQuestionId,
            JevTriageQuestions.SensitiveDataQuestionId,
            JevTriageQuestions.HumanReviewQuestionId);

        await _jevClient.Received(1).EvaluateAsync(Arg.Any<JevSystemOneRequest>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task TriageAsync_UsesTheConfiguredModelAndTheDocumentedPrimitives()
    {
        GivenAnswers();
        var service = CreateService();

        await service.TriageAsync(Request(), CancellationToken.None);

        var request = await CapturedRequestAsync();

        request.Model.Should().Be("jev-latest");
        request.Questions[JevTriageQuestions.CategoryQuestionId].Should().BeOfType<JevChoiceQuestion>();
        request.Questions[JevTriageQuestions.TargetTeamQuestionId].Should().BeOfType<JevChoiceQuestion>();
        request.Questions[JevTriageQuestions.PriorityQuestionId].Should().BeOfType<JevScoreQuestion>();
        request.Questions[JevTriageQuestions.SensitiveDataQuestionId].Should().BeOfType<JevNoulQuestion>();
        request.Questions[JevTriageQuestions.HumanReviewQuestionId].Should().BeOfType<JevNoulQuestion>();
    }

    [Fact]
    public async Task TriageAsync_AsksTheScoreQuestionWithOneLevelPerPriority()
    {
        GivenAnswers();
        var service = CreateService();

        await service.TriageAsync(Request(), CancellationToken.None);

        var request = await CapturedRequestAsync();
        var priority = (JevScoreQuestion)request.Questions[JevTriageQuestions.PriorityQuestionId];

        priority.Criteria.Should().HaveCount(Enum.GetValues<TicketPriority>().Length);
    }

    [Fact]
    public async Task TriageAsync_WithAConfidentTicket_ReturnsJevValuesUntouched()
    {
        GivenAnswers();
        var service = CreateService();

        var response = await service.TriageAsync(Request(), CancellationToken.None);

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
        GivenAnswers(sensitiveProbability: 0.96);
        var service = CreateService();

        var response = await service.TriageAsync(
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
        GivenAnswers();
        var service = CreateService();

        var response = await service.TriageAsync(
            Request(description: "The reporting portal fails to save a record."),
            CancellationToken.None);

        response.Jev.StateSummary.Redacted.Should().BeFalse();
        response.Jev.StateSummary.Description.Should().Contain("reporting portal");
    }

    [Fact]
    public async Task TriageAsync_WhenARuleOverrides_SaysSoOnTheField()
    {
        GivenAnswers(category: TicketCategory.SecurityConcern);
        var service = CreateService();

        var response = await service.TriageAsync(Request(), CancellationToken.None);

        response.NeedsHumanReview.Value.Should().BeTrue();
        response.NeedsHumanReview.ModelValue.Should().BeFalse();
        response.NeedsHumanReview.Origin.Should().Be(nameof(DecisionOrigin.BusinessRule));
        response.NeedsHumanReview.WasOverridden.Should().BeTrue();
    }

    [Fact]
    public async Task TriageAsync_ReportsTheModeOfTheUnderlyingClient()
    {
        GivenAnswers();
        _jevClient.IsLive.Returns(false);

        var response = await CreateService().TriageAsync(Request(), CancellationToken.None);

        response.Jev.Mode.Should().Be("Mock");
    }

    [Fact]
    public async Task TriageAsync_SendsTheTicketAsStructuredStateWithoutTrailingWhitespace()
    {
        GivenAnswers();
        var service = CreateService();

        await service.TriageAsync(
            Request(title: "  A padded title  "),
            CancellationToken.None);

        var request = await CapturedRequestAsync();
        var state = request.State.Should().BeAssignableTo<IReadOnlyDictionary<string, object>>().Subject;
        var ticket = state["ticket"].Should().BeAssignableTo<IReadOnlyDictionary<string, string>>().Subject;

        ticket["title"].Should().Be("A padded title");
        ticket.Should().ContainKey("requester_role");
    }

    [Fact]
    public async Task TriageAsync_PropagatesClientFailures()
    {
        _jevClient
            .EvaluateAsync(Arg.Any<JevSystemOneRequest>(), Arg.Any<CancellationToken>())
            .Returns<Task<JevSystemOneResponse>>(_ => throw new JevClientException("upstream is down"));

        var act = async () => await CreateService().TriageAsync(
            Request(),
            CancellationToken.None);

        await act.Should().ThrowAsync<JevClientException>().WithMessage("upstream is down");
    }

    [Fact]
    public async Task TriageAsync_ForwardsTheCancellationToken()
    {
        GivenAnswers();
        using var cts = new CancellationTokenSource();

        await CreateService().TriageAsync(Request(), cts.Token);

        await _jevClient.Received(1).EvaluateAsync(Arg.Any<JevSystemOneRequest>(), cts.Token);
    }

    private TicketTriageService CreateService() => new(
        _jevClient,
        Options.Create(new TriageOptions()),
        NullLogger<TicketTriageService>.Instance);

    private async Task<JevSystemOneRequest> CapturedRequestAsync()
    {
        await Task.CompletedTask;

        // ReceivedCalls() also reports property getters such as IsLive, which take no arguments.
        var calls = _jevClient.ReceivedCalls()
            .Where(call => call.GetMethodInfo().Name == nameof(IJevClient.EvaluateAsync))
            .ToList();

        calls.Should().NotBeEmpty();

        return (JevSystemOneRequest)calls[^1].GetArguments()[0]!;
    }

    private static TriageTicketRequest Request(
        string title = "Access to the reporting portal",
        string description = "A new analyst needs read-only access to the quarterly reporting portal.") =>
        new(title, description, RequesterRole.InternalSupport);

    private void GivenAnswers(
        TicketCategory category = TicketCategory.AccessRequest,
        double sensitiveProbability = 0.02)
    {
        _jevClient.IsLive.Returns(true);
        _jevClient
            .EvaluateAsync(Arg.Any<JevSystemOneRequest>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(new JevSystemOneResponse
            {
                Model = "jev-1.13.0",
                Answers = new Dictionary<string, JevAnswer>
                {
                    [JevTriageQuestions.CategoryQuestionId] = new()
                    {
                        Type = "choice",
                        Choice = category.ToString(),
                        Confidence = 0.94,
                    },
                    [JevTriageQuestions.TargetTeamQuestionId] = new()
                    {
                        Type = "choice",
                        Choice = nameof(TargetTeam.IdentityAccess),
                        Confidence = 0.92,
                    },
                    [JevTriageQuestions.PriorityQuestionId] = new()
                    {
                        Type = "score",
                        Score = 1.0,
                        Probabilities = new Dictionary<string, double> { ["1"] = 0.95 },
                        Confidence = 0.9,
                    },
                    [JevTriageQuestions.SensitiveDataQuestionId] = new()
                    {
                        Type = "noul",
                        Noul = sensitiveProbability,
                    },
                    [JevTriageQuestions.HumanReviewQuestionId] = new() { Type = "noul", Noul = 0.05 },
                },
                Usage = new JevUsage { InputTokens = 100, OutputTokens = 20 },
            }));
    }
}
