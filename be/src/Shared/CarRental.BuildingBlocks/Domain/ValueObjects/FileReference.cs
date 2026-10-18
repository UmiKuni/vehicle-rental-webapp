using CarRental.BuildingBlocks.Domain;
using CarRental.BuildingBlocks.Results;

namespace CarRental.BuildingBlocks.Domain.ValueObjects;

/// <summary>
/// Value object referencing an uploaded media or document file.
/// Enforces storage security rules: private files (identity documents, receipts) cannot store persistent public URLs.
/// </summary>
public sealed record FileReference : ValueObject
{
    public string StorageKey { get; init; }
    public string? Url { get; init; }
    public bool IsPrivate { get; init; }
    public string ContentType { get; init; }
    public long SizeBytes { get; init; }

    /// <summary>
    /// Parameterless constructor required by EF Core.
    /// </summary>
    private FileReference()
    {
        StorageKey = string.Empty;
        ContentType = "application/octet-stream";
    }

    public FileReference(string storageKey, string? url, bool isPrivate, string contentType, long sizeBytes)
    {
        Guard.AgainstNullOrWhiteSpace(storageKey, "file_reference.invalid_storage_key", "Storage key is required.");
        Guard.Against(sizeBytes < 0, "file_reference.invalid_size", "File size cannot be negative.");
        Guard.Against(isPrivate && !string.IsNullOrWhiteSpace(url), "file_reference.private_url_forbidden", "Private files cannot store persistent public URLs.");

        StorageKey = storageKey.Trim();
        Url = isPrivate ? null : url?.Trim();
        IsPrivate = isPrivate;
        ContentType = string.IsNullOrWhiteSpace(contentType) ? "application/octet-stream" : contentType.Trim();
        SizeBytes = sizeBytes;
    }

    public static Result<FileReference> Create(
        string storageKey,
        string? url,
        bool isPrivate,
        string contentType,
        long sizeBytes)
    {
        if (string.IsNullOrWhiteSpace(storageKey))
        {
            return Result<FileReference>.Failure(
                new Error("file_reference.invalid_storage_key", "Storage key is required."));
        }

        if (sizeBytes < 0)
        {
            return Result<FileReference>.Failure(
                new Error("file_reference.invalid_size", "File size cannot be negative."));
        }

        if (isPrivate && !string.IsNullOrWhiteSpace(url))
        {
            return Result<FileReference>.Failure(
                new Error("file_reference.private_url_forbidden", "Private files cannot store persistent public URLs."));
        }

        return Result<FileReference>.Success(new FileReference(storageKey, url, isPrivate, contentType, sizeBytes));
    }

    public static FileReference CreatePrivate(string storageKey, string contentType, long sizeBytes) =>
        new(storageKey, null, true, contentType, sizeBytes);

    public static FileReference CreatePublic(string storageKey, string url, string contentType, long sizeBytes) =>
        new(storageKey, url, false, contentType, sizeBytes);

    public override string ToString() => IsPrivate ? $"[Private] {StorageKey}" : $"{StorageKey} ({Url})";
}
