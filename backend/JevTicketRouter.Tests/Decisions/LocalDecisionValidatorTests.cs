using FluentAssertions;
using JevTicketRouter.Domain.Decisions;
using JevTicketRouter.Domain.Triage;
using JevTicketRouter.Infrastructure.Decisions.Local;

namespace JevTicketRouter.Tests.Decisions;

/// <summary>
/// Covers parsing a local model's reply.
/// <para>
/// The contract these tests defend is that a malformed reply is always reported, never repaired.
/// A local instruction-tuned model is far less disciplined than a purpose-built classifier, and a
/// ticket routed on a defaulted or guessed value would be worse than one that failed visibly.
/// </para>
/// </summary>
public sealed class LocalDecisionValidatorTests
{
    private const string Model = "qwen2.5:7b-instruct";

    [Fact]
    public void Validate_WithAWellFormedReply_ProducesADecision()
    {
        var validation = LocalDecisionValidator.Validate(Payload(), Model);

        validation.IsValid.Should().BeTrue();

        var result = validation.Result!;
        result.Category.Should().Be(TicketCategory.AccessRequest);
        result.CategoryConfidence.Should().Be(0.91);
        result.TargetTeam.Should().Be(TargetTeam.IdentityAccess);
        result.Priority.Should().Be(TicketPriority.Medium);
        result.PriorityScore.Should().Be((int)TicketPriority.Medium);
        result.SensitiveDataProbability.Should().Be(0.02);
        result.HumanReviewProbability.Should().Be(0.11);
        result.Model.Should().Be(Model);
        result.Provider.Should().Be(AiProvider.Local);
    }

    [Fact]
    public void Validate_AcceptsAReplyWrappedInAMarkdownCodeFence()
    {
        // Small local models do this constantly despite being told not to.
        var content = $"Here is the result:\n\n```json\n{Payload()}\n```\n";

        LocalDecisionValidator.Validate(content, Model).IsValid.Should().BeTrue();
    }

    [Fact]
    public void Validate_AcceptsAReplyWithLeadingProse()
    {
        LocalDecisionValidator.Validate($"Sure! {Payload()}", Model).IsValid.Should().BeTrue();
    }

