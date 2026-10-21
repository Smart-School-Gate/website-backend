using System;
using System.Net;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;
using SmartSchool.Website.Api.Data;
using SmartSchool.Website.Api.DTOs;
using Xunit;

namespace SmartSchool.Website.Api.Tests.IntegrationTests;

public class ContactSubmissionsApiTests : IClassFixture<CustomWebApplicationFactory>
{
    private readonly CustomWebApplicationFactory _factory;
    private readonly HttpClient _client;

    public ContactSubmissionsApiTests(CustomWebApplicationFactory factory)
    {
        _factory = factory;
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task Post_WhenPayloadIsValid_Returns201CreatedAndPersists()
    {
        // Arrange
        var request = new CreateContactSubmissionRequest
        {
            Name = "Ahmed Mohamed",
            SchoolName = "Example International School",
            PhoneNumber = "+966500000000",
            WorkEmail = "contact@example.com",
            Message = "We would like to learn more about the platform."
        };

        // Act
        var response = await _client.PostAsJsonAsync("/api/v1/contact-submissions", request);

        // Assert
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);

        var result = await response.Content.ReadFromJsonAsync<ContactSubmissionResponse>();
        Assert.NotNull(result);
        Assert.NotEqual(Guid.Empty, result.Id);
        Assert.Equal("Your request has been submitted successfully.", result.Message);

        // Verify in database
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var persisted = await db.ContactSubmissions.FindAsync(result.Id);

        Assert.NotNull(persisted);
        Assert.Equal(request.Name, persisted.Name);
        Assert.Equal(request.SchoolName, persisted.SchoolName);
        Assert.Equal(request.PhoneNumber, persisted.PhoneNumber);
        Assert.Equal("contact@example.com", persisted.WorkEmail);
        Assert.Equal(request.Message, persisted.Message);
        Assert.True(persisted.CreatedAtUtc <= DateTime.UtcNow);
    }

    [Fact]
    public async Task Post_WhenMessageIsOmitted_Returns201Created()
    {
        // Arrange
        var request = new CreateContactSubmissionRequest
        {
            Name = "Sara Al-Otaibi",
            SchoolName = "Riyadh Excellence Academy",
            PhoneNumber = "+966512345678",
            WorkEmail = "sara@excellence.edu.sa",
            Message = null
        };

        // Act
        var response = await _client.PostAsJsonAsync("/api/v1/contact-submissions", request);

        // Assert
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);

        var result = await response.Content.ReadFromJsonAsync<ContactSubmissionResponse>();
        Assert.NotNull(result);

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var persisted = await db.ContactSubmissions.FindAsync(result.Id);

        Assert.NotNull(persisted);
        Assert.Null(persisted.Message);
    }

    [Fact]
    public async Task Post_WhenRequiredFieldsMissing_Returns400BadRequestWithStructuredValidationErrors()
    {
        // Arrange
        var invalidRequest = new CreateContactSubmissionRequest
        {
            Name = "",
            SchoolName = "   ",
            PhoneNumber = "invalid",
            WorkEmail = "not-an-email"
        };

        // Act
        var response = await _client.PostAsJsonAsync("/api/v1/contact-submissions", invalidRequest);

        // Assert
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);

        var problemDetails = await response.Content.ReadFromJsonAsync<ValidationProblemDetails>();
        Assert.NotNull(problemDetails);
        Assert.Equal(400, problemDetails.Status);
        Assert.NotNull(problemDetails.Errors);
        Assert.True(problemDetails.Errors.ContainsKey("Name"));
        Assert.True(problemDetails.Errors.ContainsKey("SchoolName"));
        Assert.True(problemDetails.Errors.ContainsKey("PhoneNumber"));
        Assert.True(problemDetails.Errors.ContainsKey("WorkEmail"));
    }

    [Fact]
    public async Task Post_WhenPayloadContainsClientSuppliedId_ServerIgnoresItAndGeneratesNewId()
    {
        // Arrange - Client sends arbitrary JSON attempting to set Id or CreatedAtUtc
        var jsonWithAttemptedSpoofing = """
        {
            "id": "11111111-1111-1111-1111-111111111111",
            "createdAtUtc": "2020-01-01T00:00:00Z",
            "name": "Legitimate User",
            "schoolName": "Security Test Academy",
            "phoneNumber": "+966599999999",
            "workEmail": "security@test.edu",
            "message": "Testing client-side parameter tampering"
        }
        """;

        using var content = new StringContent(jsonWithAttemptedSpoofing, System.Text.Encoding.UTF8, "application/json");

        // Act
        var response = await _client.PostAsync("/api/v1/contact-submissions", content);

        // Assert
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);

        var result = await response.Content.ReadFromJsonAsync<ContactSubmissionResponse>();
        Assert.NotNull(result);
        Assert.NotEqual(Guid.Parse("11111111-1111-1111-1111-111111111111"), result.Id);

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var persisted = await db.ContactSubmissions.FindAsync(result.Id);

        Assert.NotNull(persisted);
        Assert.True(persisted.CreatedAtUtc > DateTime.UtcNow.AddMinutes(-1));
    }

    [Fact]
    public async Task Post_WhenRateLimitExceeded_Returns429TooManyRequests()
    {
        // Arrange - Client with custom IP address to isolate rate limiting window
        var client = _factory.WithWebHostBuilder(builder =>
        {
            builder.ConfigureServices(services =>
            {
                services.Configure<SmartSchool.Website.Api.Configuration.RateLimitingOptions>(options =>
                {
                    options.PermitLimit = 2;
                    options.WindowSeconds = 60;
                });
            });
        }).CreateClient();

        client.DefaultRequestHeaders.Add("X-Forwarded-For", "203.0.113.195");

        var validPayload = new CreateContactSubmissionRequest
        {
            Name = "Rate Limit Tester",
            SchoolName = "Rate Limit Academy",
            PhoneNumber = "+966500000000",
            WorkEmail = "ratelimit@example.com",
            Message = "Testing rate limit"
        };

        // Act - Request 1 & 2 succeed (within limit 2)
        var resp1 = await client.PostAsJsonAsync("/api/v1/contact-submissions", validPayload);
        var resp2 = await client.PostAsJsonAsync("/api/v1/contact-submissions", validPayload);

        // Request 3 exceeds limit
        var resp3 = await client.PostAsJsonAsync("/api/v1/contact-submissions", validPayload);

        // Assert
        Assert.Equal(HttpStatusCode.Created, resp1.StatusCode);
        Assert.Equal(HttpStatusCode.Created, resp2.StatusCode);
        Assert.Equal(HttpStatusCode.TooManyRequests, resp3.StatusCode);

        var problemDetails = await resp3.Content.ReadFromJsonAsync<ProblemDetails>();
        Assert.NotNull(problemDetails);
        Assert.Equal(429, problemDetails.Status);
        Assert.Equal("Too Many Requests", problemDetails.Title);
    }
}
