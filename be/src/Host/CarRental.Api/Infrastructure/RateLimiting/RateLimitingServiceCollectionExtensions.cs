using Microsoft.Extensions.DependencyInjection.Extensions;
using StackExchange.Redis;

namespace CarRental.Api.Infrastructure.RateLimiting;

public static class RateLimitingServiceCollectionExtensions
{
    public static IServiceCollection AddDistributedRateLimiting(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services.AddOptions<RateLimitingOptions>()
            .BindConfiguration(RateLimitingOptions.SectionName);

        var hasRedis = services.Any(d => d.ServiceType == typeof(IConnectionMultiplexer));
        if (hasRedis)
        {
            services.AddSingleton<IRateLimiter, RedisRateLimiter>();
        }
        else
        {
            services.TryAddSingleton<IRateLimiter>(sp =>
                new InMemoryRateLimiter(sp.GetService<TimeProvider>()));
        }

        return services;
    }
}
