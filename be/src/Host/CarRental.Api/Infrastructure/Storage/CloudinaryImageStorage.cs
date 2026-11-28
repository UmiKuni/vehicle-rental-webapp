using System.Net;
using CarRental.BuildingBlocks.Results;
using CarRental.BuildingBlocks.Storage;
using CloudinaryDotNet;
using CloudinaryDotNet.Actions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using StorageUploadResult = CarRental.BuildingBlocks.Storage.UploadResult;

namespace CarRental.Api.Infrastructure.Storage;

internal sealed class CloudinaryImageStorage : IImageStorage
{
    private readonly Cloudinary _cloudinary;
    private readonly CloudinaryOptions _options;
    private readonly TimeProvider _clock;
    private readonly ILogger<CloudinaryImageStorage> _logger;

    public CloudinaryImageStorage(
        IOptions<CloudinaryOptions> options,
        TimeProvider clock,
        ILogger<CloudinaryImageStorage>? logger = null)
    {
        _options = options.Value;
        _clock = clock;
        _logger = logger ?? NullLogger<CloudinaryImageStorage>.Instance;

        var account = new Account(
            _options.CloudName.Trim(),
            _options.ApiKey.Trim(),
            _options.ApiSecret.Trim());

        _cloudinary = new Cloudinary(account);
        _cloudinary.Api.Secure = true;
        _cloudinary.Api.SignatureAlgorithm = SignatureAlgorithm.SHA256;
    }

    public async Task<Result<StorageUploadResult>> UploadAsync(UploadRequest request, CancellationToken ct = default)
    {
        var validatedResult = await StorageValidation.ValidateAndReadUploadAsync(
            request,
            _options.MaxFileSizeBytes,
            ct);
        if (validatedResult.IsFailure)
            return validatedResult.Error;

        var validated = validatedResult.Value;
        var parsedResult = StorageValidation.ParseStorageKey(validated.StorageKey);
        if (parsedResult.IsFailure)
            return parsedResult.Error;

        var parsed = parsedResult.Value;
        var deliveryType = request.Visibility == StorageVisibility.Private ? "authenticated" : "upload";

        try
        {
            var response = await ExecuteWithSingleRetryAsync(async () =>
            {
                await using var uploadStream = new MemoryStream(validated.Bytes, writable: false);
                var fileDesc = new FileDescription(
                    string.IsNullOrWhiteSpace(request.FileName) ? "upload" : Path.GetFileName(request.FileName),
                    uploadStream);

                if (parsed.IsPdf)
                {
                    var rawParams = new RawUploadParams
                    {
                        File = fileDesc,
                        PublicId = parsed.NormalizedKey,
                        AssetFolder = parsed.Folder,
                        Type = deliveryType,
                        Overwrite = true,
                        Invalidate = true,
                        UseFilename = false,
                        UniqueFilename = false
                    };
                    return await _cloudinary.UploadAsync(rawParams, "raw", ct);
                }

                var imageParams = new ImageUploadParams
                {
                    File = fileDesc,
                    PublicId = parsed.KeyWithoutImageExtension,
                    AssetFolder = parsed.Folder,
                    Type = deliveryType,
                    Overwrite = true,
                    Invalidate = true,
                    UseFilename = false,
                    UniqueFilename = false
                };
                return await _cloudinary.UploadAsync(imageParams, ct);
            }, ct);

            if (response is null)
                return StorageErrors.UploadFailed("Empty response from Cloudinary.");

            if (response.Error is not null || (int)response.StatusCode >= 400)
                return StorageErrors.UploadFailed(response.Error?.Message ?? $"HTTP {(int)response.StatusCode}");

            return new StorageUploadResult(
                validated.StorageKey,
                validated.FileHash,
                validated.Bytes.LongLength,
                validated.ContentType);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            return StorageErrors.UploadFailed(ex.Message);
        }
    }

