using System.Text.Json;
using CarRental.Api.Infrastructure.ExceptionHandling;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging.Abstractions;

namespace CarRental.IntegrationTests.Infrastructure;

public class ApiExceptionHandlingMiddlewareTests
{
    [Fact]
    public async Task InvokeAsync_MissingRequestBodyBadHttpRequestException_Returns400MissingBodyProblemDetails()
    {
        var middleware = CreateMiddleware(
            _ => throw new BadHttpRequestException(
                "Implicit body inferred for parameter \"dto\" but no body was provided. Did you mean to use a Service instead?"),
            Environments.Development);

        var context = CreateHttpContext();

        await middleware.InvokeAsync(context);

        context.Response.StatusCode.Should().Be(StatusCodes.Status400BadRequest);
        context.Response.ContentType.Should().StartWith("application/problem+json");

        using var doc = await ReadResponseJsonAsync(context);
        doc.RootElement.GetProperty("status").GetInt32().Should().Be(400);
        doc.RootElement.GetProperty("errorCode").GetString().Should().Be("request.missing_body");
        doc.RootElement.GetProperty("detail").GetString().Should().Contain("JSON request body");
    }

    [Fact]
    public async Task InvokeAsync_InvalidJsonBadHttpRequestException_Returns400InvalidJsonProblemDetails()
    {
        var middleware = CreateMiddleware(
            _ => throw new BadHttpRequestException(
                "Failed to read parameter \"dto\" from the request body as JSON.",
                new JsonException("Expected '{' token")),
            Environments.Production);

        var context = CreateHttpContext();

        await middleware.InvokeAsync(context);

        context.Response.StatusCode.Should().Be(StatusCodes.Status400BadRequest);
        context.Response.ContentType.Should().StartWith("application/problem+json");

        using var doc = await ReadResponseJsonAsync(context);
        doc.RootElement.GetProperty("status").GetInt32().Should().Be(400);
        doc.RootElement.GetProperty("errorCode").GetString().Should().Be("validation.invalid_json");
        doc.RootElement.GetProperty("detail").GetString().Should().Be("The request body contains invalid JSON.");
    }

    [Fact]
    public async Task InvokeAsync_UnsupportedMediaTypeBadHttpRequestException_Returns415ProblemDetails()
    {
        var middleware = CreateMiddleware(
            _ => throw new BadHttpRequestException(
                "Expected a supported JSON media type.",
                StatusCodes.Status415UnsupportedMediaType),
            Environments.Development);

        var context = CreateHttpContext();

        await middleware.InvokeAsync(context);

        context.Response.StatusCode.Should().Be(StatusCodes.Status415UnsupportedMediaType);
        context.Response.ContentType.Should().StartWith("application/problem+json");

        using var doc = await ReadResponseJsonAsync(context);
        doc.RootElement.GetProperty("status").GetInt32().Should().Be(415);
        doc.RootElement.GetProperty("errorCode").GetString().Should().Be("request.unsupported_media_type");
    }

    [Fact]
    public async Task InvokeAsync_UnauthorizedAccessException_Returns401ProblemDetails()
    {
        var middleware = CreateMiddleware(
            _ => throw new UnauthorizedAccessException("The authenticated user identifier is invalid."),
            Environments.Production);

        var context = CreateHttpContext();

        await middleware.InvokeAsync(context);

        context.Response.StatusCode.Should().Be(StatusCodes.Status401Unauthorized);
        context.Response.ContentType.Should().StartWith("application/problem+json");

        using var doc = await ReadResponseJsonAsync(context);
        doc.RootElement.GetProperty("status").GetInt32().Should().Be(401);
        doc.RootElement.GetProperty("errorCode").GetString().Should().Be("auth.unauthorized");
    }

    [Fact]
    public async Task InvokeAsync_UnhandledException_Returns500UnhandledErrorProblemDetails()
    {
        var middleware = CreateMiddleware(
            _ => throw new InvalidOperationException("Database exploded."),
            Environments.Development);

        var context = CreateHttpContext();

        await middleware.InvokeAsync(context);

        context.Response.StatusCode.Should().Be(StatusCodes.Status500InternalServerError);
        context.Response.ContentType.Should().StartWith("application/problem+json");

        using var doc = await ReadResponseJsonAsync(context);
        doc.RootElement.GetProperty("status").GetInt32().Should().Be(500);
        doc.RootElement.GetProperty("title").GetString().Should().Be("Internal Server Error");
        doc.RootElement.GetProperty("errorCode").GetString().Should().Be("server.unhandled_error");
    }

    private static ApiExceptionHandlingMiddleware CreateMiddleware(
        RequestDelegate next,
        string environmentName) =>
        new(
            next,
            NullLogger<ApiExceptionHandlingMiddleware>.Instance,
            new FakeHostEnvironment(environmentName));

    private static DefaultHttpContext CreateHttpContext()
    {
        var context = new DefaultHttpContext();
        context.Request.Method = HttpMethods.Post;
        context.Request.Path = "/api/auth/login";
        context.Response.Body = new MemoryStream();
        return context;
    }

    private static async Task<JsonDocument> ReadResponseJsonAsync(HttpContext context)
    {
        context.Response.Body.Seek(0, SeekOrigin.Begin);
        return await JsonDocument.ParseAsync(context.Response.Body);
    }

    private sealed class FakeHostEnvironment(string environmentName) : IHostEnvironment
    {
        public string EnvironmentName { get; set; } = environmentName;
        public string ApplicationName { get; set; } = "CarRental.Api";
        public string ContentRootPath { get; set; } = AppContext.BaseDirectory;
        public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
    }
}

