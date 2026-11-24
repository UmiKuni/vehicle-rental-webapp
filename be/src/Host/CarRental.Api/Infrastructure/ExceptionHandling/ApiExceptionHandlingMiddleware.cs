using System.Text.Json;
using Microsoft.AspNetCore.WebUtilities;

namespace CarRental.Api.Infrastructure.ExceptionHandling;

internal sealed class ApiExceptionHandlingMiddleware
{
    private const string ProblemJsonContentType = "application/problem+json";

    private readonly RequestDelegate _next;
    private readonly ILogger<ApiExceptionHandlingMiddleware> _logger;
    private readonly IHostEnvironment _environment;

    public ApiExceptionHandlingMiddleware(
        RequestDelegate next,
        ILogger<ApiExceptionHandlingMiddleware> logger,
        IHostEnvironment environment)
    {
        _next = next;
        _logger = logger;
        _environment = environment;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            await _next(context);
        }
        catch (OperationCanceledException) when (context.RequestAborted.IsCancellationRequested)
        {
            return;
        }
        catch (BadHttpRequestException ex)
        {
            if (context.Response.HasStarted)
                throw;

            await HandleBadHttpRequestAsync(context, ex);
        }
        catch (UnauthorizedAccessException ex)
        {
            if (context.Response.HasStarted)
                throw;

            await HandleUnauthorizedAsync(context, ex);
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "Unhandled exception processing {Method} {Path}",
                context.Request.Method,
                context.Request.Path);

            if (context.Response.HasStarted)
                throw;

            await WriteProblemResponseAsync(
                context,
                StatusCodes.Status500InternalServerError,
                new
                {
                    type = "about:blank",
                    title = "Internal Server Error",
                    status = StatusCodes.Status500InternalServerError,
                    errorCode = "server.unhandled_error"
                });
        }
    }

    private Task HandleBadHttpRequestAsync(HttpContext context, BadHttpRequestException ex)
    {
        var statusCode = ex.StatusCode is >= 400 and < 500
            ? ex.StatusCode
            : StatusCodes.Status400BadRequest;

        _logger.LogWarning(
            "Bad HTTP request processing {Method} {Path} ({StatusCode}): {Message}",
            context.Request.Method,
            context.Request.Path,
            statusCode,
            ex.Message);

        var reasonPhrase = ReasonPhrases.GetReasonPhrase(statusCode);
        var title = string.IsNullOrWhiteSpace(reasonPhrase) ? "Bad Request" : reasonPhrase;
        var (errorCode, detail) = ClassifyBadRequest(ex, statusCode);

        return WriteProblemResponseAsync(
            context,
            statusCode,
            new
            {
                type = "about:blank",
                title,
                status = statusCode,
                detail,
                errorCode
            });
    }

    private Task HandleUnauthorizedAsync(HttpContext context, UnauthorizedAccessException ex)
    {
        _logger.LogWarning(
            ex,
            "Unauthorized access processing {Method} {Path}",
            context.Request.Method,
            context.Request.Path);

        return WriteProblemResponseAsync(
            context,
            StatusCodes.Status401Unauthorized,
            new
            {
                type = "about:blank",
                title = "Unauthorized",
                status = StatusCodes.Status401Unauthorized,
                detail = "Authentication is required or invalid.",
                errorCode = "auth.unauthorized"
            });
    }

    private (string ErrorCode, string Detail) ClassifyBadRequest(BadHttpRequestException ex, int statusCode)
    {
        if (statusCode == StatusCodes.Status415UnsupportedMediaType)
        {
            return ("request.unsupported_media_type", "Unsupported Content-Type. Expected application/json.");
        }

        if (ex.InnerException is JsonException jsonEx)
        {
            var detail = _environment.IsDevelopment()
                ? $"Invalid JSON payload: {jsonEx.Message}"
                : "The request body contains invalid JSON.";
            return ("validation.invalid_json", detail);
        }

        if (ex.Message.Contains("no body was provided", StringComparison.OrdinalIgnoreCase) ||
            ex.Message.Contains("was not provided from body", StringComparison.OrdinalIgnoreCase))
        {
            return ("request.missing_body", "A valid JSON request body is required.");
        }

        var fallbackDetail = _environment.IsDevelopment()
            ? ex.Message
            : "The request is invalid or malformed.";
        return ("request.bad_request", fallbackDetail);
    }

    private static Task WriteProblemResponseAsync(
        HttpContext context,
        int statusCode,
        object payload)
    {
        context.Response.StatusCode = statusCode;
        return context.Response.WriteAsJsonAsync(
            payload,
            options: null,
            contentType: ProblemJsonContentType,
            cancellationToken: context.RequestAborted);
    }
}

