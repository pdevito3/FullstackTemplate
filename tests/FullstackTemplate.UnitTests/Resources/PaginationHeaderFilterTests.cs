namespace FullstackTemplate.UnitTests.Resources;

using System.Text.Json;
using FullstackTemplate.Server.Resources;
using FullstackTemplate.Server.Resources.Entrypoints.Http;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Shouldly;

public class PaginationHeaderFilterTests
{
    [Test]
    public async Task filter_adds_the_page_data_of_a_paged_list()
    {
        // Arrange
        var pagedList = PagedList.Create(Enumerable.Range(1, 3).AsQueryable(), 2, 1);

        // Act
        var headers = await HeadersAfterFilter(TypedResults.Ok(pagedList));

        // Assert
        var metadata = JsonSerializer.Deserialize<PaginationMetadata>(
            headers[PaginationMetadata.HeaderName].ToString(),
            JsonSerializerOptions.Web).ShouldNotBeNull();
        metadata.PageNumber.ShouldBe(2);
        metadata.TotalCount.ShouldBe(3);
        metadata.TotalPages.ShouldBe(3);
    }

    [Test]
    public async Task filter_names_each_page_field_in_camel_case()
    {
        // Arrange
        var pagedList = PagedList.Create(Enumerable.Range(1, 3).AsQueryable(), 1, 2);

        // Act
        var headers = await HeadersAfterFilter(TypedResults.Ok(pagedList));

        // Assert
        using var header = JsonDocument.Parse(headers[PaginationMetadata.HeaderName].ToString());
        header.RootElement.EnumerateObject().Select(p => p.Name).ShouldBe(
        [
            "totalCount", "pageSize", "currentPageSize", "currentStartIndex", "currentEndIndex",
            "pageNumber", "totalPages", "hasPrevious", "hasNext",
        ], ignoreOrder: true);
    }

    [Test]
    public async Task filter_reads_the_inner_result_of_a_union_result()
    {
        // Arrange
        Results<Ok<PagedList<int>>, NotFound> result = TypedResults.Ok(PagedList.Create(Enumerable.Range(1, 3).AsQueryable(), 1, 2));

        // Act
        var headers = await HeadersAfterFilter(result);

        // Assert
        headers.ContainsKey(PaginationMetadata.HeaderName).ShouldBeTrue();
    }

    public static readonly IEnumerable<object?> ResultsThatAreNotAPagedList =
    [
        TypedResults.Ok(new[] { 1, 2 }),
        TypedResults.Ok<object?>(null),
        TypedResults.NoContent(),
        new[] { 1, 2 },
        null,
    ];

    [Test]
    [MethodDataSource(nameof(ResultsThatAreNotAPagedList))]
    public async Task filter_adds_no_header_to_a_result_that_is_not_a_paged_list(object? result)
    {
        // Act
        var headers = await HeadersAfterFilter(result);

        // Assert
        headers.ContainsKey(PaginationMetadata.HeaderName).ShouldBeFalse();
    }

    private static async Task<IHeaderDictionary> HeadersAfterFilter(object? result)
    {
        var httpContext = new DefaultHttpContext();
        var context = EndpointFilterInvocationContext.Create(httpContext);

        var returned = await new PaginationHeaderFilter().InvokeAsync(context, _ => ValueTask.FromResult(result));

        returned.ShouldBeSameAs(result);
        return httpContext.Response.Headers;
    }
}
