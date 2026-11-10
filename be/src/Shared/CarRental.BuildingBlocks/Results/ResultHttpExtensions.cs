using Microsoft.AspNetCore.Http;
using HttpResults = Microsoft.AspNetCore.Http.Results;

namespace CarRental.BuildingBlocks.Results;

public static class ResultHttpExtensions
{
    public static IResult ToHttpResult(this Result result) =>
        result.IsSuccess ? HttpResults.NoContent() : result.Error.ToProblemResult();

    public static IResult ToHttpResult<T>(this Result<T> result) =>
        result.IsSuccess ? HttpResults.Ok(result.Value) : result.Error.ToProblemResult();

    public static IResult ToCreatedHttpResult<T>(this Result<T> result, Func<T, string> locationFactory) =>
        result.IsSuccess
            ? HttpResults.Created(locationFactory(result.Value), result.Value)
            : result.Error.ToProblemResult();

    public static IResult ToProblemResult(this Error error)
    {
        var (statusCode, title) = error.Type switch
        {
            ErrorType.Validation   => (StatusCodes.Status400BadRequest, "Validation Error"),
            ErrorType.Unauthorized => (StatusCodes.Status401Unauthorized, "Unauthorized"),
            ErrorType.Forbidden    => (StatusCodes.Status403Forbidden, "Forbidden"),
            ErrorType.NotFound     => (StatusCodes.Status404NotFound, "Resource Not Found"),
            ErrorType.Conflict     => (StatusCodes.Status409Conflict, "Resource Conflict"),
            _                      => (StatusCodes.Status400BadRequest, "Bad Request")
        };

        return HttpResults.Problem(
            title: title,
            detail: error.Message,
            statusCode: statusCode,
            extensions: new Dictionary<string, object?>
            {
                ["errorCode"] = error.Code
            });
    }
}
