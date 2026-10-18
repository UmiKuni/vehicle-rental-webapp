using System.Diagnostics.CodeAnalysis;

namespace CarRental.BuildingBlocks.Results;

/// <summary>
/// Represents the outcome of an operation without a return value.
/// </summary>
public readonly record struct Result
{
    public bool IsSuccess { get; }
    public Error Error { get; }
    public bool IsFailure => !IsSuccess;

    private Result(bool isSuccess, Error error)
    {
        if (isSuccess && error != Error.None)
        {
            throw new InvalidOperationException("A successful result cannot have an error.");
        }

        if (!isSuccess && error == Error.None)
        {
            throw new InvalidOperationException("A failure result must specify an error.");
        }

        IsSuccess = isSuccess;
        Error = error;
    }

    public static Result Success() => new(true, Error.None);

    public static Result Failure(Error error) => new(false, error);

    public static Result<T> Success<T>(T value) => Result<T>.Success(value);

    public static Result<T> Failure<T>(Error error) => Result<T>.Failure(error);

    public static implicit operator Result(Error error) => Failure(error);

    public TOut Match<TOut>(Func<TOut> onSuccess, Func<Error, TOut> onFailure)
    {
        ArgumentNullException.ThrowIfNull(onSuccess);
        ArgumentNullException.ThrowIfNull(onFailure);

        return IsSuccess ? onSuccess() : onFailure(Error);
    }

    public Result Bind(Func<Result> binder)
    {
        ArgumentNullException.ThrowIfNull(binder);
        return IsSuccess ? binder() : this;
    }

    public Result<TOut> Bind<TOut>(Func<Result<TOut>> binder)
    {
        ArgumentNullException.ThrowIfNull(binder);
        return IsSuccess ? binder() : Result<TOut>.Failure(Error);
    }
}

/// <summary>
/// Represents the outcome of an operation with a strongly-typed return value.
/// </summary>
/// <typeparam name="T">The type of the encapsulated value.</typeparam>
public readonly record struct Result<T>
{
    private readonly T? _value;

    public bool IsSuccess { get; }
    public Error Error { get; }
    public bool IsFailure => !IsSuccess;

    public T Value
    {
        get
        {
            if (IsFailure)
            {
                throw new InvalidOperationException("Cannot access the value of a failure result.");
            }

            return _value!;
        }
    }

    private Result(bool isSuccess, T? value, Error error)
    {
        if (isSuccess && error != Error.None)
        {
            throw new InvalidOperationException("A successful result cannot have an error.");
        }

        if (!isSuccess && error == Error.None)
        {
            throw new InvalidOperationException("A failure result must specify an error.");
        }

        IsSuccess = isSuccess;
        _value = value;
        Error = error;
    }

    public static Result<T> Success(T value) => new(true, value, Error.None);

    public static Result<T> Failure(Error error) => new(false, default, error);

    public static implicit operator Result<T>(T value) =>
        value is not null ? Success(value) : Failure(Error.NullValue);

    public static implicit operator Result<T>(Error error) => Failure(error);

    public static implicit operator Result(Result<T> result) =>
        result.IsSuccess ? Result.Success() : Result.Failure(result.Error);

    public TOut Match<TOut>(Func<T, TOut> onSuccess, Func<Error, TOut> onFailure)
    {
        ArgumentNullException.ThrowIfNull(onSuccess);
        ArgumentNullException.ThrowIfNull(onFailure);

        return IsSuccess ? onSuccess(Value) : onFailure(Error);
    }

    public Result<TOut> Map<TOut>(Func<T, TOut> mapper)
    {
        ArgumentNullException.ThrowIfNull(mapper);
        return IsSuccess ? Result<TOut>.Success(mapper(Value)) : Result<TOut>.Failure(Error);
    }

    public Result<TOut> Bind<TOut>(Func<T, Result<TOut>> binder)
    {
        ArgumentNullException.ThrowIfNull(binder);
        return IsSuccess ? binder(Value) : Result<TOut>.Failure(Error);
    }

    public Result Bind(Func<T, Result> binder)
    {
        ArgumentNullException.ThrowIfNull(binder);
        return IsSuccess ? binder(Value) : Result.Failure(Error);
    }
}
