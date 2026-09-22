using System.Text.Json;
using FluentAssertions;
using JevTicketRouter.Application.Jev;
using JevTicketRouter.Application.Jev.Abstractions;
using JevTicketRouter.Application.Jev.Contracts;
using JevTicketRouter.Domain.Triage;

namespace JevTicketRouter.Tests.Application;

/// <summary>
/// Covers the translation of raw TypeSafe answers into a domain assessment, including the malformed
/// responses the mapper must refuse rather than route on.
/// </summary>
public sealed class JevAnswerMapperTests
{
    [Fact]
    public void Map_WithACompleteResponse_ProducesTheExpectedAssessment()
    {
        var assessment = JevAnswerMapper.Map(BuildResponse());

        assessment.Category.Should().Be(TicketCategory.AccessRequest);
        assessment.CategoryConfidence.Should().BeApproximately(0.91, 1e-6);
        assessment.TargetTeam.Should().Be(TargetTeam.IdentityAccess);
        assessment.Priority.Should().Be(TicketPriority.Medium);
        assessment.PriorityScore.Should().BeApproximately(1.2, 1e-6);
        assessment.SensitiveDataProbability.Should().BeApproximately(0.03, 1e-6);
        assessment.HumanReviewProbability.Should().BeApproximately(0.11, 1e-6);
        assessment.Model.Should().Be("jev-1.13.0");
    }

    [Fact]
    public void Map_PrefersTheMostProbableLevelOverTheWeightedScore()
    {
        // The weighted score is 1.2, but nearly all the mass sits on level 2.
        var response = BuildResponse(priority: answer => answer with
        {
            Score = 1.2,
            Probabilities = new Dictionary<string, double> { ["0"] = 0.1, ["1"] = 0.2, ["2"] = 0.7 },
        });

        JevAnswerMapper.Map(response).Priority.Should().Be(TicketPriority.High);
    }

    [Fact]
    public void Map_WithoutProbabilities_FallsBackToRoundingTheScore()
    {
        var response = BuildResponse(priority: answer => answer with
        {
            Score = 2.6,
            Probabilities = null,
        });

        JevAnswerMapper.Map(response).Priority.Should().Be(TicketPriority.Critical);
    }

    [Fact]
    public void Map_WithAScoreBeyondTheDefinedLevels_ClampsIntoRange()
    {
        var response = BuildResponse(priority: answer => answer with
        {
            Score = 9.0,
            Probabilities = null,
        });

        JevAnswerMapper.Map(response).Priority.Should().Be(TicketPriority.Critical);
    }

    [Fact]
    public void Map_WithNoulConfidenceAbsent_IsAccepted()
    {
        // The TypeSafe API deliberately returns no confidence on noul answers.
        var response = BuildResponse();

        response.Answers[JevTriageQuestions.SensitiveDataQuestionId].Confidence.Should().BeNull();

        var act = () => JevAnswerMapper.Map(response);

        act.Should().NotThrow();
    }

    [Fact]
    public void Map_WithAMissingAnswer_Throws()
    {
        var answers = BuildResponse().Answers.ToDictionary(pair => pair.Key, pair => pair.Value);
        answers.Remove(JevTriageQuestions.TargetTeamQuestionId);

        var act = () => JevAnswerMapper.Map(new JevSystemOneResponse { Model = "jev-1.13.0", Answers = answers });

        act.Should().Throw<JevClientException>()
            .WithMessage($"*{JevTriageQuestions.TargetTeamQuestionId}*");
    }

    [Fact]
    public void Map_WithAnUnknownChoiceOption_Throws()
    {
        var response = BuildResponse(category: answer => answer with { Choice = "SomethingElse" });

        var act = () => JevAnswerMapper.Map(response);

        act.Should().Throw<JevClientException>().WithMessage("*SomethingElse*");
    }

    [Fact]
    public void Map_WithAMissingScore_Throws()
    {
        var response = BuildResponse(priority: answer => answer with { Score = null });

        var act = () => JevAnswerMapper.Map(response);

        act.Should().Throw<JevClientException>().WithMessage("*did not include a score*");
    }

    [Fact]
    public void Map_WithAMissingNoulValue_Throws()
    {
        var response = BuildResponse(sensitive: answer => answer with { Noul = null });

        var act = () => JevAnswerMapper.Map(response);

        act.Should().Throw<JevClientException>().WithMessage("*did not include a noul value*");
    }

    [Fact]
    public void Map_WithOutOfRangeConfidence_ClampsToTheUnitInterval()
    {
        var response = BuildResponse(category: answer => answer with { Confidence = 1.9 });

        JevAnswerMapper.Map(response).CategoryConfidence.Should().Be(1d);
    }

    [Fact]
    public void Map_WithAnEmptyModelName_ReportsItAsUnknown()
    {
        var response = BuildResponse() with { Model = "" };

        JevAnswerMapper.Map(response).Model.Should().Be("unknown");
    }

    private static JevSystemOneResponse BuildResponse(
        Func<JevAnswer, JevAnswer>? category = null,
        Func<JevAnswer, JevAnswer>? priority = null,
        Func<JevAnswer, JevAnswer>? sensitive = null)
    {
        var categoryAnswer = new JevAnswer
        {
            Type = "choice",
            Choice = nameof(TicketCategory.AccessRequest),
            Probabilities = new Dictionary<string, double> { [nameof(TicketCategory.AccessRequest)] = 0.91 },
            Confidence = 0.91,
        };

        var priorityAnswer = new JevAnswer
        {
            Type = "score",
            Score = 1.2,
            Probabilities = new Dictionary<string, double> { ["0"] = 0.1, ["1"] = 0.6, ["2"] = 0.3 },
            Legend = new Dictionary<string, JsonElement>
            {
                ["0"] = JsonSerializer.SerializeToElement("Low"),
                ["1"] = JsonSerializer.SerializeToElement("Medium"),
                ["2"] = JsonSerializer.SerializeToElement("High"),
            },
            Confidence = 0.82,
        };

        var sensitiveAnswer = new JevAnswer { Type = "noul", Noul = 0.03 };

        return new JevSystemOneResponse
        {
            Model = "jev-1.13.0",
            Answers = new Dictionary<string, JevAnswer>
            {
                [JevTriageQuestions.CategoryQuestionId] =
                    category is null ? categoryAnswer : category(categoryAnswer),
                [JevTriageQuestions.TargetTeamQuestionId] = new()
                {
                    Type = "choice",
                    Choice = nameof(TargetTeam.IdentityAccess),
                    Probabilities = new Dictionary<string, double> { [nameof(TargetTeam.IdentityAccess)] = 0.88 },
                    Confidence = 0.88,
                },
                [JevTriageQuestions.PriorityQuestionId] =
                    priority is null ? priorityAnswer : priority(priorityAnswer),
                [JevTriageQuestions.SensitiveDataQuestionId] =
                    sensitive is null ? sensitiveAnswer : sensitive(sensitiveAnswer),
                [JevTriageQuestions.HumanReviewQuestionId] = new() { Type = "noul", Noul = 0.11 },
            },
            Usage = new JevUsage { InputTokens = 312, OutputTokens = 48 },
        };
    }
}
