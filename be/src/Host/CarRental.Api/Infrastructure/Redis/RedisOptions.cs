using Microsoft.Extensions.Options;

namespace CarRental.Api.Infrastructure.Redis;

public sealed class RedisOptions
{
    public const string SectionName = "Redis";

    public bool Enabled { get; set; } = true;

    public string KeyPrefix { get; set; } = "carrental:";

    public int DefaultLockTtlSeconds { get; set; } = 10;

    public int DefaultLockWaitMs { get; set; } = 3000;
}

internal sealed class RedisOptionsValidator : IValidateOptions<RedisOptions>
{
    public ValidateOptionsResult Validate(string? name, RedisOptions options)
    {
        if (options.DefaultLockTtlSeconds <= 0)
            return ValidateOptionsResult.Fail("Redis:DefaultLockTtlSeconds must be greater than zero.");

        if (options.DefaultLockWaitMs < 0)
            return ValidateOptionsResult.Fail("Redis:DefaultLockWaitMs must be zero or greater.");

        return ValidateOptionsResult.Success;
    }
}

