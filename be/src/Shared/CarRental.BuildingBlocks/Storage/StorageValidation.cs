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
        var signatureResult = ValidateSignatureAndResolveType(bytes, contentType, request.ContentType ?? string.Empty);
        if (signatureResult.IsFailure)
            return signatureResult.Error;

        var (canonicalContentType, extension) = signatureResult.Value;
        var fileHash = Convert.ToHexStringLower(SHA256.HashData(bytes));
        var storageKey = $"{normalizedFolder}/{Guid.CreateVersion7():N}{extension}";

        return new ValidatedUpload(bytes, canonicalContentType, storageKey, fileHash);
    }

    public static Result<string> ValidateStorageKey(string? storageKey)
    {
        if (string.IsNullOrWhiteSpace(storageKey))
            return StorageErrors.InvalidStorageKey;

        var trimmed = storageKey.Trim().Replace('\\', '/').TrimStart('/');
        if (string.IsNullOrWhiteSpace(trimmed) || trimmed.EndsWith('/'))
            return StorageErrors.InvalidStorageKey;

        var segments = trimmed.Split('/');
        foreach (var segment in segments)
        {
            if (string.IsNullOrWhiteSpace(segment) || segment is "." or "..")
                return StorageErrors.InvalidStorageKey;
        }

        return string.Join('/', segments);
    }

    public static Result<ParsedStorageKey> ParseStorageKey(string? storageKey)
    {
        var keyResult = ValidateStorageKey(storageKey);
        if (keyResult.IsFailure)
            return keyResult.Error;

        var key = keyResult.Value;
        var lastSlash = key.LastIndexOf('/');
        var folder = lastSlash > 0 ? key[..lastSlash] : null;
        var fileName = lastSlash >= 0 ? key[(lastSlash + 1)..] : key;
        var dotIndex = fileName.LastIndexOf('.');

        if (dotIndex <= 0 || dotIndex == fileName.Length - 1)
            return new ParsedStorageKey(key, key, folder, Format: null, IsPdf: false);

        var ext = fileName[dotIndex..].ToLowerInvariant();
        var keyWithoutExt = key[..(key.Length - ext.Length)];

        return ext switch
        {
            ".pdf" => new ParsedStorageKey(key, key, folder, "pdf", IsPdf: true),
            ".jpg" or ".jpeg" => new ParsedStorageKey(key, keyWithoutExt, folder, "jpg", IsPdf: false),
            ".png" => new ParsedStorageKey(key, keyWithoutExt, folder, "png", IsPdf: false),
            ".webp" => new ParsedStorageKey(key, keyWithoutExt, folder, "webp", IsPdf: false),
            _ => new ParsedStorageKey(key, key, folder, Format: null, IsPdf: false)
        };
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

    private static Result<(string CanonicalContentType, string Extension)> ValidateSignatureAndResolveType(
        ReadOnlySpan<byte> bytes,
        string normalizedContentType,
        string rawContentType)
    {
        if (IsJpeg(bytes))
        {
            return normalizedContentType is "image/jpeg" or "image/jpg"
                ? ("image/jpeg", ".jpg")
                : StorageErrors.InvalidFileSignature(rawContentType);
        }

        if (IsPng(bytes))
        {
            return normalizedContentType == "image/png"
                ? ("image/png", ".png")
                : StorageErrors.InvalidFileSignature(rawContentType);
        }

        if (IsWebp(bytes))
        {
            return normalizedContentType == "image/webp"
                ? ("image/webp", ".webp")
                : StorageErrors.InvalidFileSignature(rawContentType);
        }

        if (IsPdf(bytes))
        {
            return normalizedContentType == "application/pdf"
                ? ("application/pdf", ".pdf")
                : StorageErrors.InvalidFileSignature(rawContentType);
        }

        return StorageErrors.InvalidFileSignature(rawContentType);
    }

    private static bool IsJpeg(ReadOnlySpan<byte> bytes) =>
        bytes.Length >= 3
        && bytes[0] == 0xFF
        && bytes[1] == 0xD8
        && bytes[2] == 0xFF;

    private static bool IsPng(ReadOnlySpan<byte> bytes) =>
        bytes.Length >= 8
        && bytes[0] == 0x89
        && bytes[1] == 0x50
        && bytes[2] == 0x4E
        && bytes[3] == 0x47
        && bytes[4] == 0x0D
        && bytes[5] == 0x0A
        && bytes[6] == 0x1A
        && bytes[7] == 0x0A;

    private static bool IsWebp(ReadOnlySpan<byte> bytes) =>
        bytes.Length >= 12
        && bytes[0] == (byte)'R'
        && bytes[1] == (byte)'I'
        && bytes[2] == (byte)'F'
        && bytes[3] == (byte)'F'
        && bytes[8] == (byte)'W'
        && bytes[9] == (byte)'E'
        && bytes[10] == (byte)'B'
        && bytes[11] == (byte)'P';

    private static bool IsPdf(ReadOnlySpan<byte> bytes) =>
        bytes.Length >= 5
        && bytes[0] == (byte)'%'
        && bytes[1] == (byte)'P'
        && bytes[2] == (byte)'D'
        && bytes[3] == (byte)'F'
        && bytes[4] == (byte)'-';

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

public sealed record ParsedStorageKey(
    string NormalizedKey,
    string KeyWithoutImageExtension,
    string? Folder,
    string? Format,
    bool IsPdf);
