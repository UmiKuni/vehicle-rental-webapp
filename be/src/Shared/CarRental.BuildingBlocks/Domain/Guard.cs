using System.Diagnostics.CodeAnalysis;
using CarRental.BuildingBlocks.Results;

namespace CarRental.BuildingBlocks.Domain;

/// <summary>
/// Domain assertion and invariant guard helpers.
/// Throws DomainException on invariant violations.
/// </summary>
public static class Guard
{
    /// <summary>
    /// Throws a DomainException if the specified condition is true.
    /// </summary>
    public static void Against(bool condition, string code, string message)
    {
        if (condition)
        {
            throw new DomainException(code, message);
        }
    }

    /// <summary>
    /// Throws a DomainException if the specified value is null.
    /// </summary>
    [return: NotNull]
    public static T AgainstNull<T>([NotNull] T? value, string code, string message)
    {
        if (value is null)
        {
            throw new DomainException(code, message);
        }

        return value;
    }

    /// <summary>
    /// Throws a DomainException if the specified string is null or consists only of white-space characters.
    /// </summary>
    [return: NotNull]
    public static string AgainstNullOrWhiteSpace([NotNull] string? value, string code, string message)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new DomainException(code, message);
        }

        return value;
    }

    /// <summary>
    /// Throws a DomainException if the specified value is strictly less than min or strictly greater than max.
    /// </summary>
    public static T AgainstOutOfRange<T>(T value, T min, T max, string code, string message) where T : IComparable<T>
    {
        if (value.CompareTo(min) < 0 || value.CompareTo(max) > 0)
        {
            throw new DomainException(code, message);
        }

        return value;
    }
}
