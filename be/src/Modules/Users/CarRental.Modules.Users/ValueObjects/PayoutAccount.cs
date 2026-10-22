using CarRental.BuildingBlocks.Domain;

namespace CarRental.Modules.Users.ValueObjects;

public sealed record PayoutAccount : ValueObject
{
    public string BankName { get; }
    public string AccountNumber { get; }
    public string AccountHolderName { get; }

    public PayoutAccount(string bankName, string accountNumber, string accountHolderName)
    {
        Guard.AgainstNullOrWhiteSpace(bankName, nameof(bankName), "Bank name is required.");
        Guard.AgainstNullOrWhiteSpace(accountNumber, nameof(accountNumber), "Account number is required.");
        Guard.AgainstNullOrWhiteSpace(accountHolderName, nameof(accountHolderName), "Account holder name is required.");

        BankName = bankName.Trim();
        AccountNumber = accountNumber.Trim();
        AccountHolderName = accountHolderName.Trim().ToUpperInvariant();
    }

    private PayoutAccount()
    {
        BankName = string.Empty;
        AccountNumber = string.Empty;
        AccountHolderName = string.Empty;
    }
}
