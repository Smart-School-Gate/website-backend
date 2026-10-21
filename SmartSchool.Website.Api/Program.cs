using System;
using System.IO;
using System.Net;
using System.Reflection;
using System.Text.Json;
using System.Threading.RateLimiting;
using FluentValidation;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.OpenApi.Models;
using SmartSchool.Website.Api.Common;
using SmartSchool.Website.Api.Configuration;
using SmartSchool.Website.Api.Data;
using SmartSchool.Website.Api.Middleware;
using SmartSchool.Website.Api.Validation;

var builder = WebApplication.CreateBuilder(args);

// 1. Database Configuration
var rawConnectionString = builder.Configuration.GetConnectionString("DefaultConnection")
    ?? Environment.GetEnvironmentVariable("DATABASE_URL");

if (!string.IsNullOrWhiteSpace(rawConnectionString))
{
    var resolvedConnectionString = DatabaseConfigurationHelper.ResolveConnectionString(rawConnectionString);
    builder.Services.AddDbContext<ApplicationDbContext>(options =>
    {
        options.UseNpgsql(resolvedConnectionString, npgsqlOptions =>
        {
            npgsqlOptions.EnableRetryOnFailure(
                maxRetryCount: 3,
                maxRetryDelay: TimeSpan.FromSeconds(5),
                errorCodesToAdd: null);
        });
    });
}
else
{
    // Register DbContext with empty options for design-time / testing overrides
    builder.Services.AddDbContext<ApplicationDbContext>();
}

// 2. FluentValidation
builder.Services.AddValidatorsFromAssemblyContaining<CreateContactSubmissionRequestValidator>();

// 3. CORS Configuration
var corsSection = builder.Configuration.GetSection(CorsOptions.SectionName);
var corsOptions = corsSection.Get<CorsOptions>() ?? new CorsOptions();
var allowedOrigins = corsOptions.AllowedOrigins ?? Array.Empty<string>();

builder.Services.AddCors(options =>
{
    options.AddPolicy("WebsiteCorsPolicy", policy =>
    {
        if (allowedOrigins.Length > 0)
        {
            policy.WithOrigins(allowedOrigins)
                  .WithMethods("POST", "OPTIONS")
                  .WithHeaders("Content-Type", "Accept")
                  .SetPreflightMaxAge(TimeSpan.FromHours(1));
        }
        else
        {
            // Fallback for local development when no origins explicitly set
            policy.AllowAnyOrigin()
                  .WithMethods("POST", "OPTIONS")
                  .WithHeaders("Content-Type", "Accept");
        }
    });
});

// 4. Rate Limiting Configuration
builder.Services.Configure<RateLimitingOptions>(builder.Configuration.GetSection(RateLimitingOptions.SectionName));

builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;

    options.OnRejected = async (context, token) =>
    {
        context.HttpContext.Response.ContentType = "application/problem+json";
        var problemDetails = new ProblemDetails
        {
            Status = StatusCodes.Status429TooManyRequests,
            Title = "Too Many Requests",
            Detail = "Rate limit exceeded. Please wait before submitting another request.",
            Instance = context.HttpContext.Request.Path
        };
        await context.HttpContext.Response.WriteAsync(JsonSerializer.Serialize(problemDetails), token);
    };

    options.AddPolicy("ContactSubmissionLimit", httpContext =>
    {
        var rateLimitOptions = httpContext.RequestServices
            .GetRequiredService<Microsoft.Extensions.Options.IOptions<RateLimitingOptions>>().Value;

        // Extract client IP (respecting forward headers where applicable)
        var clientIp = httpContext.Request.Headers["X-Forwarded-For"].FirstOrDefault()
            ?? httpContext.Connection.RemoteIpAddress?.ToString()
            ?? "anonymous";

        return RateLimitPartition.GetFixedWindowLimiter(
            partitionKey: clientIp,
            factory: _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = rateLimitOptions.PermitLimit,
                Window = TimeSpan.FromSeconds(rateLimitOptions.WindowSeconds),
                QueueLimit = rateLimitOptions.QueueLimit,
                AutoReplenishment = true
            });
    });
});

// 5. Controllers with Suppressed Default Validation (handled explicitly by FluentValidation)
builder.Services.AddControllers()
    .ConfigureApiBehaviorOptions(options =>
    {
        options.SuppressModelStateInvalidFilter = true;
    });

// 6. Swagger / OpenAPI Documentation
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(options =>
{
    options.SwaggerDoc("v1", new OpenApiInfo
    {
        Title = "Smart School Website Contact API",
        Version = "v1",
        Description = "Minimal, dedicated backend API for smart-school website demo request & contact form submissions."
    });

    var xmlFile = $"{Assembly.GetExecutingAssembly().GetName().Name}.xml";
    var xmlPath = Path.Combine(AppContext.BaseDirectory, xmlFile);
    if (File.Exists(xmlPath))
    {
        options.IncludeXmlComments(xmlPath);
    }
});

var app = builder.Build();

// Pipeline Configuration
app.UseMiddleware<GlobalExceptionHandlingMiddleware>();

var swaggerEnabled = app.Environment.IsDevelopment() ||
                     app.Configuration.GetValue<bool>("Swagger:Enabled", false);

if (swaggerEnabled)
{
    app.UseSwagger();
    app.UseSwaggerUI(c =>
    {
        c.SwaggerEndpoint("/swagger/v1/swagger.json", "Smart School Website Contact API v1");
        c.RoutePrefix = "swagger";
    });
}

app.UseCors("WebsiteCorsPolicy");

app.UseRateLimiter();

app.MapControllers();

app.Run();

// Required for WebApplicationFactory in integration tests
public partial class Program { }
