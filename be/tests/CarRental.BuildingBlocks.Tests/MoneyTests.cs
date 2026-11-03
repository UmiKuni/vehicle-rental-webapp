using CarRental.BuildingBlocks.Domain.ValueObjects;
using CarRental.BuildingBlocks.Results;

namespace CarRental.BuildingBlocks.Tests;

public class MoneyTests
{
    [Fact]
    public void Create_ValidAmountAndCurrency_ReturnsSuccess()
    {
        var result = Money.Create(100_000, "VND");

        result.IsSuccess.Should().BeTrue();
        result.Value.Amount.Should().Be(100_000);
        result.Value.Currency.Should().Be("VND");
    }

    [Fact]
    public void Create_NegativeAmount_ReturnsFailure()
    {
        var result = Money.Create(-1, "VND");

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("money.negative_amount");
    }

    [Theory]
    [InlineData("")]
    [InlineData(" ")]
    [InlineData("VN")]
    [InlineData("VIETNAM")]
    public void Create_InvalidCurrency_ReturnsFailure(string currency)
    {
        var result = Money.Create(50_000, currency);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("money.invalid_currency");
    }

    [Fact]
    public void Constructor_NegativeAmount_ThrowsDomainException()
    {
        var act = () => new Money(-500);

        act.Should().Throw<DomainException>()
            .Which.Code.Should().Be("money.negative_amount");
    }

    [Fact]
    public void Add_SameCurrency_ReturnsSummedMoney()
    {
        var m1 = new Money(100_000, "VND");
        var m2 = new Money(50_000, "VND");

        var sum = m1.Add(m2);

        sum.Amount.Should().Be(150_000);
        sum.Currency.Should().Be("VND");
        (m1 + m2).Should().Be(sum);
    }

    [Fact]
    public void Add_DifferentCurrency_ThrowsDomainException()
    {
        var m1 = new Money(100, "USD");
        var m2 = new Money(100_000, "VND");

        var act = () => m1.Add(m2);

        act.Should().Throw<DomainException>()
            .Which.Code.Should().Be("money.currency_mismatch");
    }

    [Fact]
    public void Subtract_ValidAmounts_ReturnsSubtractedMoney()
    {
        var m1 = new Money(100_000, "VND");
        var m2 = new Money(40_000, "VND");

        var diff = m1.Subtract(m2);

        diff.Amount.Should().Be(60_000);
        (m1 - m2).Should().Be(diff);
    }

    [Fact]
    public void Subtract_ResultingInNegative_ThrowsDomainException()
    {
        var m1 = new Money(40_000, "VND");
        var m2 = new Money(100_000, "VND");

        var act = () => m1.Subtract(m2);

        act.Should().Throw<DomainException>()
            .Which.Code.Should().Be("money.negative_result");
    }

    [Fact]
    public void Multiply_ValidFactor_CalculatesRoundedAmount()
    {
        var money = new Money(100_000, "VND");

        var result = money.Multiply(1.5m);

        result.Amount.Should().Be(150_000);
        (money * 1.5m).Should().Be(result);
    }

    [Fact]
    public void Multiply_NegativeFactor_ThrowsDomainException()
    {
        var money = new Money(100_000, "VND");

        var act = () => money.Multiply(-0.5m);

        act.Should().Throw<DomainException>()
            .Which.Code.Should().Be("money.negative_multiplier");
    }

    [Fact]
    public void Percent_CalculatesPercentageCorrectly()
    {
        var money = new Money(200_000, "VND");

        var discount = money.Percent(10m); // 10% of 200,000

        discount.Amount.Should().Be(20_000);
    }

    [Fact]
    public void ComparisonOperators_WorkCorrectly()
    {
        var m1 = new Money(100_000, "VND");
        var m2 = new Money(200_000, "VND");

        (m1 < m2).Should().BeTrue();
        (m2 > m1).Should().BeTrue();
        (m1 <= m2).Should().BeTrue();
        (m2 >= m1).Should().BeTrue();
        (m1 == new Money(100_000, "VND")).Should().BeTrue();
    }
}
