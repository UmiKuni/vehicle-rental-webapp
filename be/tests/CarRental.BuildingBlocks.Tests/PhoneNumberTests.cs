using CarRental.BuildingBlocks.Domain.ValueObjects;
using CarRental.BuildingBlocks.Results;

namespace CarRental.BuildingBlocks.Tests;

public class PhoneNumberTests
{
    [Theory]
    [InlineData("0901234567", "+84901234567")]
    [InlineData("090 123 4567", "+84901234567")]
    [InlineData("090-123-4567", "+84901234567")]
    [InlineData("84901234567", "+84901234567")]
    [InlineData("+84901234567", "+84901234567")]
    [InlineData("(+84) 90 123 4567", "+84901234567")]
    [InlineData("+14155552671", "+14155552671")]
    public void Normalize_ValidInputs_ReturnsNormalizedE164(string input, string expected)
    {
        var normalized = PhoneNumber.Normalize(input);
        normalized.Should().Be(expected);
    }

    [Fact]
    public void Create_ValidVietnameseNumber_ReturnsSuccess()
    {
        var result = PhoneNumber.Create("0901234567");

        result.IsSuccess.Should().BeTrue();
        result.Value.E164.Should().Be("+84901234567");
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Create_EmptyString_ReturnsEmptyError(string input)
    {
        var result = PhoneNumber.Create(input);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("phone_number.empty");
    }

    [Theory]
    [InlineData("not-a-number")]
    [InlineData("123")]
    [InlineData("+++000000")]
    public void Create_InvalidFormat_ReturnsFormatError(string input)
    {
        var result = PhoneNumber.Create(input);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("phone_number.invalid_format");
    }

    [Fact]
    public void Constructor_InvalidFormat_ThrowsDomainException()
    {
        var act = () => new PhoneNumber("invalid");

        act.Should().Throw<DomainException>()
            .Which.Code.Should().Be("phone_number.invalid_format");
    }
}
