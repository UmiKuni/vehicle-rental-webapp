using CarRental.BuildingBlocks.Storage;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;

namespace CarRental.Api.Infrastructure.Storage;

public static class StorageServiceCollectionExtensions
{
    public static IServiceCollection AddImageStorage(
        this IServiceCollection services,
        IConfiguration configuration,
        IHostEnvironment environment)
    {
        services.AddOptions<CloudinaryOptions>()
            .BindConfiguration(CloudinaryOptions.SectionName)
            .ValidateOnStart();

        services.AddSingleton<IValidateOptions<CloudinaryOptions>, CloudinaryOptionsValidator>();

        var options = configuration.GetSection(CloudinaryOptions.SectionName).Get<CloudinaryOptions>()
                      ?? new CloudinaryOptions();

        if (!environment.IsEnvironment("Testing") && options.IsConfigured)
        {
            services.AddSingleton<IImageStorage, CloudinaryImageStorage>();
        }
        else
        {
            var maxSize = options.MaxFileSizeBytes > 0
                ? options.MaxFileSizeBytes
                : StorageValidation.DefaultMaxFileSizeBytes;

            services.TryAddSingleton<IImageStorage>(sp =>
                new InMemoryImageStorage(sp.GetService<TimeProvider>(), maxSize));
        }

        return services;
    }
}
