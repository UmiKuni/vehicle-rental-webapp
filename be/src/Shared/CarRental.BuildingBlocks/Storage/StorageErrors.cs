using CarRental.BuildingBlocks.Results;

namespace CarRental.BuildingBlocks.Storage;

public static class StorageErrors
{
    public static Error EmptyStream =>
        Error.Validation("storage.empty_stream", "Uploaded file content cannot be empty.");

    public static Error FileTooLarge(long maxBytes) =>
        Error.Validation(
            "storage.file_too_large",
            $"Uploaded file exceeds the maximum allowed size of {maxBytes} bytes.");

    public static Error UnsupportedContentType(string contentType) =>
        Error.Validation(
            "storage.unsupported_content_type",
            $"Content type '{contentType}' is not supported.");

    public static Error InvalidFolder =>
        Error.Validation("storage.invalid_folder", "Target storage folder is invalid.");

    public static Error InvalidStorageKey =>
        Error.Validation("storage.invalid_storage_key", "Storage key is required.");

    public static Error UploadFailed(string detail) =>
        Error.Failure("storage.upload_failed", $"File upload failed: {detail}");

    public static Error DeleteFailed(string detail) =>
        Error.Failure("storage.delete_failed", $"File deletion failed: {detail}");

    public static Error UrlGenerationFailed(string detail) =>
        Error.Failure("storage.url_generation_failed", $"Could not generate file URL: {detail}");
}
