using System.Security.Cryptography;
using CarRental.BuildingBlocks.Results;

namespace CarRental.BuildingBlocks.Storage;

public static class StorageValidation
{
    public const long DefaultMaxFileSizeBytes = 10L * 1024 * 1024;
    public static readonly TimeSpan DefaultSignedUrlLifetime = TimeSpan.FromMinutes(5);
    public static readonly TimeSpan MinSignedUrlLifetime = TimeSpan.FromMinutes(1);
    public static readonly TimeSpan MaxSignedUrlLifetime = TimeSpan.FromMinutes(15);

    private static readonly HashSet<string> AllowedImageContentTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        "image/jpeg",
        "image/jpg",
        "image/png",
        "image/webp"
    };

    public static async Task<Result<ValidatedUpload>> ValidateAndReadUploadAsync(
        UploadRequest? request,
        long maxFileSizeBytes = DefaultMaxFileSizeBytes,
        CancellationToken ct = default)
    {
        if (request is null || request.Content is null || !request.Content.CanRead)
            return StorageErrors.EmptyStream;

        var folderResult = NormalizeFolder(request.Folder);
        if (folderResult.IsFailure)
            return folderResult.Error;

        var normalizedFolder = folderResult.Value;
        var contentType = NormalizeContentType(request.ContentType);
        var allowPdf = request.AllowPdf || IsIdentityFolder(normalizedFolder);

        if (!IsAllowedContentType(contentType, allowPdf))
            return StorageErrors.UnsupportedContentType(request.ContentType ?? string.Empty);

        var effectiveMaxBytes = maxFileSizeBytes > 0 ? maxFileSizeBytes : DefaultMaxFileSizeBytes;
        var readResult = await ReadWithLimitAsync(request.Content, effectiveMaxBytes, ct);
        if (readResult.IsFailure)
            return readResult.Error;

        var bytes = readResult.Value;
        var fileHash = Convert.ToHexStringLower(SHA256.HashData(bytes));
        var storageKey = $"{normalizedFolder}/{Guid.CreateVersion7():N}";

        return new ValidatedUpload(bytes, contentType, storageKey, fileHash);
    }

    public static Result<string> ValidateStorageKey(string? storageKey)
    {
        if (string.IsNullOrWhiteSpace(storageKey))
            return StorageErrors.InvalidStorageKey;

        var trimmed = storageKey.Trim().Replace('\\', '/').TrimStart('/');
        if (string.IsNullOrWhiteSpace(trimmed))
            return StorageErrors.InvalidStorageKey;

        return trimmed;
    }

    public static TimeSpan NormalizeSignedUrlLifetime(
        TimeSpan? signedUrlLifetime,
        TimeSpan? defaultLifetime = null,
        TimeSpan? maxLifetime = null)
    {
        var effectiveDefault = defaultLifetime ?? DefaultSignedUrlLifetime;
        var effectiveMax = maxLifetime ?? MaxSignedUrlLifetime;
        var requested = signedUrlLifetime ?? effectiveDefault;

        if (requested < MinSignedUrlLifetime)
            return MinSignedUrlLifetime;

        return requested > effectiveMax ? effectiveMax : requested;
    }

    public static Result<string> NormalizeFolder(string? folder)
    {
        if (string.IsNullOrWhiteSpace(folder))
            return StorageErrors.InvalidFolder;

        var normalized = folder.Trim().Replace('\\', '/').Trim('/');
        if (string.IsNullOrWhiteSpace(normalized))
            return StorageErrors.InvalidFolder;

        var segments = normalized.Split('/');
        foreach (var segment in segments)
        {
            if (string.IsNullOrWhiteSpace(segment) || segment is "." or "..")
                return StorageErrors.InvalidFolder;
        }

        return string.Join('/', segments);
    }

    private static bool IsIdentityFolder(string normalizedFolder) =>
        normalizedFolder.Equals("identity", StringComparison.OrdinalIgnoreCase)
        || normalizedFolder.StartsWith("identity/", StringComparison.OrdinalIgnoreCase);

    private static string NormalizeContentType(string? contentType)
    {
        if (string.IsNullOrWhiteSpace(contentType))
            return string.Empty;

        var semicolonIndex = contentType.IndexOf(';');
        var mediaType = semicolonIndex >= 0 ? contentType[..semicolonIndex] : contentType;
        return mediaType.Trim().ToLowerInvariant();
    }

    private static bool IsAllowedContentType(string contentType, bool allowPdf)
    {
        if (AllowedImageContentTypes.Contains(contentType))
            return true;

        return allowPdf && string.Equals(contentType, "application/pdf", StringComparison.OrdinalIgnoreCase);
    }

    private static async Task<Result<byte[]>> ReadWithLimitAsync(
        Stream content,
        long maxFileSizeBytes,
        CancellationToken ct)
    {
        using var buffer = new MemoryStream();
        var chunk = new byte[81920];
        long totalRead = 0;

        while (true)
        {
            var remainingAllowed = maxFileSizeBytes - totalRead;
            var toRead = remainingAllowed > 0
                ? (int)Math.Min(chunk.Length, remainingAllowed + 1)
                : 1;

            var read = await content.ReadAsync(chunk.AsMemory(0, toRead), ct);
            if (read == 0)
                break;

            totalRead += read;
            if (totalRead > maxFileSizeBytes)
                return StorageErrors.FileTooLarge(maxFileSizeBytes);

            buffer.Write(chunk, 0, read);
        }

        if (totalRead == 0)
            return StorageErrors.EmptyStream;

        return buffer.ToArray();
    }
}

public sealed record ValidatedUpload(
    byte[] Bytes,
    string ContentType,
    string StorageKey,
    string FileHash);
