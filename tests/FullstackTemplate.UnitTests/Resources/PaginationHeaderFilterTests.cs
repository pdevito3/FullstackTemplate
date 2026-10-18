namespace FullstackTemplate.UnitTests.Resources;

using FullstackTemplate.Server.Resources;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Abstractions;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.AspNetCore.Routing;
using Microsoft.Net.Http.Headers;
using Shouldly;

public class PaginationHeaderFilterTests
{
    [Fact]
    public void filter_keeps_the_headers_that_another_component_exposed()
    {
        // Arrange
        var httpContext = new DefaultHttpContext();
        httpContext.Response.Headers.Append(HeaderNames.AccessControlExposeHeaders, "X-Other");
        var pagedList = PagedList<int>.Create(new[] { 1, 2 }.AsQueryable(), 1, 1);
        var context = new ResultExecutingContext(
            new ActionContext(httpContext, new RouteData(), new ActionDescriptor()),
            [],
            new OkObjectResult(pagedList),
            new object());

        // Act
        new PaginationHeaderFilter().OnResultExecuting(context);

        // Assert
        httpContext.Response.Headers[HeaderNames.AccessControlExposeHeaders]
            .ToArray()
            .ShouldBe(["X-Other", PaginationMetadata.HeaderName]);
    }
}
