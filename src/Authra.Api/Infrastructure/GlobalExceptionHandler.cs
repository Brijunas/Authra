using System.Diagnostics;
using Authra.Domain.Exceptions;
using FluentValidation;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;

namespace Authra.Api.Infrastructure;

/// <summary>
/// Global exception handler implementing RFC 9457 Problem Details.
/// </summary>
public class GlobalExceptionHandler : IExceptionHandler
{
    private readonly ILogger<GlobalExceptionHandler> _logger;
    private readonly IHostEnvironment _environment;

    public GlobalExceptionHandler(ILogger<GlobalExceptionHandler> logger, IHostEnvironment environment)
    {
        _logger = logger;
        _environment = environment;
    }

    public async ValueTask<bool> TryHandleAsync(
        HttpContext httpContext,
        Exception exception,
        CancellationToken cancellationToken)
    {
        var traceId = Activity.Current?.Id ?? httpContext.TraceIdentifier;

        var problemDetails = exception switch
        {
            // Domain exceptions
            DomainException domainException => CreateProblemDetails(
                httpContext,
                domainException.StatusCode,
                GetTitle(domainException),
                domainException.Message,
                traceId,
                domainException is Domain.Exceptions.ValidationException validationEx ? validationEx.Errors : null),

            // FluentValidation exceptions
            FluentValidation.ValidationException validationException => CreateProblemDetails(
                httpContext,
                StatusCodes.Status400BadRequest,
                "Validation Failed",
                "One or more validation errors occurred.",
                traceId,
                validationException.Errors
                    .GroupBy(e => e.PropertyName)
                    .ToDictionary(
                        g => ToCamelCase(g.Key),
                        g => g.Select(e => e.ErrorMessage).ToArray())),

            // All other exceptions
            _ => CreateProblemDetails(
                httpContext,
                StatusCodes.Status500InternalServerError,
                "Internal Server Error",
                _environment.IsDevelopment() ? exception.Message : "An unexpected error occurred.",
                traceId)
        };

        // Log the exception
        LogException(exception, problemDetails.Status ?? 500);

        httpContext.Response.StatusCode = problemDetails.Status ?? StatusCodes.Status500InternalServerError;
        httpContext.Response.ContentType = "application/problem+json";

        await httpContext.Response.WriteAsJsonAsync(problemDetails, cancellationToken);
        return true;
    }

    private static ProblemDetails CreateProblemDetails(
        HttpContext httpContext,
        int statusCode,
        string title,
        string detail,
        string traceId,
        IDictionary<string, string[]>? errors = null)
    {
        var problemDetails = new ProblemDetails
        {
            Type = GetProblemType(statusCode),
            Title = title,
            Status = statusCode,
            Detail = detail,
            Instance = httpContext.Request.Path
        };

        problemDetails.Extensions["traceId"] = $"req_{traceId.Replace("-", "")[..16]}";

        if (errors != null && errors.Count > 0)
        {
            problemDetails.Extensions["errors"] = errors;
        }

        return problemDetails;
    }

    private static string GetProblemType(int statusCode)
    {
        return statusCode switch
        {
            400 => "https://authra.io/errors/validation",
            401 => "https://authra.io/errors/unauthorized",
            403 => "https://authra.io/errors/forbidden",
            404 => "https://authra.io/errors/not-found",
            409 => "https://authra.io/errors/conflict",
            429 => "https://authra.io/errors/rate-limit",
            _ => "https://authra.io/errors/internal"
        };
    }

    private static string GetTitle(DomainException exception)
    {
        return exception switch
        {
            AuthenticationException => "Authentication Failed",
            UnauthorizedException => "Unauthorized",
            ForbiddenException => "Forbidden",
            NotFoundException => "Not Found",
            ConflictException => "Conflict",
            Domain.Exceptions.ValidationException => "Validation Failed",
            _ => "Error"
        };
    }

    private void LogException(Exception exception, int statusCode)
    {
        if (statusCode >= 500)
        {
            _logger.LogError(exception, "Unhandled exception: {Message}", exception.Message);
        }
        else if (statusCode >= 400)
        {
            _logger.LogWarning("Client error: {ExceptionType} - {Message}",
                exception.GetType().Name,
                exception.Message);
        }
    }

    private static string ToCamelCase(string value)
    {
        if (string.IsNullOrEmpty(value))
            return value;

        return char.ToLowerInvariant(value[0]) + value[1..];
    }
}