    public Result<string> GetUrl(
        string storageKey,
        StorageVisibility visibility,
        TimeSpan? signedUrlLifetime = null,
        ImageTransform? transform = null)
    {
        var parsedResult = StorageValidation.ParseStorageKey(storageKey);
        if (parsedResult.IsFailure)
            return parsedResult.Error;

        var parsed = parsedResult.Value;

        try
        {
            if (visibility == StorageVisibility.Public)
            {
                if (parsed.IsPdf)
                {
                    return _cloudinary.Api.Url
                        .Secure(true)
                        .ResourceType("raw")
                        .Action("upload")
                        .BuildUrl(parsed.NormalizedKey);
                }

                var publicUrl = _cloudinary.Api.UrlImgUp
                    .Secure(true)
                    .Action("upload");

                var publicTransformation = BuildTransformation(transform);
                if (publicTransformation is not null)
                    publicUrl = publicUrl.Transform(publicTransformation);

                return publicUrl.BuildUrl(parsed.NormalizedKey);
            }

            var lifetime = StorageValidation.NormalizeSignedUrlLifetime(
                signedUrlLifetime,
                TimeSpan.FromMinutes(_options.DefaultSignedUrlMinutes),
                TimeSpan.FromMinutes(_options.MaxSignedUrlMinutes));
            var expiresUnix = _clock.GetUtcNow().Add(lifetime).ToUnixTimeSeconds();

            if (parsed.IsPdf)
            {
                return _cloudinary.DownloadPrivate(
                    parsed.NormalizedKey,
                    attachment: false,
                    format: null,
                    type: "authenticated",
                    expiresAt: expiresUnix,
                    resourceType: "raw");
            }

            var privateTransformation = BuildTransformation(transform);
            if (privateTransformation is null && !string.IsNullOrWhiteSpace(parsed.Format))
            {
                return _cloudinary.DownloadPrivate(
                    parsed.KeyWithoutImageExtension,
                    attachment: false,
                    format: parsed.Format,
                    type: "authenticated",
                    expiresAt: expiresUnix,
                    resourceType: "image");
            }

            var fallbackUrl = _cloudinary.Api.UrlImgUp
                .Secure(true)
                .Action("authenticated")
                .Signed(true);

            if (privateTransformation is not null)
                fallbackUrl = fallbackUrl.Transform(privateTransformation);

            return fallbackUrl.BuildUrl(parsed.NormalizedKey);
        }
        catch (Exception ex)
        {
            return StorageErrors.UrlGenerationFailed(ex.Message);
        }
    }

    public async Task<Result> DeleteAsync(
        string storageKey,
        StorageVisibility visibility,
        CancellationToken ct = default)
    {
        var parsedResult = StorageValidation.ParseStorageKey(storageKey);
        if (parsedResult.IsFailure)
            return parsedResult.Error;

        var parsed = parsedResult.Value;
        var deliveryType = visibility == StorageVisibility.Private ? "authenticated" : "upload";
        var publicId = parsed.IsPdf ? parsed.NormalizedKey : parsed.KeyWithoutImageExtension;
        var resourceType = parsed.IsPdf ? ResourceType.Raw : ResourceType.Image;

        try
        {
            var response = await ExecuteWithSingleRetryAsync(
                () => _cloudinary.DestroyAsync(new DeletionParams(publicId)
                {
                    ResourceType = resourceType,
                    Type = deliveryType,
                    Invalidate = true
                }),
                ct);

            if (response is null)
                return StorageErrors.DeleteFailed("Empty response from Cloudinary.");

            if (response.Error is not null || (response.StatusCode != HttpStatusCode.OK && response.StatusCode != HttpStatusCode.NotFound))
                return StorageErrors.DeleteFailed(response.Error?.Message ?? $"HTTP {(int)response.StatusCode}");

            if (string.Equals(response.Result, "not found", StringComparison.OrdinalIgnoreCase))
            {
                _logger.LogWarning(
                    "Cloudinary asset '{PublicId}' (ResourceType={ResourceType}, Type={DeliveryType}) reported 'not found' during deletion.",
                    publicId,
                    resourceType,
                    deliveryType);
                return Result.Success();
            }

            if (!string.IsNullOrWhiteSpace(response.Result)
                && !string.Equals(response.Result, "ok", StringComparison.OrdinalIgnoreCase))
            {
                return StorageErrors.DeleteFailed($"Unexpected Cloudinary delete result '{response.Result}'.");
            }

            return Result.Success();
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            return StorageErrors.DeleteFailed(ex.Message);
        }
    }

    private static Transformation? BuildTransformation(ImageTransform? transform)
    {
        if (transform is null)
            return null;

        var t = new Transformation();
        var hasAny = false;

        if (transform.Width.HasValue)
        {
            t.Width(transform.Width.Value);
            hasAny = true;
        }

        if (transform.Height.HasValue)
        {
            t.Height(transform.Height.Value);
            hasAny = true;
        }

        if (!string.IsNullOrWhiteSpace(transform.Crop))
        {
            t.Crop(transform.Crop.Trim());
            hasAny = true;
        }

        if (transform.Quality.HasValue)
        {
            t.Quality(transform.Quality.Value);
            hasAny = true;
        }

        return hasAny ? t : null;
    }

    private static async Task<TResult> ExecuteWithSingleRetryAsync<TResult>(
        Func<Task<TResult>> action,
        CancellationToken ct)
        where TResult : BaseResult
    {
        try
        {
            var first = await action();
            if (first is not null && !IsTransientFailure(first))
                return first;
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException && !ct.IsCancellationRequested)
        {
            // Retry once on transient network failure
        }

        return await action();
    }

    private static bool IsTransientFailure(BaseResult result) =>
        result.StatusCode == HttpStatusCode.TooManyRequests
        || (int)result.StatusCode >= 500;
}
