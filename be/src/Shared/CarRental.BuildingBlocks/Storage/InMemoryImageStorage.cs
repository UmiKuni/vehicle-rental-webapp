using System.Collections.Concurrent;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using CarRental.BuildingBlocks.Results;

namespace CarRental.BuildingBlocks.Storage;

public sealed class InMemoryImageStorage : IImageStorage
{
    private static readonly byte[] SigningKey = Encoding.UTF8.GetBytes("carrental-inmemory-storage-signing-key");

    private readonly ConcurrentDictionary<string, StoredEntry> _entries = new(StringComparer.Ordinal);
    private readonly TimeProvider _clock;
    private readonly long _maxFileSizeBytes;

    public InMemoryImageStorage(
        TimeProvider? clock = null,
        long maxFileSizeBytes = StorageValidation.DefaultMaxFileSizeBytes)
    {
        _clock = clock ?? TimeProvider.System;
        _maxFileSizeBytes = maxFileSizeBytes > 0 ? maxFileSizeBytes : StorageValidation.DefaultMaxFileSizeBytes;
    }

    public async Task<Result<UploadResult>> UploadAsync(UploadRequest request, CancellationToken ct = default)
    {
        var validatedResult = await StorageValidation.ValidateAndReadUploadAsync(request, _maxFileSizeBytes, ct);
        if (validatedResult.IsFailure)
            return validatedResult.Error;

        var validated = validatedResult.Value;
        _entries[validated.StorageKey] = new StoredEntry(
            validated.Bytes,
            validated.ContentType,
            validated.FileHash,
            request.Visibility);

        return new UploadResult(
            validated.StorageKey,
            validated.FileHash,
            validated.Bytes.LongLength,
            validated.ContentType);
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
        if (visibility == StorageVisibility.Public)
        {
            var baseUrl = $"https://storage.carrental.vn/public/{key}";
            return AppendTransformQuery(baseUrl, transform);
        }

        var lifetime = StorageValidation.NormalizeSignedUrlLifetime(signedUrlLifetime);
        var exp = _clock.GetUtcNow().Add(lifetime).ToUnixTimeSeconds();
        var payload = $"{key}:{exp.ToString(CultureInfo.InvariantCulture)}";
        var sig = Convert.ToHexStringLower(HMACSHA256.HashData(SigningKey, Encoding.UTF8.GetBytes(payload)));

        return $"https://storage.carrental.vn/private/{key}?exp={exp.ToString(CultureInfo.InvariantCulture)}&sig={sig}";
    }

    public Task<Result> DeleteAsync(
        string storageKey,
        StorageVisibility visibility,
        CancellationToken ct = default)
    {
        var keyResult = StorageValidation.ValidateStorageKey(storageKey);
        if (keyResult.IsFailure)
            return Task.FromResult<Result>(keyResult.Error);

        _entries.TryRemove(keyResult.Value, out _);
        return Task.FromResult(Result.Success());
    }

    public bool Contains(string storageKey) =>
        !string.IsNullOrWhiteSpace(storageKey) && _entries.ContainsKey(storageKey.Trim());

    public byte[]? GetBytes(string storageKey) =>
        !string.IsNullOrWhiteSpace(storageKey) && _entries.TryGetValue(storageKey.Trim(), out var entry)
            ? entry.Bytes
            : null;

    private static string AppendTransformQuery(string baseUrl, ImageTransform? transform)
    {
        if (transform is null)
            return baseUrl;

        var query = new List<string>();
        if (transform.Width.HasValue)
            query.Add($"w={transform.Width.Value.ToString(CultureInfo.InvariantCulture)}");
        if (transform.Height.HasValue)
            query.Add($"h={transform.Height.Value.ToString(CultureInfo.InvariantCulture)}");
        if (!string.IsNullOrWhiteSpace(transform.Crop))
            query.Add($"c={Uri.EscapeDataString(transform.Crop.Trim())}");
        if (transform.Quality.HasValue)
            query.Add($"q={transform.Quality.Value.ToString(CultureInfo.InvariantCulture)}");

        return query.Count == 0 ? baseUrl : $"{baseUrl}?{string.Join('&', query)}";
    }

    private sealed record StoredEntry(
        byte[] Bytes,
        string ContentType,
        string FileHash,
        StorageVisibility Visibility);
}
