using System;

namespace SmartSchool.Website.Api.Data.Entities;

/// <summary>
/// Represents a prospective school contact/demonstration form submission.
/// </summary>
public class ContactSubmission
{
    public Guid Id { get; set; }

    public string Name { get; set; } = string.Empty;

    public string SchoolName { get; set; } = string.Empty;

    public string PhoneNumber { get; set; } = string.Empty;

    public string WorkEmail { get; set; } = string.Empty;

    public string? Message { get; set; }

    public DateTime CreatedAtUtc { get; set; }
}
