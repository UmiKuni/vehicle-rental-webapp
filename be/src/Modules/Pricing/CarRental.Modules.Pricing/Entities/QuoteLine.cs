using CarRental.BuildingBlocks.Domain;
using CarRental.BuildingBlocks.Domain.ValueObjects;
using CarRental.Modules.Pricing.Contracts.Dtos;
using CarRental.Modules.Pricing.Contracts.Enums;

namespace CarRental.Modules.Pricing.Entities;

public sealed record QuoteLine : ValueObject
{
    public QuoteLineType Type { get; init; }
    public string Description { get; init; } = string.Empty;
    public Money Amount { get; init; } = Money.Zero();

    public QuoteLine(QuoteLineType type, string description, Money amount)
    {
        Type = type;
        Description = description;
        Amount = amount;
    }

    private QuoteLine() { }

    public QuoteLineDto ToDto() => new(Type.ToString(), Description, Amount);
}
