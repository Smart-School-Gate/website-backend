using System;

namespace SmartSchool.Website.Api.DTOs;

/// <summary>
/// Confirmation response returned after a successful contact form submission.
/// </summary>
public class ContactSubmissionResponse
{
    public Guid Id { get; init; }

    public string Message { get; init; } = "Your request has been submitted successfully.";
}
