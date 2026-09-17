using FluentAssertions;
using JevTicketRouter.Domain.Triage;

namespace JevTicketRouter.Tests.Domain;

/// <summary>
/// Covers the deterministic rules that have the final say over Jev's proposal.
/// </summary>
public sealed class TriageRuleEngineTests
{
    [Fact]
    public void Apply_WithConfidentUnremarkableAssessment_KeepsJevValuesAndFiresNoRules()
    {
        var decision = TriageRuleEngine.Apply(TestData.CleanAssessment());

        decision.Category.Value.Should().Be(TicketCategory.TechnicalIssue);
        decision.Category.Origin.Should().Be(DecisionOrigin.JevModel);
        decision.TargetTeam.Origin.Should().Be(DecisionOrigin.JevModel);
        decision.Priority.Origin.Should().Be(DecisionOrigin.JevModel);
        decision.NeedsHumanReview.Value.Should().BeFalse();
        decision.ContainsSensitiveData.Value.Should().BeFalse();
        decision.AppliedRules.Should().BeEmpty();
        decision.RoutingSummary.Should().Contain("Auto-routed");
    }

    [Fact]
    public void Apply_WithSecurityConcern_ForcesHumanReview()
    {
        var assessment = TestData.CleanAssessment(category: TicketCategory.SecurityConcern);

        var decision = TriageRuleEngine.Apply(assessment);

        decision.NeedsHumanReview.Value.Should().BeTrue();
        decision.NeedsHumanReview.Origin.Should().Be(DecisionOrigin.BusinessRule);
        decision.NeedsHumanReview.WasOverridden.Should().BeTrue();
        decision.NeedsHumanReview.ModelValue.Should().BeFalse("Jev did not ask for a review on its own");
        decision.AppliedRules.Should().ContainSingle(rule => rule.Id == TriageRuleEngine.SecurityEscalationRuleId);
    }

    [Fact]
    public void Apply_WithCriticalPriority_ForcesHumanReview()
    {
        var assessment = TestData.CleanAssessment(priority: TicketPriority.Critical);

        var decision = TriageRuleEngine.Apply(assessment);

        decision.NeedsHumanReview.Value.Should().BeTrue();
        decision.AppliedRules
            .Should().ContainSingle(rule => rule.Id == TriageRuleEngine.SecurityEscalationRuleId)
            .Which.Effect.Should().Contain("priority is Critical");
    }

    [Fact]
    public void Apply_WithSecurityConcernAndCriticalPriority_RecordsBothReasonsInOneRule()
    {
        var assessment = TestData.CleanAssessment(
            category: TicketCategory.SecurityConcern,
            priority: TicketPriority.Critical);

        var decision = TriageRuleEngine.Apply(assessment);

        decision.AppliedRules
            .Should().ContainSingle(rule => rule.Id == TriageRuleEngine.SecurityEscalationRuleId)
            .Which.Effect.Should().Contain("category is SecurityConcern and priority is Critical");
    }

    [Theory]
    [InlineData(0.74, 0.95, 0.95, "category")]
    [InlineData(0.95, 0.74, 0.95, "targetTeam")]
    [InlineData(0.95, 0.95, 0.74, "priority")]
    public void Apply_WithConfidenceBelowThreshold_ForcesHumanReview(
        double categoryConfidence,
        double teamConfidence,
        double priorityConfidence,
        string expectedField)
    {
        var assessment = TestData.CleanAssessment(
            categoryConfidence: categoryConfidence,
            teamConfidence: teamConfidence,
            priorityConfidence: priorityConfidence);

        var decision = TriageRuleEngine.Apply(assessment);

        decision.NeedsHumanReview.Value.Should().BeTrue();
        decision.NeedsHumanReview.Origin.Should().Be(DecisionOrigin.BusinessRule);
        decision.AppliedRules
            .Should().ContainSingle(rule => rule.Id == TriageRuleEngine.LowConfidenceRuleId)
            .Which.Effect.Should().Contain(expectedField);
    }

    [Fact]
    public void Apply_AtExactlyTheConfidenceThreshold_DoesNotEscalate()
    {
        var assessment = TestData.CleanAssessment(
            categoryConfidence: 0.75,
            teamConfidence: 0.75,
            priorityConfidence: 0.75);

        var decision = TriageRuleEngine.Apply(assessment);

        decision.NeedsHumanReview.Value.Should().BeFalse();
        decision.AppliedRules.Should().NotContain(rule => rule.Id == TriageRuleEngine.LowConfidenceRuleId);
    }

