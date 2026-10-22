using CarRental.BuildingBlocks.Domain.ValueObjects;
using CarRental.BuildingBlocks.Results;

namespace CarRental.BuildingBlocks.Tests;

public class ValueObjectsTests
{
    [Fact]
    public void GeoPoint_ValidCoordinates_CalculatesDistance()
    {
        // Hanoi coordinates: 21.028511, 105.804817
        var hanoi = new GeoPoint(21.028511, 105.804817);
        // HCMC coordinates: 10.823099, 106.629664
        var hcmc = new GeoPoint(10.823099, 106.629664);

        var distanceKm = hanoi.DistanceToInKilometers(hcmc);

        // Distance between Hanoi and HCMC is roughly ~1,130 - 1,170 km
        distanceKm.Should().BeInRange(1100, 1200);
    }

    [Theory]
    [InlineData(-91, 100)]
    [InlineData(91, 100)]
    [InlineData(0, -181)]
    [InlineData(0, 181)]
    public void GeoPoint_OutOfRangeCoordinates_ThrowsDomainException(double lat, double lon)
    {
        var act = () => new GeoPoint(lat, lon);

        act.Should().Throw<DomainException>();
    }

    [Fact]
    public void Address_ValidLineAndCity_ReturnsSuccess()
    {
        var address = new Address("123 Le Loi", "Ben Nghe", "District 1", "Ho Chi Minh City");

        address.Line.Should().Be("123 Le Loi");
        address.City.Should().Be("Ho Chi Minh City");
        address.Country.Should().Be("VN");
    }

    [Theory]
    [InlineData("", "HCMC")]
    [InlineData("123 Street", "")]
    public void Address_MissingRequiredFields_ThrowsDomainException(string line, string city)
    {
        var act = () => new Address(line, "Ward", "District", city);

        act.Should().Throw<DomainException>();
    }

    [Theory]
    [InlineData(1)]
    [InlineData(3)]
    [InlineData(5)]
    public void Rating_ValidValues_Succeeds(byte value)
    {
        var rating = new Rating(value);
        rating.Value.Should().Be(value);
        byte rawByte = rating;
        rawByte.Should().Be(value);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(6)]
    public void Rating_OutOfRangeValues_ThrowsDomainException(byte value)
    {
        var act = () => new Rating(value);

        act.Should().Throw<DomainException>()
            .Which.Code.Should().Be("rating.out_of_range");
    }

    [Fact]
    public void FileReference_PublicFile_AllowsUrl()
    {
        var file = FileReference.CreatePublic("cars/img1.jpg", "https://cdn.example.com/cars/img1.jpg", "image/jpeg", 1024);

        file.IsPrivate.Should().BeFalse();
        file.Url.Should().Be("https://cdn.example.com/cars/img1.jpg");
        file.StorageKey.Should().Be("cars/img1.jpg");
    }

    [Fact]
    public void FileReference_PrivateFile_ProhibitsPublicUrl()
    {
        var act = () => new FileReference("id-docs/cccd.pdf", "https://public.url/doc", true, "application/pdf", 2048);

        act.Should().Throw<DomainException>()
            .Which.Code.Should().Be("file_reference.private_url_forbidden");
    }

    [Fact]
    public void FileReference_PrivateFile_WithoutUrl_Succeeds()
    {
        var file = FileReference.CreatePrivate("id-docs/cccd.pdf", "application/pdf", 2048);

        file.IsPrivate.Should().BeTrue();
        file.Url.Should().BeNull();
    }
}
