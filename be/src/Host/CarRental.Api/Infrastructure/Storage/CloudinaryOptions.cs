using Microsoft.Extensions.Options;

namespace CarRental.Api.Infrastructure.Storage;

public sealed class CloudinaryOptions
{
    public const string SectionName = "Cloudinary";

    public string CloudName { get; set; } = string.Empty;
    public string ApiKey { get; set; } = string.Empty;
    public string ApiSecret { get; set; } = string.Empty;
    public long MaxFileSizeBytes { get; set; } = 10L * 1024 * 1024;
    public int DefaultSignedUrlMinutes { get; set; } = 5;
    public int MaxSignedUrlMinutes { get; set; } = 15;

    public bool IsConfigured =>
        !string.IsNullOrWhiteSpace(CloudName)
        && !string.IsNullOrWhiteSpace(ApiKey)
        && !string.IsNullOrWhiteSpace(ApiSecret);
}

internal sealed class CloudinaryOptionsValidator : IValidateOptions<CloudinaryOptions>
{
    public ValidateOptionsResult Validate(string? name, CloudinaryOptions options)
    {
        if (options.MaxFileSizeBytes <= 0)
            return ValidateOptionsResult.Fail("Cloudinary:MaxFileSizeBytes must be greater than zero.");

        if (options.DefaultSignedUrlMinutes < 1)
            return ValidateOptionsResult.Fail("Cloudinary:DefaultSignedUrlMinutes must be at least 1.");

        if (options.MaxSignedUrlMinutes < options.DefaultSignedUrlMinutes || options.MaxSignedUrlMinutes > 60)
            return ValidateOptionsResult.Fail("Cloudinary:MaxSignedUrlMinutes must be between DefaultSignedUrlMinutes and 60.");

        var hasAnyCredential =
            !string.IsNullOrWhiteSpace(options.CloudName)
            || !string.IsNullOrWhiteSpace(options.ApiKey)
            || !string.IsNullOrWhiteSpace(options.ApiSecret);

        if (hasAnyCredential && !options.IsConfigured)
            return ValidateOptionsResult.Fail("Cloudinary:CloudName, ApiKey, and ApiSecret must all be set when configuring Cloudinary.");

        return ValidateOptionsResult.Success;
    }
}
