using System.Security.Cryptography;
using CarRental.BuildingBlocks.Results;
using CarRental.BuildingBlocks.Storage;
using FluentAssertions;

namespace CarRental.BuildingBlocks.Tests;

public class ImageStorageTests
{
    private static readonly DateTimeOffset Now = new(2026, 11, 19, 10, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task UploadAsync_WithValidImages_ShouldGenerateVersion7KeyComputeSha256AndStoreBytes()
    {
        var storage = new InMemoryImageStorage(new FixedTimeProvider(Now));
        var jpegBytes = new byte[] { 0xFF, 0xD8, 0xFF, 0xE0, 0x01, 0x02, 0x03 };
        var expectedHash = Convert.ToHexStringLower(SHA256.HashData(jpegBytes));

        await using var stream = new MemoryStream(jpegBytes);
        var result = await storage.UploadAsync(new UploadRequest(
            stream,
            "front-left.jpg",
            "image/jpeg",
            "handover/booking-123",
            StorageVisibility.Private));

        result.IsSuccess.Should().BeTrue();
        result.Value.StorageKey.Should().StartWith("handover/booking-123/");
        result.Value.StorageKey.Should().NotContain("front-left");
        result.Value.FileHash.Should().Be(expectedHash);
        result.Value.SizeBytes.Should().Be(jpegBytes.Length);
        result.Value.ContentType.Should().Be("image/jpeg");
        storage.Contains(result.Value.StorageKey).Should().BeTrue();
        storage.GetBytes(result.Value.StorageKey).Should().Equal(jpegBytes);

        await using var pngStream = new MemoryStream([1, 2, 3, 4]);
        var pngResult = await storage.UploadAsync(new UploadRequest(
            pngStream, "card.png", "image/png", "vehicles/v1", StorageVisibility.Public));
        pngResult.IsSuccess.Should().BeTrue();

        await using var webpStream = new MemoryStream([5, 6, 7, 8]);
        var webpResult = await storage.UploadAsync(new UploadRequest(
            webpStream, "card.webp", "image/webp", "vehicles/v1", StorageVisibility.Public));
        webpResult.IsSuccess.Should().BeTrue();
    }

    [Fact]
    public async Task UploadAsync_PdfRule_ShouldAllowOnlyForIdentityOrExplicitFlag()
    {
        var storage = new InMemoryImageStorage(new FixedTimeProvider(Now));
        var pdfBytes = "%PDF-1.7 sample"u8.ToArray();

        await using var vehiclePdf = new MemoryStream(pdfBytes);
        var rejected = await storage.UploadAsync(new UploadRequest(
            vehiclePdf, "doc.pdf", "application/pdf", "vehicles/v1", StorageVisibility.Public));
        rejected.IsFailure.Should().BeTrue();
        rejected.Error.Type.Should().Be(ErrorType.Validation);
        rejected.Error.Code.Should().Be(StorageErrors.UnsupportedContentType("application/pdf").Code);

        await using var identityPdf = new MemoryStream(pdfBytes);
        var identityResult = await storage.UploadAsync(new UploadRequest(
            identityPdf, "cccd.pdf", "application/pdf", "identity/user-1", StorageVisibility.Private));
        identityResult.IsSuccess.Should().BeTrue();

        await using var explicitPdf = new MemoryStream(pdfBytes);
        var explicitResult = await storage.UploadAsync(new UploadRequest(
            explicitPdf, "evidence.pdf", "application/pdf", "support/ticket-1", StorageVisibility.Private, AllowPdf: true));
        explicitResult.IsSuccess.Should().BeTrue();
    }

    [Fact]
    public async Task UploadAsync_InvalidInputs_ShouldReturnExpectedValidationErrors()
    {
        var storage = new InMemoryImageStorage(new FixedTimeProvider(Now), maxFileSizeBytes: 8);

        await using var empty = new MemoryStream([]);
        var emptyResult = await storage.UploadAsync(new UploadRequest(
            empty, "empty.jpg", "image/jpeg", "vehicles/v1", StorageVisibility.Public));
        emptyResult.IsFailure.Should().BeTrue();
        emptyResult.Error.Code.Should().Be(StorageErrors.EmptyStream.Code);

        await using var tooLarge = new MemoryStream(new byte[9]);
        var sizeResult = await storage.UploadAsync(new UploadRequest(
            tooLarge, "big.jpg", "image/jpeg", "vehicles/v1", StorageVisibility.Public));
        sizeResult.IsFailure.Should().BeTrue();
        sizeResult.Error.Code.Should().Be("storage.file_too_large");

        await using var traversal = new MemoryStream([1, 2, 3]);
        var folderResult = await storage.UploadAsync(new UploadRequest(
            traversal, "a.jpg", "image/jpeg", "../secrets", StorageVisibility.Private));
        folderResult.IsFailure.Should().BeTrue();
        folderResult.Error.Code.Should().Be(StorageErrors.InvalidFolder.Code);

        await using var unsupported = new MemoryStream([1, 2, 3]);
        var typeResult = await storage.UploadAsync(new UploadRequest(
            unsupported, "script.sh", "text/plain", "vehicles/v1", StorageVisibility.Public));
        typeResult.IsFailure.Should().BeTrue();
        typeResult.Error.Code.Should().Be("storage.unsupported_content_type");
    }

    [Fact]
    public void GetUrl_ShouldHandlePublicTransformsAndClampedPrivateSignedUrls()
    {
        var storage = new InMemoryImageStorage(new FixedTimeProvider(Now));

        var invalid = storage.GetUrl("   ", StorageVisibility.Public);
        invalid.IsFailure.Should().BeTrue();
        invalid.Error.Code.Should().Be(StorageErrors.InvalidStorageKey.Code);

        var publicUrl = storage.GetUrl(
            "vehicles/v1/abc",
            StorageVisibility.Public,
            transform: new ImageTransform(Width: 800, Height: 600, Crop: "fill", Quality: 80));
        publicUrl.IsSuccess.Should().BeTrue();
        publicUrl.Value.Should().Be("https://storage.carrental.vn/public/vehicles/v1/abc?w=800&h=600&c=fill&q=80");

        var privateDefault = storage.GetUrl("handover/b1/xyz", StorageVisibility.Private);
        privateDefault.IsSuccess.Should().BeTrue();
        var defaultExp = Now.AddMinutes(5).ToUnixTimeSeconds();
        privateDefault.Value.Should().StartWith($"https://storage.carrental.vn/private/handover/b1/xyz?exp={defaultExp}&sig=");

        // Lifetime above 15 minutes is clamped to 15 minutes
        var privateClampedMax = storage.GetUrl("handover/b1/xyz", StorageVisibility.Private, TimeSpan.FromHours(2));
        privateClampedMax.IsSuccess.Should().BeTrue();
        var maxExp = Now.AddMinutes(15).ToUnixTimeSeconds();
        privateClampedMax.Value.Should().Contain($"exp={maxExp}&sig=");

        // Lifetime below 1 minute is clamped to 1 minute
        var privateClampedMin = storage.GetUrl("handover/b1/xyz", StorageVisibility.Private, TimeSpan.FromSeconds(5));
        privateClampedMin.IsSuccess.Should().BeTrue();
        var minExp = Now.AddMinutes(1).ToUnixTimeSeconds();
        privateClampedMin.Value.Should().Contain($"exp={minExp}&sig=");
    }

    [Fact]
    public async Task DeleteAsync_ShouldRemoveStoredEntryAndValidateKey()
    {
        var storage = new InMemoryImageStorage(new FixedTimeProvider(Now));

        var invalidDelete = await storage.DeleteAsync("", StorageVisibility.Public);
        invalidDelete.IsFailure.Should().BeTrue();
        invalidDelete.Error.Code.Should().Be(StorageErrors.InvalidStorageKey.Code);

        await using var stream = new MemoryStream([10, 20, 30]);
        var uploaded = await storage.UploadAsync(new UploadRequest(
            stream, "car.jpg", "image/jpeg", "vehicles/v1", StorageVisibility.Public));
        uploaded.IsSuccess.Should().BeTrue();

        var deleted = await storage.DeleteAsync(uploaded.Value.StorageKey, StorageVisibility.Public);
        deleted.IsSuccess.Should().BeTrue();
        storage.Contains(uploaded.Value.StorageKey).Should().BeFalse();
    }

    private sealed class FixedTimeProvider(DateTimeOffset utcNow) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => utcNow;
    }
}
