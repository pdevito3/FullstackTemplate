namespace FullstackTemplate.UnitTests.Resources;

using System.Text.Json;
using FullstackTemplate.Server.Resources;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Abstractions;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.AspNetCore.Routing;
using Shouldly;

public class PaginationHeaderFilterTests
{
    [Test]
    public void filter_adds_the_page_data_of_a_paged_list()
    {
        // Arrange
        var pagedList = PagedList.Create(Enumerable.Range(1, 3).AsQueryable(), 2, 1);
        var (context, headers) = ContextFor(new OkObjectResult(pagedList));

        // Act
        new PaginationHeaderFilter().OnResultExecuting(context);

        // Assert
        var metadata = JsonSerializer.Deserialize<PaginationMetadata>(
            headers[PaginationMetadata.HeaderName].ToString(),
            JsonSerializerOptions.Web).ShouldNotBeNull();
        metadata.PageNumber.ShouldBe(2);
        metadata.TotalCount.ShouldBe(3);
        metadata.TotalPages.ShouldBe(3);
    }

    public static readonly IEnumerable<IActionResult> ResultsThatAreNotAPagedList =
    [
        new OkObjectResult(new[] { 1, 2 }),
        new OkObjectResult(null),
        new NoContentResult(),
    ];

    [Test]
    [MethodDataSource(nameof(ResultsThatAreNotAPagedList))]
    public void filter_adds_no_header_to_a_result_that_is_not_a_paged_list(IActionResult result)
    {
        // Arrange
        var (context, headers) = ContextFor(result);

        // Act
        new PaginationHeaderFilter().OnResultExecuting(context);

        // Assert
        headers.ContainsKey(PaginationMetadata.HeaderName).ShouldBeFalse();
    }

    private static (ResultExecutingContext Context, IHeaderDictionary Headers) ContextFor(IActionResult result)
    {
        var httpContext = new DefaultHttpContext();
        var context = new ResultExecutingContext(
            new ActionContext(httpContext, new RouteData(), new ActionDescriptor()),
            [],
            result,
            new object());
        return (context, httpContext.Response.Headers);
    }
}
