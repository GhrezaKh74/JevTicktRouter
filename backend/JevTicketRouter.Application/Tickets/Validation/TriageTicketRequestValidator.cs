using FluentValidation;
using JevTicketRouter.Application.Tickets.Dtos;

namespace JevTicketRouter.Application.Tickets.Validation;

/// <summary>
/// Validates an incoming triage request. Limits are generous enough for a real ticket in either
/// Persian or English, and tight enough to keep a single request from dominating a Jev call.
/// </summary>
public sealed class TriageTicketRequestValidator : AbstractValidator<TriageTicketRequest>
{
    /// <summary>Minimum title length.</summary>
    public const int TitleMinLength = 3;

    /// <summary>Maximum title length.</summary>
    public const int TitleMaxLength = 200;

    /// <summary>Minimum description length.</summary>
    public const int DescriptionMinLength = 10;

    /// <summary>Maximum description length.</summary>
    public const int DescriptionMaxLength = 5000;

    /// <summary>Creates the validator.</summary>
    public TriageTicketRequestValidator()
    {
        RuleFor(request => request.Title)
            .NotEmpty().WithMessage("Title is required.")
            .Must(title => !string.IsNullOrWhiteSpace(title))
                .WithMessage("Title cannot be only whitespace.")
            .MinimumLength(TitleMinLength)
                .WithMessage($"Title must be at least {TitleMinLength} characters.")
            .MaximumLength(TitleMaxLength)
                .WithMessage($"Title must be at most {TitleMaxLength} characters.");

        RuleFor(request => request.Description)
            .NotEmpty().WithMessage("Description is required.")
            .Must(description => !string.IsNullOrWhiteSpace(description))
                .WithMessage("Description cannot be only whitespace.")
            .MinimumLength(DescriptionMinLength)
                .WithMessage($"Description must be at least {DescriptionMinLength} characters.")
            .MaximumLength(DescriptionMaxLength)
                .WithMessage($"Description must be at most {DescriptionMaxLength} characters.");

        RuleFor(request => request.RequesterRole)
            .IsInEnum().WithMessage("RequesterRole must be BranchEmployee, Customer, or InternalSupport.");
    }
}
