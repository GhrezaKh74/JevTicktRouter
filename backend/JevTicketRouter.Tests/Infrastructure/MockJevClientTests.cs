using FluentAssertions;
using JevTicketRouter.Application.Jev;
using JevTicketRouter.Domain.Tickets;
using JevTicketRouter.Domain.Triage;
using JevTicketRouter.Infrastructure.Jev;
using Microsoft.Extensions.Logging.Abstractions;

namespace JevTicketRouter.Tests.Infrastructure;

/// <summary>
/// Covers mock mode: it must be deterministic, must answer in the shape the real API documents, and
/// must be plausible enough for the demo tickets in both Persian and English.
/// </summary>
public sealed class MockJevClientTests
{
    private readonly MockJevClient _client = new(NullLogger<MockJevClient>.Instance);

    [Fact]
    public void IsLive_IsFalse()
    {
        _client.IsLive.Should().BeFalse();
    }

    [Fact]
    public async Task EvaluateAsync_AnswersEveryQuestionThatWasAsked()
    {
        var response = await EvaluateAsync(TestData.Ticket());

        response.Answers.Keys.Should().BeEquivalentTo(
            JevTriageQuestions.CategoryQuestionId,
            JevTriageQuestions.TargetTeamQuestionId,
            JevTriageQuestions.PriorityQuestionId,
            JevTriageQuestions.SensitiveDataQuestionId,
            JevTriageQuestions.HumanReviewQuestionId);

        response.Model.Should().Be(MockJevClient.MockModelId);
        response.Usage!.InputTokens.Should().BeGreaterThan(0);
    }

    [Fact]
    public async Task EvaluateAsync_IsDeterministic()
    {
        var ticket = TestData.Ticket();

        var first = JevAnswerMapper.Map(await EvaluateAsync(ticket));
        var second = JevAnswerMapper.Map(await EvaluateAsync(ticket));

        second.Should().BeEquivalentTo(first);
    }

    [Fact]
    public async Task EvaluateAsync_ProducesChoiceAnswersMatchingTheDocumentedShape()
    {
        var response = await EvaluateAsync(TestData.Ticket());
        var answer = response.Answers[JevTriageQuestions.CategoryQuestionId];

        answer.Type.Should().Be("choice");
        answer.Choice.Should().NotBeNullOrWhiteSpace();
        answer.Confidence.Should().BeInRange(0d, 1d);
        answer.Probabilities.Should().NotBeNull();
        answer.Probabilities!.Values.Sum().Should().BeApproximately(1d, 0.01);
        answer.Probabilities.Should().ContainKey(answer.Choice!);
    }

    [Fact]
    public async Task EvaluateAsync_ProducesScoreAnswersMatchingTheDocumentedShape()
    {
        var response = await EvaluateAsync(TestData.Ticket());
        var answer = response.Answers[JevTriageQuestions.PriorityQuestionId];

        answer.Type.Should().Be("score");
        answer.Score.Should().BeInRange(0d, JevTriageQuestions.PriorityLevels.Count - 1);
        answer.Legend.Should().HaveCount(JevTriageQuestions.PriorityLevels.Count);
        answer.Probabilities!.Values.Sum().Should().BeApproximately(1d, 0.01);
        answer.Confidence.Should().BeInRange(0d, 1d);
    }

    [Fact]
    public async Task EvaluateAsync_ProducesNoulAnswersWithoutConfidence()
    {
        var response = await EvaluateAsync(TestData.Ticket());
        var answer = response.Answers[JevTriageQuestions.SensitiveDataQuestionId];

        answer.Type.Should().Be("noul");
        answer.Noul.Should().BeInRange(0d, 1d);
        answer.Confidence.Should().BeNull("the TypeSafe API does not return confidence for noul answers");
    }

    [Fact]
    public async Task EvaluateAsync_RoutesAPersianTechnicalIssueToApplicationSupport()
    {
        var ticket = new SupportTicket(
            "خطا هنگام ثبت تراکنش در سامانه شعبه",
            "از امروز صبح هنگام ثبت تراکنش در سامانه شعبه با خطا مواجه می‌شوم و صفحه لود نمی‌شود.",
            RequesterRole.BranchEmployee);

        var assessment = JevAnswerMapper.Map(await EvaluateAsync(ticket));

        assessment.Category.Should().Be(TicketCategory.TechnicalIssue);
        assessment.TargetTeam.Should().Be(TargetTeam.ApplicationSupport);
    }

