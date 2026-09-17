using System.Net;
using FluentAssertions;
using JevTicketRouter.Application.Decisions;
using JevTicketRouter.Application.Jev;
using JevTicketRouter.Application.Jev.Abstractions;
using JevTicketRouter.Application.Jev.Contracts;
using JevTicketRouter.Application.Tickets;
using JevTicketRouter.Domain.Decisions;
using JevTicketRouter.Domain.Tickets;
using JevTicketRouter.Domain.Triage;
using JevTicketRouter.Infrastructure.Decisions;
using Microsoft.Extensions.Options;
using NSubstitute;

namespace JevTicketRouter.Tests.Decisions;

/// <summary>
/// Covers the Jev engine: the batched question set it builds and how transport and mapping failures
/// become <see cref="DecisionEngineException"/>. These assertions moved here from the triage service
/// tests when the provider abstraction went in — the service no longer knows about Jev.
/// </summary>
public sealed class TypeSafeJevDecisionEngineTests
{
    private readonly IJevClient _client = Substitute.For<IJevClient>();

    [Fact]
    public async Task EvaluateAsync_AsksEveryQuestionInASingleBatchedCall()
    {
        GivenAnswers();

        await CreateEngine().EvaluateAsync(Input(), CancellationToken.None);

        var request = CapturedRequest();

        request.Questions.Should().HaveCount(5);
        request.Questions.Keys.Should().BeEquivalentTo(
            JevTriageQuestions.CategoryQuestionId,
            JevTriageQuestions.TargetTeamQuestionId,
            JevTriageQuestions.PriorityQuestionId,
            JevTriageQuestions.SensitiveDataQuestionId,
            JevTriageQuestions.HumanReviewQuestionId);

        await _client.Received(1).EvaluateAsync(Arg.Any<JevSystemOneRequest>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task EvaluateAsync_UsesTheConfiguredModelAndTheDocumentedPrimitives()
    {
        GivenAnswers();

        await CreateEngine().EvaluateAsync(Input(), CancellationToken.None);

        var request = CapturedRequest();

        request.Model.Should().Be("jev-latest");
        request.Questions[JevTriageQuestions.CategoryQuestionId].Should().BeOfType<JevChoiceQuestion>();
        request.Questions[JevTriageQuestions.TargetTeamQuestionId].Should().BeOfType<JevChoiceQuestion>();
        request.Questions[JevTriageQuestions.PriorityQuestionId].Should().BeOfType<JevScoreQuestion>();
        request.Questions[JevTriageQuestions.SensitiveDataQuestionId].Should().BeOfType<JevNoulQuestion>();
        request.Questions[JevTriageQuestions.HumanReviewQuestionId].Should().BeOfType<JevNoulQuestion>();
    }

    [Fact]
    public async Task EvaluateAsync_AsksTheScoreQuestionWithOneLevelPerPriority()
    {
        GivenAnswers();

        await CreateEngine().EvaluateAsync(Input(), CancellationToken.None);

        var priority = (JevScoreQuestion)CapturedRequest().Questions[JevTriageQuestions.PriorityQuestionId];

        priority.Criteria.Should().HaveCount(Enum.GetValues<TicketPriority>().Length);
    }

    [Fact]
    public async Task EvaluateAsync_SendsTheTicketAsStructuredState()
    {
        GivenAnswers();

        await CreateEngine().EvaluateAsync(Input(), CancellationToken.None);

        var state = CapturedRequest().State
            .Should().BeAssignableTo<IReadOnlyDictionary<string, object>>().Subject;
        var ticket = state["ticket"]
            .Should().BeAssignableTo<IReadOnlyDictionary<string, string>>().Subject;

        ticket["title"].Should().Be("Access to the reporting portal");
        ticket.Should().ContainKey("requester_role");
    }

    [Fact]
    public async Task EvaluateAsync_TagsTheResultAsJevWhenTheClientIsLive()
    {
        GivenAnswers();

        var result = await CreateEngine().EvaluateAsync(Input(), CancellationToken.None);

        result.Provider.Should().Be(AiProvider.Jev);
        result.Category.Should().Be(TicketCategory.AccessRequest);
    }

    [Fact]
    public async Task EvaluateAsync_TagsTheResultAsMockWhenTheClientIsNotLive()
    {
        GivenAnswers(isLive: false);

        var result = await CreateEngine().EvaluateAsync(Input(), CancellationToken.None);

        result.Provider.Should().Be(AiProvider.Mock);
    }

    [Fact]
    public async Task EvaluateAsync_TurnsATransportFailureIntoATransientEngineFailure()
    {
        _client.IsLive.Returns(true);
        _client
            .EvaluateAsync(Arg.Any<JevSystemOneRequest>(), Arg.Any<CancellationToken>())
            .Returns<Task<JevSystemOneResponse>>(_ => throw new JevClientException("unreachable"));

        var act = async () => await CreateEngine().EvaluateAsync(Input(), CancellationToken.None);

        var exception = (await act.Should().ThrowAsync<DecisionEngineException>()).Which;
        exception.Kind.Should().Be(DecisionFailureKind.Transport);
        exception.IsTransient.Should().BeTrue();
    }

    [Fact]
    public async Task EvaluateAsync_TurnsAnErrorStatusIntoAProviderFailure()
    {
        _client.IsLive.Returns(true);
        _client
            .EvaluateAsync(Arg.Any<JevSystemOneRequest>(), Arg.Any<CancellationToken>())
            .Returns<Task<JevSystemOneResponse>>(_ => throw new JevClientException(
                "rate limited",
                HttpStatusCode.TooManyRequests));

        var act = async () => await CreateEngine().EvaluateAsync(Input(), CancellationToken.None);

        var exception = (await act.Should().ThrowAsync<DecisionEngineException>()).Which;
        exception.Kind.Should().Be(DecisionFailureKind.Provider);
        exception.StatusCode.Should().Be(HttpStatusCode.TooManyRequests);
        exception.IsTransient.Should().BeTrue();
    }

    [Fact]
    public async Task EvaluateAsync_TurnsAnUnmappableAnswerIntoAMalformedResponseFailure()
    {
        _client.IsLive.Returns(true);
        _client
            .EvaluateAsync(Arg.Any<JevSystemOneRequest>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(new JevSystemOneResponse
            {
                Model = "jev-1.13.0",
                Answers = new Dictionary<string, JevAnswer>
                {
                    [JevTriageQuestions.CategoryQuestionId] = new()
                    {
                        Type = "choice",
                        Choice = "NotARealCategory",
                        Confidence = 0.9,
                    },
                },
            }));

        var act = async () => await CreateEngine().EvaluateAsync(Input(), CancellationToken.None);

        var exception = (await act.Should().ThrowAsync<DecisionEngineException>()).Which;
        exception.Kind.Should().Be(DecisionFailureKind.MalformedResponse);
        exception.IsTransient.Should().BeFalse();
    }

    private TypeSafeJevDecisionEngine CreateEngine() =>
        new(_client, Options.Create(new TriageOptions()));

    private JevSystemOneRequest CapturedRequest()
    {
        var calls = _client.ReceivedCalls()
            .Where(call => call.GetMethodInfo().Name == nameof(IJevClient.EvaluateAsync))
            .ToList();

        calls.Should().NotBeEmpty();

        return (JevSystemOneRequest)calls[^1].GetArguments()[0]!;
    }

    private static TicketInput Input() => new(
        "Access to the reporting portal",
        "A new analyst needs read-only access to the quarterly reporting portal.",
        RequesterRole.InternalSupport);

    private void GivenAnswers(bool isLive = true)
    {
        _client.IsLive.Returns(isLive);
        _client
            .EvaluateAsync(Arg.Any<JevSystemOneRequest>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(new JevSystemOneResponse
            {
                Model = "jev-1.13.0",
                Answers = new Dictionary<string, JevAnswer>
                {
                    [JevTriageQuestions.CategoryQuestionId] = new()
                    {
                        Type = "choice",
                        Choice = nameof(TicketCategory.AccessRequest),
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
                    [JevTriageQuestions.SensitiveDataQuestionId] = new() { Type = "noul", Noul = 0.02 },
                    [JevTriageQuestions.HumanReviewQuestionId] = new() { Type = "noul", Noul = 0.05 },
                },
                Usage = new JevUsage { InputTokens = 100, OutputTokens = 20 },
            }));
    }
}
