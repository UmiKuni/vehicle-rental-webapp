using CarRental.Api.Infrastructure.Storage;
using CarRental.BuildingBlocks.Storage;
using FluentAssertions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;
using Xunit;

namespace CarRental.IntegrationTests.Infrastructure;

public sealed class CloudinaryImageStorageTests
{
    private static readonly DateTimeOffset FixedNow = new(2026, 11, 27, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public void GetUrl_PrivatePdf_ShouldGenerateExpiringRawDownloadPrivateUrl()
    {
        var storage = CreateCloudinaryStorage();

        var result = storage.GetUrl(
            "identity/user123/01936f00aabbccddeeff001122334455.pdf",
            StorageVisibility.Private,
            TimeSpan.FromMinutes(10));

        result.IsSuccess.Should().BeTrue();
        var expectedExp = FixedNow.AddMinutes(10).ToUnixTimeSeconds();
        result.Value.Should().StartWith("https://api.cloudinary.com/v1_1/demo-cloud/raw/download?");
        result.Value.Should().Contain("public_id=identity/user123/01936f00aabbccddeeff001122334455.pdf");
        result.Value.Should().Contain($"expires_at={expectedExp}");
        result.Value.Should().Contain("type=authenticated");
        result.Value.Should().Contain("attachment=false");
        result.Value.Should().Contain("signature=");
        result.Value.Should().NotContain("format=");
    }

    [Fact]
    public void GetUrl_PrivateImageWithExtension_ShouldGenerateExpiringImageDownloadPrivateUrlWithFormat()
    {
        var storage = CreateCloudinaryStorage();

        var result = storage.GetUrl(
            "handover/booking123/01936f00aabbccddeeff001122334455.jpg",
            StorageVisibility.Private,
            TimeSpan.FromMinutes(10));

        result.IsSuccess.Should().BeTrue();
        var expectedExp = FixedNow.AddMinutes(10).ToUnixTimeSeconds();
        result.Value.Should().StartWith("https://api.cloudinary.com/v1_1/demo-cloud/image/download?");
        result.Value.Should().Contain("public_id=handover/booking123/01936f00aabbccddeeff001122334455");
        result.Value.Should().NotContain("public_id=handover/booking123/01936f00aabbccddeeff001122334455.jpg");
        result.Value.Should().Contain("format=jpg");
        result.Value.Should().Contain($"expires_at={expectedExp}");
        result.Value.Should().Contain("type=authenticated");
    }

    [Fact]
    public void GetUrl_LegacyExtensionlessPrivateImage_ShouldFallbackToSignedAuthenticatedCdnUrlWithoutFakeExp()
    {
        var storage = CreateCloudinaryStorage();

        var result = storage.GetUrl(
            "handover/booking123/legacykeywithoutformat",
            StorageVisibility.Private,
            TimeSpan.FromMinutes(10));

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().StartWith("https://res.cloudinary.com/demo-cloud/image/authenticated/s--");
        result.Value.Should().EndWith("/handover/booking123/legacykeywithoutformat");
        result.Value.Should().NotContain("?exp=");
    }

    [Fact]
    public void GetUrl_PublicAssets_ShouldUseRawForPdfAndTransformedImageCdnForImages()
    {
        var storage = CreateCloudinaryStorage();

        var imageUrl = storage.GetUrl(
            "vehicles/v1/01936f00aabbccddeeff001122334455.webp",
            StorageVisibility.Public,
            transform: new ImageTransform(Width: 800, Height: 600, Crop: "fill", Quality: 85));
        imageUrl.IsSuccess.Should().BeTrue();
        imageUrl.Value.Should().StartWith("https://res.cloudinary.com/demo-cloud/image/upload/");
        imageUrl.Value.Should().Contain("c_fill").And.Contain("h_600").And.Contain("q_85").And.Contain("w_800");
        imageUrl.Value.Should().EndWith("/vehicles/v1/01936f00aabbccddeeff001122334455.webp");

        var pdfUrl = storage.GetUrl(
            "support/t1/01936f00aabbccddeeff001122334455.pdf",
            StorageVisibility.Public,
            transform: new ImageTransform(Width: 800));
        pdfUrl.IsSuccess.Should().BeTrue();
        pdfUrl.Value.Should().StartWith("https://res.cloudinary.com/demo-cloud/raw/upload/");
        pdfUrl.Value.Should().EndWith("/support/t1/01936f00aabbccddeeff001122334455.pdf");
        pdfUrl.Value.Should().NotContain("w_800");
    }

    private static IImageStorage CreateCloudinaryStorage()
    {
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Cloudinary:CloudName"] = "demo-cloud",
                ["Cloudinary:ApiKey"] = "1234567890",
                ["Cloudinary:ApiSecret"] = "super-secret-key",
                ["Cloudinary:DefaultSignedUrlMinutes"] = "5",
                ["Cloudinary:MaxSignedUrlMinutes"] = "15"
            })
            .Build();

        var services = new ServiceCollection();
        services.AddLogging();
        services.AddSingleton<IConfiguration>(config);
        services.AddSingleton<TimeProvider>(new FixedTimeProvider(FixedNow));
        services.AddImageStorage(config, new FakeHostEnvironment("Production"));

        var provider = services.BuildServiceProvider();
        return provider.GetRequiredService<IImageStorage>();
    }

    private sealed class FixedTimeProvider(DateTimeOffset utcNow) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => utcNow;
    }

    private sealed class FakeHostEnvironment(string environmentName) : IHostEnvironment
    {
        public string EnvironmentName { get; set; } = environmentName;
        public string ApplicationName { get; set; } = "CarRental.Api";
        public string ContentRootPath { get; set; } = AppContext.BaseDirectory;
        public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
    }
}
