using CarRental.BuildingBlocks.Auth;
using CarRental.BuildingBlocks.Redis;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;
using StackExchange.Redis;

namespace CarRental.Api.Infrastructure.Redis;

public static class RedisServiceCollectionExtensions
{
    public static IServiceCollection AddRedisInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration,
        IHostEnvironment environment)
    {
        services.AddOptions<RedisOptions>()
            .BindConfiguration(RedisOptions.SectionName)
            .ValidateOnStart();

        services.AddSingleton<IValidateOptions<RedisOptions>, RedisOptionsValidator>();

        var options = configuration.GetSection(RedisOptions.SectionName).Get<RedisOptions>() ?? new RedisOptions();
        var redisConnectionString = configuration.GetConnectionString("Redis");

        if (!environment.IsEnvironment("Testing")
            && options.Enabled
            && !string.IsNullOrWhiteSpace(redisConnectionString))
        {
            var configOptions = ConfigurationOptions.Parse(redisConnectionString);
            configOptions.AbortOnConnectFail = false;

            services.AddSingleton<IConnectionMultiplexer>(_ => ConnectionMultiplexer.Connect(configOptions));
            services.AddSingleton<ICacheService, RedisCacheService>();
            services.AddSingleton<IDistributedLockProvider, RedisDistributedLockProvider>();
            services.AddSingleton<ITokenRevocationStore, RedisTokenRevocationStore>();
        }
        else
        {
            services.TryAddSingleton<ICacheService>(sp =>
                new InMemoryCacheService(sp.GetService<TimeProvider>()));
            services.TryAddSingleton<IDistributedLockProvider, InMemoryDistributedLockProvider>();
            services.TryAddSingleton<ITokenRevocationStore>(sp =>
                new InMemoryTokenRevocationStore(sp.GetService<TimeProvider>()));
        }

        return services;
    }
}