    [Fact]
    public void Validate_AcceptsNestedBracesInsideStringValues()
    {
        var content = Payload(category: "AccessRequest").Replace(
            "\"category\": \"AccessRequest\"",
            "\"category\": \"AccessRequest\", \"note\": \"a } brace { inside a string\"",
            StringComparison.Ordinal);

        // Unknown extra fields are ignored; the brace scanner must not stop at the one in the string.
        LocalDecisionValidator.Validate(content, Model).IsValid.Should().BeTrue();
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Validate_RejectsAnEmptyReply(string? content)
    {
        var validation = LocalDecisionValidator.Validate(content, Model);

        validation.IsValid.Should().BeFalse();
        validation.Error.Should().Contain("empty");
    }

    [Fact]
    public void Validate_RejectsAReplyWithNoJsonAtAll()
    {
        var validation = LocalDecisionValidator.Validate(
            "I am sorry, I cannot help with that request.",
            Model);

        validation.IsValid.Should().BeFalse();
        validation.Error.Should().Contain("did not contain a JSON object");
    }

    [Fact]
    public void Validate_RejectsTruncatedJson()
    {
        // The usual shape of a hit token limit.
        var validation = LocalDecisionValidator.Validate(
            "{\"category\": \"AccessRequest\", \"category_confidence\": 0.9",
            Model);

        validation.IsValid.Should().BeFalse();
        validation.Result.Should().BeNull();
    }

    [Fact]
    public void Validate_RejectsSyntacticallyInvalidJson()
    {
        var validation = LocalDecisionValidator.Validate("{ \"category\": AccessRequest, }", Model);

        validation.IsValid.Should().BeFalse();
        validation.Result.Should().BeNull();
    }

    [Fact]
    public void Validate_RejectsAnInventedCategory()
    {
        var validation = LocalDecisionValidator.Validate(Payload(category: "BillingProblem"), Model);

        validation.IsValid.Should().BeFalse();
        validation.Error.Should().Contain("category").And.Contain("BillingProblem");
    }

    [Fact]
    public void Validate_RejectsAnInventedTeam()
    {
        var validation = LocalDecisionValidator.Validate(Payload(team: "HelpDesk"), Model);

        validation.IsValid.Should().BeFalse();
        validation.Error.Should().Contain("target_team");
    }

    [Fact]
    public void Validate_RejectsAnInventedPriority()
    {
        var validation = LocalDecisionValidator.Validate(Payload(priority: "Urgent"), Model);

        validation.IsValid.Should().BeFalse();
        validation.Error.Should().Contain("priority");
    }

    [Theory]
    [InlineData("category")]
    [InlineData("target_team")]
    [InlineData("priority")]
    [InlineData("category_confidence")]
    [InlineData("target_team_confidence")]
    [InlineData("priority_confidence")]
    [InlineData("contains_sensitive_data_probability")]
    [InlineData("needs_human_review_probability")]
    public void Validate_RejectsAReplyMissingAnyRequiredField(string field)
    {
        var validation = LocalDecisionValidator.Validate(PayloadWithout(field), Model);

        validation.IsValid.Should().BeFalse();
        validation.Error.Should().Contain(field);
        validation.Result.Should().BeNull("a missing field must never be defaulted");
    }

    [Theory]
    [InlineData(-0.1)]
    [InlineData(1.5)]
    [InlineData(42)]
    public void Validate_RejectsAConfidenceOutsideZeroToOne(double confidence)
    {
        var validation = LocalDecisionValidator.Validate(Payload(categoryConfidence: confidence), Model);

        validation.IsValid.Should().BeFalse();
        validation.Error.Should().Contain("category_confidence").And.Contain("outside the range");
    }

    [Fact]
    public void Validate_RejectsANullConfidence()
    {
        var validation = LocalDecisionValidator.Validate(
            Payload().Replace("\"category_confidence\": 0.91", "\"category_confidence\": null", StringComparison.Ordinal),
            Model);

        validation.IsValid.Should().BeFalse();
    }

    [Fact]
    public void Validate_AcceptsTheBoundaryConfidences()
    {
        LocalDecisionValidator.Validate(Payload(categoryConfidence: 0), Model).IsValid.Should().BeTrue();
        LocalDecisionValidator.Validate(Payload(categoryConfidence: 1), Model).IsValid.Should().BeTrue();
    }

    [Fact]
    public void Validate_IsCaseInsensitiveAboutEnumNames()
    {
        var validation = LocalDecisionValidator.Validate(
            Payload(category: "accessrequest", team: "identityaccess", priority: "medium"),
            Model);

        validation.IsValid.Should().BeTrue();
        validation.Result!.Category.Should().Be(TicketCategory.AccessRequest);
    }

    [Fact]
    public void Validate_ReportsAnUnknownModelNameRatherThanBlank()
    {
        LocalDecisionValidator.Validate(Payload(), "  ").Result!.Model.Should().Be("unknown");
    }

    [Fact]
    public void Validate_ErrorsNeverContainTicketText()
    {
        // The reply is only ever described structurally, so a failure cannot leak ticket content
        // into a log line.
        const string secret = "national id 0012345678";
        var validation = LocalDecisionValidator.Validate($"I cannot classify: {secret}", Model);

        validation.IsValid.Should().BeFalse();
        validation.Error.Should().NotContain(secret);
    }

    private static string Payload(
        string category = "AccessRequest",
        string team = "IdentityAccess",
        string priority = "Medium",
        double categoryConfidence = 0.91) =>
        $$"""
        {
          "category": "{{category}}",
          "category_confidence": {{categoryConfidence.ToString(System.Globalization.CultureInfo.InvariantCulture)}},
          "target_team": "{{team}}",
          "target_team_confidence": 0.88,
          "priority": "{{priority}}",
          "priority_confidence": 0.84,
          "contains_sensitive_data_probability": 0.02,
          "needs_human_review_probability": 0.11
        }
        """;

    /// <summary>Builds a payload with one field removed, to prove nothing is defaulted.</summary>
    private static string PayloadWithout(string field)
    {
        var lines = Payload()
            .Split('\n')
            .Where(line => !line.TrimStart().StartsWith($"\"{field}\"", StringComparison.Ordinal))
            .ToList();

        // Drop a trailing comma left by removing the last property.
        for (var i = lines.Count - 2; i >= 0; i--)
        {
            if (lines[i + 1].Trim() == "}")
            {
                lines[i] = lines[i].TrimEnd().TrimEnd(',');
                break;
            }
        }

        return string.Join('\n', lines);
    }
}
