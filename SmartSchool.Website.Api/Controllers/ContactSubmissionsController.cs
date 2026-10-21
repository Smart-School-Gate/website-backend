using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using FluentValidation;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.Extensions.Logging;
using SmartSchool.Website.Api.Data;
using SmartSchool.Website.Api.Data.Entities;
using SmartSchool.Website.Api.DTOs;

namespace SmartSchool.Website.Api.Controllers;

[ApiController]
[Route("api/v1/contact-submissions")]
public class ContactSubmissionsController : ControllerBase
{
    private readonly ApplicationDbContext _dbContext;
    private readonly IValidator<CreateContactSubmissionRequest> _validator;
    private readonly ILogger<ContactSubmissionsController> _logger;

    public ContactSubmissionsController(
        ApplicationDbContext dbContext,
        IValidator<CreateContactSubmissionRequest> validator,
        ILogger<ContactSubmissionsController> logger)
    {
        _dbContext = dbContext;
        _validator = validator;
        _logger = logger;
    }

    /// <summary>
    /// Submits a prospective school contact or demonstration request.
    /// </summary>
    /// <param name="request">The contact form submission payload.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Confirmation details including the assigned submission ID.</returns>
    [HttpPost]
    [EnableRateLimiting("ContactSubmissionLimit")]
    [RequestSizeLimit(32 * 1024)] // Max 32 KB request body limit
    [ProducesResponseType(typeof(ContactSubmissionResponse), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status429TooManyRequests)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> Submit(
        [FromBody] CreateContactSubmissionRequest? request,
        CancellationToken cancellationToken)
    {
        if (request is null)
        {
            return BadRequest(new ProblemDetails
            {
                Status = StatusCodes.Status400BadRequest,
                Title = "Invalid payload",
                Detail = "A valid JSON request body is required."
            });
        }

        var validationResult = await _validator.ValidateAsync(request, cancellationToken);
        if (!validationResult.IsValid)
        {
            var errors = validationResult.Errors
                .GroupBy(e => e.PropertyName)
                .ToDictionary(
                    g => g.Key,
                    g => g.Select(e => e.ErrorMessage).ToArray()
                );

            var problemDetails = new ValidationProblemDetails(errors)
            {
                Type = "https://tools.ietf.org/html/rfc9110#section-15.5.1",
                Title = "One or more validation errors occurred.",
                Status = StatusCodes.Status400BadRequest,
                Instance = HttpContext.Request.Path
            };

            return BadRequest(problemDetails);
        }

        // Generate server-controlled ID and timestamp
        var submissionId = Guid.NewGuid();
        var createdAtUtc = DateTime.UtcNow;

        var entity = new ContactSubmission
        {
            Id = submissionId,
            Name = request.Name!.Trim(),
            SchoolName = request.SchoolName!.Trim(),
            PhoneNumber = request.PhoneNumber!.Trim(),
            WorkEmail = request.WorkEmail!.Trim().ToLowerInvariant(),
            Message = string.IsNullOrWhiteSpace(request.Message) ? null : request.Message.Trim(),
            CreatedAtUtc = createdAtUtc
        };

        _dbContext.ContactSubmissions.Add(entity);
        await _dbContext.SaveChangesAsync(cancellationToken);

        // Security rule: Do NOT log the message content or sensitive personal information
        _logger.LogInformation(
            "Contact submission {SubmissionId} received and persisted for school {SchoolName}",
            submissionId,
            entity.SchoolName);

        var response = new ContactSubmissionResponse
        {
            Id = submissionId,
            Message = "Your request has been submitted successfully."
        };

        return StatusCode(StatusCodes.Status201Created, response);
    }
}
