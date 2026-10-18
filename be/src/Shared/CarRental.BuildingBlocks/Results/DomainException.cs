namespace CarRental.BuildingBlocks.Results;

/// <summary>
/// Exception thrown when a domain invariant is violated or an unexpected programmer-level assertion fails.
/// </summary>
public class DomainException : Exception
{
    public string Code { get; }

    public DomainException(string code, string message) : base(message)
    {
        Code = code;
    }

    public DomainException(string code, string message, Exception? innerException) : base(message, innerException)
    {
        Code = code;
    }
}
