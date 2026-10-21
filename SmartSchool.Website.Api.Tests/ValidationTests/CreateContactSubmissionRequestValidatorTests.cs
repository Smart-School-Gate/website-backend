using FluentValidation.TestHelper;
using SmartSchool.Website.Api.DTOs;
using SmartSchool.Website.Api.Validation;
using Xunit;

namespace SmartSchool.Website.Api.Tests.ValidationTests;

public class CreateContactSubmissionRequestValidatorTests
{
    private readonly CreateContactSubmissionRequestValidator _validator = new();

    private static CreateContactSubmissionRequest CreateValidRequest() => new()
    {
        Name = "Ahmed Mohamed",
        SchoolName = "Smart International School",
        PhoneNumber = "+966500000000",
        WorkEmail = "contact@example.com",
        Message = "We are interested in a product demonstration."
    };

    [Fact]
    public void Validate_WhenPayloadIsValid_ShouldHaveNoErrors()
    {
        var request = CreateValidRequest();
        var result = _validator.TestValidate(request);
        result.ShouldNotHaveAnyValidationErrors();
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Validate_WhenNameIsNullOrEmptyOrWhitespace_ShouldHaveError(string? name)
    {
        var request = CreateValidRequest();
        request.Name = name;

        var result = _validator.TestValidate(request);
        result.ShouldHaveValidationErrorFor(x => x.Name);
    }

    [Fact]
    public void Validate_WhenNameExceeds150Chars_ShouldHaveError()
    {
        var request = CreateValidRequest();
        request.Name = new string('A', 151);

        var result = _validator.TestValidate(request);
        result.ShouldHaveValidationErrorFor(x => x.Name)
            .WithErrorMessage("Your Name cannot exceed 150 characters.");
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Validate_WhenSchoolNameIsNullOrEmptyOrWhitespace_ShouldHaveError(string? schoolName)
    {
        var request = CreateValidRequest();
        request.SchoolName = schoolName;

        var result = _validator.TestValidate(request);
        result.ShouldHaveValidationErrorFor(x => x.SchoolName);
    }

    [Fact]
    public void Validate_WhenSchoolNameExceeds200Chars_ShouldHaveError()
    {
        var request = CreateValidRequest();
        request.SchoolName = new string('S', 201);

        var result = _validator.TestValidate(request);
        result.ShouldHaveValidationErrorFor(x => x.SchoolName)
            .WithErrorMessage("School Name cannot exceed 200 characters.");
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Validate_WhenPhoneNumberIsNullOrEmptyOrWhitespace_ShouldHaveError(string? phone)
    {
        var request = CreateValidRequest();
        request.PhoneNumber = phone;

        var result = _validator.TestValidate(request);
        result.ShouldHaveValidationErrorFor(x => x.PhoneNumber);
    }

    [Theory]
    [InlineData("123")] // Too short
    [InlineData("invalid_phone!#$")] // Invalid characters
    [InlineData("phone number")] // Non-digits
    public void Validate_WhenPhoneNumberHasInvalidFormat_ShouldHaveError(string phone)
    {
        var request = CreateValidRequest();
        request.PhoneNumber = phone;

        var result = _validator.TestValidate(request);
        result.ShouldHaveValidationErrorFor(x => x.PhoneNumber)
            .WithErrorMessage("Please enter a valid phone number.");
    }

    [Fact]
    public void Validate_WhenPhoneNumberExceeds30Chars_ShouldHaveError()
    {
        var request = CreateValidRequest();
        request.PhoneNumber = "+1234567890123456789012345678901"; // 31 chars

        var result = _validator.TestValidate(request);
        result.ShouldHaveValidationErrorFor(x => x.PhoneNumber);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Validate_WhenWorkEmailIsNullOrEmptyOrWhitespace_ShouldHaveError(string? email)
    {
        var request = CreateValidRequest();
        request.WorkEmail = email;

        var result = _validator.TestValidate(request);
        result.ShouldHaveValidationErrorFor(x => x.WorkEmail);
    }

    [Theory]
    [InlineData("not-an-email")]
    [InlineData("missingat.com")]
    [InlineData("user@nodomain")]
    [InlineData("@missinguser.com")]
    [InlineData("user@domain.")]
    public void Validate_WhenWorkEmailHasInvalidSyntax_ShouldHaveError(string email)
    {
        var request = CreateValidRequest();
        request.WorkEmail = email;

        var result = _validator.TestValidate(request);
        result.ShouldHaveValidationErrorFor(x => x.WorkEmail)
            .WithErrorMessage("Please enter a valid work email.");
    }

    [Fact]
    public void Validate_WhenWorkEmailExceeds254Chars_ShouldHaveError()
    {
        var request = CreateValidRequest();
        // 245 'a's + "@domain.com" (11 chars) = 256 chars
        request.WorkEmail = $"{new string('a', 245)}@domain.com";

        var result = _validator.TestValidate(request);
        result.ShouldHaveValidationErrorFor(x => x.WorkEmail)
            .WithErrorMessage("Work Email cannot exceed 254 characters.");
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Validate_WhenMessageIsOptionalOrOmitted_ShouldBeValid(string? message)
    {
        var request = CreateValidRequest();
        request.Message = message;

        var result = _validator.TestValidate(request);
        result.ShouldNotHaveValidationErrorFor(x => x.Message);
    }

    [Fact]
    public void Validate_WhenMessageExceeds2000Chars_ShouldHaveError()
    {
        var request = CreateValidRequest();
        request.Message = new string('M', 2001);

        var result = _validator.TestValidate(request);
        result.ShouldHaveValidationErrorFor(x => x.Message)
            .WithErrorMessage("Message cannot exceed 2000 characters.");
    }
}
