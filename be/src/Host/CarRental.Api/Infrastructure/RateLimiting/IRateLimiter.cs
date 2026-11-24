namespace CarRental.Api.Infrastructure.RateLimiting;

internal sealed record RateLimitDecision(
    bool IsAllowed,
    int Limit,
    int Remaining,
    int RetryAfterSeconds);

internal interface IRateLimiter
{
    Task<RateLimitDecision> CheckAsync(
        string policyName,
        string partitionKey,
        int permitLimit,
        TimeSpan window,
        CancellationToken ct = default);
}
