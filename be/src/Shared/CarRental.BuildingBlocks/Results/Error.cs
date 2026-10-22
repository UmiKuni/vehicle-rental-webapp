namespace CarRental.BuildingBlocks.Results;

/// <summary>
/// Represents an application or domain error with a machine-readable code and human-readable message.
/// </summary>
public sealed record Error(string Code, string Message)
{
    public static readonly Error None = new(string.Empty, string.Empty);
    public static readonly Error NullValue = new("Error.NullValue", "The specified result value is null.");

    public static Error NotFound(string code, string message) => new(code, message);
    public static Error Validation(string code, string message) => new(code, message);
    public static Error Conflict(string code, string message) => new(code, message);
    public static Error Failure(string code, string message) => new(code, message);
    public static Error Unauthorized(string code, string message) => new(code, message);
    public static Error Forbidden(string code, string message) => new(code, message);
}
