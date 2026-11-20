using System.Globalization;
using System.Net;
using CarRental.BuildingBlocks.Results;
using CarRental.BuildingBlocks.Storage;
using CloudinaryDotNet;
using CloudinaryDotNet.Actions;
using Microsoft.Extensions.Options;
using StorageUploadResult = CarRental.BuildingBlocks.Storage.UploadResult;

namespace CarRental.Api.Infrastructure.Storage;

internal sealed class CloudinaryImageStorage : IImageStorage
{
    private readonly Cloudinary _cloudinary;
    private readonly CloudinaryOptions _options;
    private readonly TimeProvider _clock;

    public CloudinaryImageStorage(IOptions<CloudinaryOptions> options, TimeProvider clock)
    {
        _options = options.Value;
        _clock = clock;

        var account = new Account(
            _options.CloudName.Trim(),
            _options.ApiKey.Trim(),
            _options.ApiSecret.Trim());

        _cloudinary = new Cloudinary(account);
        _cloudinary.Api.Secure = true;
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
        var deliveryType = request.Visibility == StorageVisibility.Private ? "authenticated" : "upload";
        var isPdf = string.Equals(validated.ContentType, "application/pdf", StringComparison.OrdinalIgnoreCase);

        try
        {
            var response = await ExecuteWithSingleRetryAsync(async () =>
            {
                await using var uploadStream = new MemoryStream(validated.Bytes, writable: false);
                var fileDesc = new FileDescription(
                    string.IsNullOrWhiteSpace(request.FileName) ? "upload" : Path.GetFileName(request.FileName),
                    uploadStream);

                if (isPdf)
                {
                    var rawParams = new RawUploadParams
                    {
                        File = fileDesc,
                        PublicId = validated.StorageKey,
                        Type = deliveryType,
                        Overwrite = true,
                        UseFilename = false,
                        UniqueFilename = false
                    };
                    return await _cloudinary.UploadAsync(rawParams, "raw", ct);
                }

                var imageParams = new ImageUploadParams
                {
                    File = fileDesc,
                    PublicId = validated.StorageKey,
                    Type = deliveryType,
                    Overwrite = true,
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
        var keyResult = StorageValidation.ValidateStorageKey(storageKey);
        if (keyResult.IsFailure)
            return keyResult.Error;

        var key = keyResult.Value;

        try
        {
            var url = _cloudinary.Api.UrlImgUp
                .Secure(true)
                .Action(visibility == StorageVisibility.Private ? "authenticated" : "upload");

            if (visibility == StorageVisibility.Private)
                url = url.Signed(true);

            var transformation = BuildTransformation(transform);
            if (transformation is not null)
                url = url.Transform(transformation);

            var builtUrl = url.BuildUrl(key);
            if (visibility == StorageVisibility.Public)
                return builtUrl;

            var lifetime = StorageValidation.NormalizeSignedUrlLifetime(
                signedUrlLifetime,
                TimeSpan.FromMinutes(_options.DefaultSignedUrlMinutes),
                TimeSpan.FromMinutes(_options.MaxSignedUrlMinutes));
            var expiresUnix = _clock.GetUtcNow().Add(lifetime).ToUnixTimeSeconds();
            var separator = builtUrl.Contains('?', StringComparison.Ordinal) ? '&' : '?';
            return $"{builtUrl}{separator}exp={expiresUnix.ToString(CultureInfo.InvariantCulture)}";
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
        var keyResult = StorageValidation.ValidateStorageKey(storageKey);
        if (keyResult.IsFailure)
            return keyResult.Error;

        var deliveryType = visibility == StorageVisibility.Private ? "authenticated" : "upload";

        try
        {
            var response = await ExecuteWithSingleRetryAsync(
                () => _cloudinary.DestroyAsync(new DeletionParams(keyResult.Value)
                {
                    Type = deliveryType,
                    Invalidate = true
                }),
                ct);

            if (response is null)
                return StorageErrors.DeleteFailed("Empty response from Cloudinary.");

            if (response.Error is not null || (response.StatusCode != HttpStatusCode.OK && response.StatusCode != HttpStatusCode.NotFound))
                return StorageErrors.DeleteFailed(response.Error?.Message ?? $"HTTP {(int)response.StatusCode}");

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
