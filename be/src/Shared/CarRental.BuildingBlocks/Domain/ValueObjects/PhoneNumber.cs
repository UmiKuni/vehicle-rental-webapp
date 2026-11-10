using System.Text.RegularExpressions;
using CarRental.BuildingBlocks.Domain;
using CarRental.BuildingBlocks.Results;

namespace CarRental.BuildingBlocks.Domain.ValueObjects;

/// <summary>
/// Value object representing an E.164 international phone number.
/// Normalizes local Vietnamese numbers (e.g. 0901234567 -> +84901234567).
/// </summary>
public sealed partial record PhoneNumber : ValueObject
{
    private static readonly Regex E164Regex = MyRegex();
    private static readonly Regex SanitizerRegex = MySanitizerRegex();

    public string E164 { get; init; }

    /// <summary>
    /// Parameterless constructor required by EF Core.
    /// </summary>
    private PhoneNumber()
    {
        E164 = string.Empty;
    }

    public PhoneNumber(string rawNumber)
    {
        Guard.AgainstNullOrWhiteSpace(rawNumber, "phone_number.empty", "Phone number cannot be empty.");
        var normalized = Normalize(rawNumber);
        Guard.Against(!IsValidE164(normalized), "phone_number.invalid_format", $"Phone number '{rawNumber}' is not a valid international format.");

        E164 = normalized;
    }

    public static Result<PhoneNumber> Create(string rawNumber)
    {
        if (string.IsNullOrWhiteSpace(rawNumber))
        {
            return Result<PhoneNumber>.Failure(
                new Error("phone_number.empty", "Phone number cannot be empty."));
        }

        var normalized = Normalize(rawNumber);
        if (!IsValidE164(normalized))
        {
            return Result<PhoneNumber>.Failure(
                new Error("phone_number.invalid_format", $"Phone number '{rawNumber}' is not a valid international format."));
        }

        return Result<PhoneNumber>.Success(new PhoneNumber(normalized));
    }

    /// <summary>
    /// Normalizes raw phone number inputs into E.164 format.
    /// </summary>
    public static string Normalize(string rawNumber)
    {
        if (string.IsNullOrWhiteSpace(rawNumber))
        {
            return string.Empty;
        }

        // Remove whitespace, dashes, dots, parentheses
        var cleaned = SanitizerRegex.Replace(rawNumber.Trim(), string.Empty);

        // Normalize Vietnamese numbers starting with '0'
        if (cleaned.StartsWith('0'))
        {
            cleaned = "+84" + cleaned[1..];
        }
        // Normalize Vietnamese numbers starting with '84' without leading '+'
        else if (cleaned.StartsWith("84", StringComparison.Ordinal) && !cleaned.StartsWith("+84", StringComparison.Ordinal))
        {
            cleaned = "+" + cleaned;
        }
        else if (!cleaned.StartsWith('+'))
        {
            cleaned = "+" + cleaned;
        }

        return cleaned;
    }

    public static bool IsValidE164(string number)
    {
        if (string.IsNullOrWhiteSpace(number))
        {
            return false;
        }

        return E164Regex.IsMatch(number);
    }

    public static implicit operator string(PhoneNumber phone) => phone.E164;

    public override string ToString() => E164;

    [GeneratedRegex(@"^\+[1-9]\d{6,14}$")]
    private static partial Regex MyRegex();

    [GeneratedRegex(@"[\s\-\(\)\.]")]
    private static partial Regex MySanitizerRegex();
}
