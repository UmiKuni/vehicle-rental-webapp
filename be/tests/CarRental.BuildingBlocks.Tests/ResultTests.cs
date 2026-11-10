using CarRental.BuildingBlocks.Results;

namespace CarRental.BuildingBlocks.Tests;

public class ResultTests
{
    [Fact]
    public void Success_ReturnsSuccessfulResult()
    {
        var result = Result.Success();

        result.IsSuccess.Should().BeTrue();
        result.IsFailure.Should().BeFalse();
        result.Error.Should().Be(Error.None);
    }

    [Fact]
    public void Failure_ReturnsFailureResult()
    {
        var error = new Error("test.error", "Something went wrong");
        var result = Result.Failure(error);

        result.IsSuccess.Should().BeFalse();
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be(error);
    }

    [Fact]
    public void TypedResult_Success_ContainsValue()
    {
        var result = Result<int>.Success(42);

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().Be(42);
        result.Error.Should().Be(Error.None);
    }

    [Fact]
    public void TypedResult_Failure_ThrowsWhenAccessingValue()
    {
        var result = Result<int>.Failure(new Error("test.error", "Error message"));

        result.IsFailure.Should().BeTrue();
        var act = () => _ = result.Value;

        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*Cannot access the value of a failure result.*");
    }

    [Fact]
    public void ImplicitConversions_WorkAsExpected()
    {
        Result<string> successResult = "hello";
        successResult.IsSuccess.Should().BeTrue();
        successResult.Value.Should().Be("hello");

        var error = new Error("error.code", "error msg");
        Result<string> failureResult = error;
        failureResult.IsFailure.Should().BeTrue();
        failureResult.Error.Should().Be(error);

        Result nonGenericResult = successResult;
        nonGenericResult.IsSuccess.Should().BeTrue();

        Result nonGenericFailure = failureResult;
        nonGenericFailure.IsFailure.Should().BeTrue();
    }

    [Fact]
    public void Map_TransformsValueOnSuccess()
    {
        var result = Result<int>.Success(10);

        var mapped = result.Map(x => x * 2);

        mapped.IsSuccess.Should().BeTrue();
        mapped.Value.Should().Be(20);
    }

    [Fact]
    public void Map_PropagatesErrorOnFailure()
    {
        var error = new Error("error", "msg");
        var result = Result<int>.Failure(error);

        var mapped = result.Map(x => x * 2);

        mapped.IsFailure.Should().BeTrue();
        mapped.Error.Should().Be(error);
    }

    [Fact]
    public void Bind_ChainsOperationsOnSuccess()
    {
        var result = Result<string>.Success("42");

        var bound = result.Bind(str => int.TryParse(str, out var num)
            ? Result<int>.Success(num)
            : Result<int>.Failure(new Error("parse.error", "Failed to parse")));

        bound.IsSuccess.Should().BeTrue();
        bound.Value.Should().Be(42);
    }

    [Fact]
    public void Match_ExecutesSuccessBranchOnSuccess()
    {
        var result = Result<int>.Success(100);

        var outcome = result.Match(
            onSuccess: val => $"Value is {val}",
            onFailure: err => $"Error: {err.Message}");

        outcome.Should().Be("Value is 100");
    }

    [Fact]
    public void Match_ExecutesFailureBranchOnFailure()
    {
        var result = Result<int>.Failure(new Error("err", "Failed"));

        var outcome = result.Match(
            onSuccess: val => $"Value is {val}",
            onFailure: err => $"Error: {err.Message}");

        outcome.Should().Be("Error: Failed");
    }
}
