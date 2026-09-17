using FluentAssertions;
using JevTicketRouter.Application.Tickets.Dtos;
using JevTicketRouter.Application.Tickets.Validation;
using JevTicketRouter.Domain.Tickets;

namespace JevTicketRouter.Tests.Application;

/// <summary>Covers request validation and the friendliness of the messages it produces.</summary>
public sealed class TriageTicketRequestValidatorTests
{
    private readonly TriageTicketRequestValidator _validator = new();

    [Fact]
    public void Validate_WithAWellFormedEnglishTicket_Passes()
    {
        var request = new TriageTicketRequest(
            "Access to the reporting portal",
            "A new analyst needs read-only access to the quarterly reporting portal.",
            RequesterRole.InternalSupport);

        _validator.Validate(request).IsValid.Should().BeTrue();
    }

    [Fact]
    public void Validate_WithAWellFormedPersianTicket_Passes()
    {
        var request = new TriageTicketRequest(
            "خطا در سامانه شعبه",
            "هنگام ثبت تراکنش در سامانه شعبه با خطا مواجه می‌شوم و صفحه لود نمی‌شود.",
            RequesterRole.BranchEmployee);

        _validator.Validate(request).IsValid.Should().BeTrue();
    }

    [Theory]
    [InlineData("")]
    [InlineData("ab")]
    public void Validate_WithTooShortTitle_Fails(string title)
    {
        var request = new TriageTicketRequest(title, new string('x', 50), RequesterRole.Customer);

        var result = _validator.Validate(request);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(error => error.PropertyName == nameof(TriageTicketRequest.Title));
    }

    [Fact]
    public void Validate_WithWhitespaceOnlyTitle_Fails()
    {
        var request = new TriageTicketRequest("     ", new string('x', 50), RequesterRole.Customer);

        var result = _validator.Validate(request);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(error => error.ErrorMessage.Contains("whitespace"));
    }

    [Fact]
    public void Validate_WithOverlongTitle_Fails()
    {
        var request = new TriageTicketRequest(
            new string('x', TriageTicketRequestValidator.TitleMaxLength + 1),
            new string('y', 50),
            RequesterRole.Customer);

        _validator.Validate(request).IsValid.Should().BeFalse();
    }

    [Theory]
    [InlineData("")]
    [InlineData("too short")]
    public void Validate_WithTooShortDescription_Fails(string description)
    {
        var request = new TriageTicketRequest("A valid title", description, RequesterRole.Customer);

        var result = _validator.Validate(request);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(error => error.PropertyName == nameof(TriageTicketRequest.Description));
    }

    [Fact]
    public void Validate_WithOverlongDescription_Fails()
    {
        var request = new TriageTicketRequest(
            "A valid title",
            new string('x', TriageTicketRequestValidator.DescriptionMaxLength + 1),
            RequesterRole.Customer);

        _validator.Validate(request).IsValid.Should().BeFalse();
    }

    [Fact]
    public void Validate_WithUndefinedRole_Fails()
    {
        var request = new TriageTicketRequest("A valid title", new string('x', 50), (RequesterRole)99);

        var result = _validator.Validate(request);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(error => error.PropertyName == nameof(TriageTicketRequest.RequesterRole));
    }

    [Fact]
    public void Validate_WithEveryFieldInvalid_ReportsEveryProblemAtOnce()
    {
        var request = new TriageTicketRequest("a", "b", (RequesterRole)42);

        var result = _validator.Validate(request);

        result.Errors.Select(error => error.PropertyName).Distinct()
            .Should().BeEquivalentTo(
                nameof(TriageTicketRequest.Title),
                nameof(TriageTicketRequest.Description),
                nameof(TriageTicketRequest.RequesterRole));
    }

    [Fact]
    public void Validate_ProducesMessagesWithoutTechnicalJargon()
    {
        var request = new TriageTicketRequest("a", "b", RequesterRole.Customer);

        var messages = _validator.Validate(request).Errors.Select(error => error.ErrorMessage);

        messages.Should().OnlyContain(message => message.EndsWith('.') && !message.Contains("_"));
    }
}
