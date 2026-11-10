using CarRental.BuildingBlocks.Results;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;

namespace CarRental.BuildingBlocks.Tests;

public class ResultHttpExtensionsTests
{
    [Fact]
    public void ToHttpResult_OnSuccessResult_ReturnsNoContent()
    {
        var result = Result.Success();

        var httpResult = result.ToHttpResult();

        httpResult.Should().BeOfType<NoContent>();
    }

    [Fact]
    public void ToHttpResult_OnSuccessTypedResult_ReturnsOkWithValue()
    {
        var result = Result<string>.Success("test-payload");

        var httpResult = result.ToHttpResult();

        var okResult = httpResult.Should().BeOfType<Ok<string>>().Subject;
        okResult.Value.Should().Be("test-payload");
    }

    [Fact]
    public void ToCreatedHttpResult_OnSuccess_ReturnsCreatedWithLocationAndValue()
    {
        var result = Result<int>.Success(123);

        var httpResult = result.ToCreatedHttpResult(id => $"/items/{id}");

        var createdResult = httpResult.Should().BeOfType<Created<int>>().Subject;
        createdResult.Location.Should().Be("/items/123");
        createdResult.Value.Should().Be(123);
    }

    [Theory]
    [InlineData(ErrorType.Validation, StatusCodes.Status400BadRequest, "Validation Error")]
    [InlineData(ErrorType.Unauthorized, StatusCodes.Status401Unauthorized, "Unauthorized")]
    [InlineData(ErrorType.Forbidden, StatusCodes.Status403Forbidden, "Forbidden")]
    [InlineData(ErrorType.NotFound, StatusCodes.Status404NotFound, "Resource Not Found")]
    [InlineData(ErrorType.Conflict, StatusCodes.Status409Conflict, "Resource Conflict")]
    [InlineData(ErrorType.Failure, StatusCodes.Status400BadRequest, "Bad Request")]
    public void ToProblemResult_MapsErrorTypeToExpectedStatusCodeAndTitle(ErrorType errorType, int expectedStatus, string expectedTitle)
    {
        var error = new Error("test.code", "Detailed error message", errorType);

        var httpResult = error.ToProblemResult();

        var problemResult = httpResult.Should().BeOfType<ProblemHttpResult>().Subject;
        problemResult.StatusCode.Should().Be(expectedStatus);
        problemResult.ProblemDetails.Title.Should().Be(expectedTitle);
        problemResult.ProblemDetails.Detail.Should().Be("Detailed error message");
        problemResult.ProblemDetails.Extensions.Should().ContainKey("errorCode")
            .WhoseValue.Should().Be("test.code");
    }

    [Fact]
    public void PagedResult_CalculatesPaginationMetadataCorrectly()
    {
        var items = new List<string> { "item1", "item2" };
        var paged = new PagedResult<string>(items, TotalCount: 25, Page: 2, PageSize: 10);

        paged.Items.Should().HaveCount(2);
        paged.TotalCount.Should().Be(25);
        paged.Page.Should().Be(2);
        paged.PageSize.Should().Be(10);
        paged.TotalPages.Should().Be(3);
        paged.HasPreviousPage.Should().BeTrue();
        paged.HasNextPage.Should().BeTrue();
    }

    [Fact]
    public void PagedResult_EdgeCases_FirstAndLastPage()
    {
        var firstPage = new PagedResult<int>([], TotalCount: 10, Page: 1, PageSize: 10);
        firstPage.TotalPages.Should().Be(1);
        firstPage.HasPreviousPage.Should().BeFalse();
        firstPage.HasNextPage.Should().BeFalse();

        var zeroSize = new PagedResult<int>([], TotalCount: 10, Page: 1, PageSize: 0);
        zeroSize.TotalPages.Should().Be(0);
        zeroSize.HasNextPage.Should().BeFalse();
    }
}