    [Fact]
    public async Task EvaluateAsync_RoutesAnEnglishAccessRequestToIdentityAccess()
    {
        var ticket = new SupportTicket(
            "Access to the reporting portal for a new analyst",
            "Our new analyst needs read-only access to the reporting portal. Please provision the account and role.",
            RequesterRole.InternalSupport);

        var assessment = JevAnswerMapper.Map(await EvaluateAsync(ticket));

        assessment.Category.Should().Be(TicketCategory.AccessRequest);
        assessment.TargetTeam.Should().Be(TargetTeam.IdentityAccess);
    }

    [Fact]
    public async Task EvaluateAsync_RoutesAPhishingReportToSecurityAndAsksForAHuman()
    {
        var ticket = new SupportTicket(
            "Suspicious email asking staff to confirm their password",
            "Several colleagues received a suspicious phishing email linking to a fake login page. Please investigate urgently.",
            RequesterRole.InternalSupport);

        var assessment = JevAnswerMapper.Map(await EvaluateAsync(ticket));
        var decision = TriageRuleEngine.Apply(assessment);

        assessment.Category.Should().Be(TicketCategory.SecurityConcern);
        assessment.TargetTeam.Should().Be(TargetTeam.Security);
        decision.NeedsHumanReview.Value.Should().BeTrue();
    }

    [Fact]
    public async Task EvaluateAsync_DetectsSensitiveValuesInTheTicketText()
    {
        var ticket = new SupportTicket(
            "Duplicate charge on my account",
            "My account number is 6037991234567890 and the card was charged twice this morning.",
            RequesterRole.Customer);

        var assessment = JevAnswerMapper.Map(await EvaluateAsync(ticket));

        assessment.SensitiveDataProbability.Should().BeGreaterThan(0.5);
    }

    [Fact]
    public async Task EvaluateAsync_TreatsALongDigitRunAloneAsSensitive()
    {
        // A 16-digit reference is a card or account shape. It must clear the threshold on its own,
        // without any accompanying keyword, or the redaction path never fires for it.
        var ticket = new SupportTicket(
            "Reference number rejected",
            "The system rejects my internal reference 4400123400567800 when I enter it.",
            RequesterRole.Customer);

        var assessment = JevAnswerMapper.Map(await EvaluateAsync(ticket));
        var decision = TriageRuleEngine.Apply(assessment);

        assessment.SensitiveDataProbability.Should().BeGreaterThan(0.5);
        decision.ContainsSensitiveData.Value.Should().BeTrue();
        decision.AppliedRules.Should().Contain(rule => rule.Id == TriageRuleEngine.SensitiveDataRedactionRuleId);
    }

    [Fact]
    public async Task EvaluateAsync_LeavesAnOrdinaryTicketUnflagged()
    {
        var assessment = JevAnswerMapper.Map(await EvaluateAsync(TestData.Ticket()));

        assessment.SensitiveDataProbability.Should().BeLessThan(0.5);
    }

    [Fact]
    public async Task EvaluateAsync_GivesAVagueTicketLowConfidenceSoItIsEscalated()
    {
        var ticket = new SupportTicket(
            "Something is not right",
            "I am not sure what is happening but something seems off today. Could someone take a look?",
            RequesterRole.Customer);

        var assessment = JevAnswerMapper.Map(await EvaluateAsync(ticket));
        var decision = TriageRuleEngine.Apply(assessment);

        assessment.CategoryConfidence.Should().BeLessThan(0.75);
        decision.NeedsHumanReview.Value.Should().BeTrue();
        decision.AppliedRules.Should().Contain(rule => rule.Id == TriageRuleEngine.LowConfidenceRuleId);
    }

    [Fact]
    public async Task EvaluateAsync_WithACancelledToken_Throws()
    {
        using var cts = new CancellationTokenSource();
        await cts.CancelAsync();

        var act = async () => await _client.EvaluateAsync(
            JevTriageQuestions.BuildRequest(TestData.Ticket(), "jev-latest"),
            cts.Token);

        await act.Should().ThrowAsync<OperationCanceledException>();
    }

    private Task<JevTicketRouter.Application.Jev.Contracts.JevSystemOneResponse> EvaluateAsync(SupportTicket ticket) =>
        _client.EvaluateAsync(
            JevTriageQuestions.BuildRequest(ticket, "jev-latest"),
            CancellationToken.None);
}
