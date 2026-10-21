using System.Text.RegularExpressions;
using FluentValidation;
using SmartSchool.Website.Api.DTOs;

namespace SmartSchool.Website.Api.Validation;

public class CreateContactSubmissionRequestValidator : AbstractValidator<CreateContactSubmissionRequest>
{
    // E.164-compatible and international school phone formats (allows digits, spaces, hyphens, plus, parenthesis)
    private static readonly Regex PhoneRegex = new(
        @"^[\d\s\+\-\(\)\.]{7,30}$",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);

    // RFC 5322 simplified email regex with standard TLD requirements
    private static readonly Regex EmailRegex = new(
        @"^[a-zA-Z0-9._%+-]+@[a-zA-Z0-9.-]+\.[a-zA-Z]{2,}$",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);

    public CreateContactSubmissionRequestValidator()
    {
        RuleFor(x => x.Name)
            .Cascade(CascadeMode.Stop)
            .NotEmpty().WithMessage("Your Name is required.")
            .Must(name => !string.IsNullOrWhiteSpace(name)).WithMessage("Your Name cannot be blank.")
            .MaximumLength(150).WithMessage("Your Name cannot exceed 150 characters.");

        RuleFor(x => x.SchoolName)
            .Cascade(CascadeMode.Stop)
            .NotEmpty().WithMessage("School Name is required.")
            .Must(s => !string.IsNullOrWhiteSpace(s)).WithMessage("School Name cannot be blank.")
            .MaximumLength(200).WithMessage("School Name cannot exceed 200 characters.");

        RuleFor(x => x.PhoneNumber)
            .Cascade(CascadeMode.Stop)
            .NotEmpty().WithMessage("Phone Number is required.")
            .Must(p => !string.IsNullOrWhiteSpace(p)).WithMessage("Phone Number cannot be blank.")
            .MaximumLength(30).WithMessage("Phone Number cannot exceed 30 characters.")
            .Matches(PhoneRegex).WithMessage("Please enter a valid phone number.");

        RuleFor(x => x.WorkEmail)
            .Cascade(CascadeMode.Stop)
            .NotEmpty().WithMessage("Work Email is required.")
            .Must(e => !string.IsNullOrWhiteSpace(e)).WithMessage("Work Email cannot be blank.")
            .MaximumLength(254).WithMessage("Work Email cannot exceed 254 characters.")
            .Matches(EmailRegex).WithMessage("Please enter a valid work email.");

        RuleFor(x => x.Message)
            .MaximumLength(2000).WithMessage("Message cannot exceed 2000 characters.")
            .When(x => !string.IsNullOrEmpty(x.Message));
    }
}
