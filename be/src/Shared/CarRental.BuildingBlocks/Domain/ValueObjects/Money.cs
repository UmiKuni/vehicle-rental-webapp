using CarRental.BuildingBlocks.Domain;
using CarRental.BuildingBlocks.Results;

namespace CarRental.BuildingBlocks.Domain.ValueObjects;

/// <summary>
/// Value object representing a monetary amount in a specific currency.
/// Amount is stored in the currency's smallest unit (VND has no fractional part).
/// </summary>
public sealed record Money : ValueObject, IComparable<Money>
{
    public const string DefaultCurrency = "VND";

    public long Amount { get; init; }
    public string Currency { get; init; }

    /// <summary>
    /// Parameterless constructor required by EF Core.
    /// </summary>
    private Money()
    {
        Amount = 0;
        Currency = DefaultCurrency;
    }

    public Money(long amount, string currency = DefaultCurrency)
    {
        Guard.Against(amount < 0, "money.negative_amount", "Money amount cannot be negative.");
        Guard.AgainstNullOrWhiteSpace(currency, "money.invalid_currency", "Currency code cannot be empty.");

        var normalizedCurrency = currency.Trim().ToUpperInvariant();
        Guard.Against(normalizedCurrency.Length != 3, "money.invalid_currency", "Currency code must be exactly 3 characters.");

        Amount = amount;
        Currency = normalizedCurrency;
    }

    public static Result<Money> Create(long amount, string currency = DefaultCurrency)
    {
        if (amount < 0)
        {
            return Result<Money>.Failure(new Error("money.negative_amount", "Money amount cannot be negative."));
        }

        if (string.IsNullOrWhiteSpace(currency))
        {
            return Result<Money>.Failure(new Error("money.invalid_currency", "Currency code cannot be empty."));
        }

        var normalized = currency.Trim().ToUpperInvariant();
        if (normalized.Length != 3)
        {
            return Result<Money>.Failure(new Error("money.invalid_currency", "Currency code must be exactly 3 characters."));
        }

        return Result<Money>.Success(new Money(amount, normalized));
    }

    public static Money Zero(string currency = DefaultCurrency) => new(0, currency);

    public static Money Of(long amount, string currency = DefaultCurrency) => new(amount, currency);

    public Money Add(Money other)
    {
        ArgumentNullException.ThrowIfNull(other);
        EnsureSameCurrency(other);

        return new Money(Amount + other.Amount, Currency);
    }

    public Money Subtract(Money other)
    {
        ArgumentNullException.ThrowIfNull(other);
        EnsureSameCurrency(other);
        Guard.Against(Amount < other.Amount, "money.negative_result", "Subtraction would result in a negative amount.");

        return new Money(Amount - other.Amount, Currency);
    }

    public Money Multiply(decimal multiplier)
    {
        Guard.Against(multiplier < 0, "money.negative_multiplier", "Multiplier cannot be negative.");

        var newAmount = (long)Math.Round(Amount * multiplier, MidpointRounding.AwayFromZero);
        return new Money(newAmount, Currency);
    }

    public Money Percent(decimal percentage)
    {
        Guard.Against(percentage < 0, "money.negative_percentage", "Percentage cannot be negative.");

        return Multiply(percentage / 100m);
    }

    public static Money operator +(Money left, Money right) => left.Add(right);

    public static Money operator -(Money left, Money right) => left.Subtract(right);

    public static Money operator *(Money left, decimal multiplier) => left.Multiply(multiplier);

    public static Money operator *(decimal multiplier, Money right) => right.Multiply(multiplier);

    public static bool operator >(Money left, Money right)
    {
        ArgumentNullException.ThrowIfNull(left);
        ArgumentNullException.ThrowIfNull(right);
        left.EnsureSameCurrency(right);
        return left.Amount > right.Amount;
    }

    public static bool operator <(Money left, Money right)
    {
        ArgumentNullException.ThrowIfNull(left);
        ArgumentNullException.ThrowIfNull(right);
        left.EnsureSameCurrency(right);
        return left.Amount < right.Amount;
    }

    public static bool operator >=(Money left, Money right)
    {
        ArgumentNullException.ThrowIfNull(left);
        ArgumentNullException.ThrowIfNull(right);
        left.EnsureSameCurrency(right);
        return left.Amount >= right.Amount;
    }

    public static bool operator <=(Money left, Money right)
    {
        ArgumentNullException.ThrowIfNull(left);
        ArgumentNullException.ThrowIfNull(right);
        left.EnsureSameCurrency(right);
        return left.Amount <= right.Amount;
    }

    public int CompareTo(Money? other)
    {
        if (other is null) return 1;
        EnsureSameCurrency(other);
        return Amount.CompareTo(other.Amount);
    }

    private void EnsureSameCurrency(Money other)
    {
        if (!string.Equals(Currency, other.Currency, StringComparison.OrdinalIgnoreCase))
        {
            throw new DomainException(
                "money.currency_mismatch",
                $"Cannot perform operations across different currencies: '{Currency}' and '{other.Currency}'.");
        }
    }

    public override string ToString() => $"{Amount:N0} {Currency}";
}