    [Fact]
    public void Apply_WithAllConfidencesLow_NamesEveryFailingFieldInOneRule()
    {
        var assessment = TestData.CleanAssessment(
            categoryConfidence: 0.2,
            teamConfidence: 0.3,
            priorityConfidence: 0.4);

        var decision = TriageRuleEngine.Apply(assessment);

        var rule = decision.AppliedRules.Single(rule => rule.Id == TriageRuleEngine.LowConfidenceRuleId);
        rule.Effect.Should().Contain("category").And.Contain("targetTeam").And.Contain("priority");
    }

    [Fact]
    public void Apply_WithHighSensitiveDataProbability_FlagsAndRecordsRedaction()
    {
        var assessment = TestData.CleanAssessment(sensitiveProbability: 0.88);

        var decision = TriageRuleEngine.Apply(assessment);

        decision.ContainsSensitiveData.Value.Should().BeTrue();
        decision.AppliedRules.Should().Contain(rule => rule.Id == TriageRuleEngine.SensitiveDataRedactionRuleId);
    }

    [Fact]
    public void Apply_WithLowSensitiveDataProbability_DoesNotFlag()
    {
        var assessment = TestData.CleanAssessment(sensitiveProbability: 0.49);

        var decision = TriageRuleEngine.Apply(assessment);

        decision.ContainsSensitiveData.Value.Should().BeFalse();
        decision.AppliedRules.Should().NotContain(rule => rule.Id == TriageRuleEngine.SensitiveDataRedactionRuleId);
    }

    [Fact]
    public void Apply_WhenJevItselfAsksForReview_KeepsTheFlagAndAttributesItToTheModel()
    {
        var assessment = TestData.CleanAssessment(humanReviewProbability: 0.91);

        var decision = TriageRuleEngine.Apply(assessment);

        decision.NeedsHumanReview.Value.Should().BeTrue();
        decision.NeedsHumanReview.ModelValue.Should().BeTrue();
        decision.NeedsHumanReview.Origin.Should().Be(DecisionOrigin.JevModel);
        decision.NeedsHumanReview.WasOverridden.Should().BeFalse();
        decision.AppliedRules.Should().Contain(rule => rule.Id == TriageRuleEngine.ModelRequestedReviewRuleId);
    }

    [Fact]
    public void Apply_NeverOverridesCategoryTeamOrPriority()
    {
        var assessment = TestData.CleanAssessment(
            category: TicketCategory.SecurityConcern,
            priority: TicketPriority.Critical,
            categoryConfidence: 0.1,
            teamConfidence: 0.1,
            priorityConfidence: 0.1);

        var decision = TriageRuleEngine.Apply(assessment);

        decision.Category.WasOverridden.Should().BeFalse();
        decision.TargetTeam.WasOverridden.Should().BeFalse();
        decision.Priority.WasOverridden.Should().BeFalse();
        decision.Category.Value.Should().Be(assessment.Category);
        decision.TargetTeam.Value.Should().Be(assessment.TargetTeam);
        decision.Priority.Value.Should().Be(assessment.Priority);
    }

    [Fact]
    public void Apply_WithCustomThresholds_UsesThemInsteadOfTheDefaults()
    {
        var assessment = TestData.CleanAssessment(categoryConfidence: 0.8);
        var strict = new TriageThresholds(MinimumConfidence: 0.9);

        var decision = TriageRuleEngine.Apply(assessment, strict);

        decision.NeedsHumanReview.Value.Should().BeTrue();
        decision.AppliedRules.Should().Contain(rule => rule.Id == TriageRuleEngine.LowConfidenceRuleId);
    }

    [Fact]
    public void Apply_WhenEscalated_SaysSoInTheRoutingSummary()
    {
        var decision = TriageRuleEngine.Apply(
            TestData.CleanAssessment(category: TicketCategory.SecurityConcern));

        decision.RoutingSummary.Should().Contain("held for human review");
    }

    [Fact]
    public void Apply_WithNullAssessment_Throws()
    {
        var act = () => TriageRuleEngine.Apply(null!);

        act.Should().Throw<ArgumentNullException>();
    }
}
