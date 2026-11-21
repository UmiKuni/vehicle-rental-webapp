namespace CarRental.BuildingBlocks.Storage;

public enum StorageVisibility
{
    Public = 0,
    Private = 1
}

public sealed record ImageTransform(
    int? Width = null,
    int? Height = null,
    string? Crop = "limit",
    int? Quality = null);

public sealed record UploadRequest(
    Stream Content,
    string FileName,
    string ContentType,
    string Folder,
    StorageVisibility Visibility,
    bool AllowPdf = false);

public sealed record UploadResult(
    string StorageKey,
    string FileHash,
    long SizeBytes,
    string ContentType);
