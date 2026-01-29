using System.Net;
using System.Text.Json;

using Altinn.Authorization.ProblemDetails;

namespace Altinn.Notifications.Middleware;

/// <summary>
/// Middleware for handling validation exceptions thrown by the ValidationErrorBuilder.
/// Converts validation exceptions to proper HTTP 400 responses with AltinnProblemDetails format.
/// </summary>
public class ValidationExceptionMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<ValidationExceptionMiddleware> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="ValidationExceptionMiddleware"/> class.
    /// </summary>
    public ValidationExceptionMiddleware(RequestDelegate next, ILogger<ValidationExceptionMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    /// <summary>
    /// Processes the HTTP request and catches validation exceptions.
    /// </summary>
    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            await _next(context);
        }
        catch (ProblemInstanceException ex)
        {
            _logger.LogInformation(ex, "Validation error occurred for request {Path}", context.Request.Path);
            await HandleValidationExceptionAsync(context, ex);
        }
    }

    private static async Task HandleValidationExceptionAsync(HttpContext context, ProblemInstanceException exception)
    {
        var problemDetails = exception.Problem.ToProblemDetails();

        context.Response.StatusCode = problemDetails.Status ?? (int)HttpStatusCode.BadRequest;
        context.Response.ContentType = "application/problem+json";

        var jsonOptions = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull,
            WriteIndented = true
        };

        await context.Response.WriteAsJsonAsync(problemDetails, jsonOptions);
    }
}

/// <summary>
/// Extension methods for registering the validation exception middleware.
/// </summary>
public static class ValidationExceptionMiddlewareExtensions
{
    /// <summary>
    /// Registers the <see cref="ValidationExceptionMiddleware"/> in the application pipeline.
    /// </summary>
    public static IApplicationBuilder UseValidationExceptionHandler(this IApplicationBuilder builder)
    {
        return builder.UseMiddleware<ValidationExceptionMiddleware>();
    }
}
