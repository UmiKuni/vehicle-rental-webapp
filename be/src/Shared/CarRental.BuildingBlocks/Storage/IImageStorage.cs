using CarRental.BuildingBlocks.Results;

namespace CarRental.BuildingBlocks.Storage;

public interface IImageStorage
{
    Task<Result<UploadResult>> UploadAsync(UploadRequest request, CancellationToken ct = default);

    Result<string> GetUrl(
        string storageKey,
        StorageVisibility visibility,
        TimeSpan? signedUrlLifetime = null,
        ImageTransform? transform = null);

    Task<Result> DeleteAsync(
        string storageKey,
        StorageVisibility visibility,
        CancellationToken ct = default);
}
