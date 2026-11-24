using System.Globalization;
using System.Security.Claims;
using Microsoft.Extensions.Options;

namespace CarRental.Api.Infrastructure.RateLimiting;

internal sealed class RateLimitingMiddleware
{
    private readonly RequestDelegate _next;
    private readonly IRateLimiter _rateLimiter;
    private readonly IOptionsMonitor<RateLimitingOptions> _options;

    public RateLimitingMiddleware(
        RequestDelegate next,
        IRateLimiter rateLimiter,
        IOptionsMonitor<RateLimitingOptions> options)
    {
        _next = next;
        _rateLimiter = rateLimiter;
        _options = options;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        var opts = _options.CurrentValue;
        if (!opts.Enabled || !HttpMethods.IsPost(context.Request.Method))
        {
            await _next(context);
            return;
        }

        var path = (context.Request.Path.Value ?? string.Empty).TrimEnd('/');
        var match = MatchPolicy(path, opts, context);
        if (match is null)
        {
            await _next(context);
            return;
        }

        var (policyName, policy, partitionKey) = match.Value;
        var window = TimeSpan.FromSeconds(Math.Max(1, policy.WindowSeconds));
        var decision = await _rateLimiter.CheckAsync(
            policyName,
            partitionKey,
            policy.PermitLimit,
            window,
            context.RequestAborted);

        context.Response.Headers["X-RateLimit-Limit"] = decision.Limit.ToString(CultureInfo.InvariantCulture);
        context.Response.Headers["X-RateLimit-Remaining"] = decision.Remaining.ToString(CultureInfo.InvariantCulture);

        if (!decision.IsAllowed)
        {
            context.Response.StatusCode = StatusCodes.Status429TooManyRequests;
            context.Response.Headers.RetryAfter = decision.RetryAfterSeconds.ToString(CultureInfo.InvariantCulture);
            context.Response.ContentType = "application/problem+json";
            await context.Response.WriteAsJsonAsync(new
            {
                type = "about:blank",
                title = "Too Many Requests",
                status = StatusCodes.Status429TooManyRequests,
                detail = "Rate limit exceeded. Please retry later.",
                errorCode = "rate_limit.exceeded"
            }, context.RequestAborted);
            return;
        }

        await _next(context);
    }

    private static (string PolicyName, RateLimitPolicyOptions Policy, string PartitionKey)? MatchPolicy(
        string path,
        RateLimitingOptions options,
        HttpContext context)
    {
        if (path.Equals("/api/users/login", StringComparison.OrdinalIgnoreCase))
        {
            return ("auth-login", options.AuthLogin, $"ip:{ResolveClientIp(context)}");
        }

        if (path.Equals("/api/users/register", StringComparison.OrdinalIgnoreCase))
        {
            return ("auth-register", options.AuthRegister, $"ip:{ResolveClientIp(context)}");
        }

        if (path.Equals("/api/users/refresh", StringComparison.OrdinalIgnoreCase))
        {
            return ("auth-refresh", options.AuthRefresh, $"ip:{ResolveClientIp(context)}");
        }

        if (path.Equals("/api/bookings", StringComparison.OrdinalIgnoreCase))
        {
            return ("bookings-create", options.BookingsCreate, ResolveUserOrIpPartition(context));
        }

        if (path.Equals("/api/payments/checkout", StringComparison.OrdinalIgnoreCase))
        {
            return ("payments-checkout", options.PaymentsCheckout, ResolveUserOrIpPartition(context));
        }

        return null;
    }

    private static string ResolveUserOrIpPartition(HttpContext context)
    {
        var userId = context.User.FindFirstValue(ClaimTypes.NameIdentifier);
        return !string.IsNullOrWhiteSpace(userId)
            ? $"user:{userId}"
            : $"ip:{ResolveClientIp(context)}";
    }

    private static string ResolveClientIp(HttpContext context)
    {
        if (context.Request.Headers.TryGetValue("X-Forwarded-For", out var forwarded))
        {
            var first = forwarded.ToString().Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).FirstOrDefault();
            if (!string.IsNullOrWhiteSpace(first))
                return first;
        }

        return context.Connection.RemoteIpAddress?.ToString() ?? "unknown";
    }
}
