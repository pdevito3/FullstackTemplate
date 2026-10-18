namespace FullstackTemplate.FunctionalTests.FunctionalTests.Pagination;

using System.Net;
using System.Net.Http.Json;
using FullstackTemplate.Server.Resources;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Shouldly;
using TestUtilities;

public class QueryInputErrorTests : TestBase
{
    private const int OverNestingLimit = CustomQueryKitConfiguration.FilterMaxNestingDepth + 1;

    public static readonly TheoryData<string, string> QueryValuesThatCannotBeRead = new()
    {
        { "filters", "Name @= " },
        { "filters", "Unknown == \"x\"" },
        // TenantDto does not show CreatedBy, so the filter cannot use it
        { "filters", "CreatedBy == \"x\"" },
        { "filters", $"Name == \"{new string('a', CustomQueryKitConfiguration.FilterMaxLength)}\"" },
        { "filters", $"{new string('(', OverNestingLimit)}Name == \"x\"{new string(')', OverNestingLimit)}" },
        { "filters", "Name ^^ \"x\"" },
        { "sortOrder", "Unknown desc,," },
        { "sortOrder", "Name foo" },
        // Model binding gives these, so the key comes from the name of the query parameter
        { "pageNumber", "abc" },
        { "pageSize", "abc" },
        // A page starts at 1, and a page has at least one row
        { "pageNumber", "0" },
        { "pageNumber", "-1" },
        { "pageSize", "0" },
        { "pageSize", "-1" },
        // The offset of a page must fit in an int
        { "pageNumber", int.MaxValue.ToString() },
        { "pageNumber", (BasePaginationParameters.MaxPageNumber + 1).ToString() },
    };

    [Theory]
    [MemberData(nameof(QueryValuesThatCannotBeRead))]
    public async Task query_value_that_cannot_be_read_returns_bad_request_problem(string parameter, string value)
    {
        // Arrange
        FactoryClient.SetFakeBearerToken(new Dictionary<string, object>
        {
            ["client_id"] = TestingWebApplicationFactory.TrustedMachineClientId
        });

        // Act
        var result = await FactoryClient.GetRequestAsync($"{ApiRoutes.Tenants.GetList}?{parameter}={Uri.EscapeDataString(value)}");

        // Assert
        result.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        result.Content.Headers.ContentType?.MediaType.ShouldBe("application/problem+json");
        var problem = (await result.Content.ReadFromJsonAsync<ValidationProblemDetails>()).ShouldNotBeNull();
        problem.Status.ShouldBe(StatusCodes.Status400BadRequest);
        problem.Errors.ShouldHaveSingleItem().Key.ShouldBe(parameter);
    }

    [Fact]
    public async Task last_page_number_with_the_largest_page_size_returns_an_empty_page()
    {
        // Arrange
        FactoryClient.SetFakeBearerToken(new Dictionary<string, object>
        {
            ["client_id"] = TestingWebApplicationFactory.TrustedMachineClientId
        });

        // Act
        var result = await FactoryClient.GetRequestAsync(
            $"{ApiRoutes.Tenants.GetList}?pageNumber={BasePaginationParameters.MaxPageNumber}&pageSize={BasePaginationParameters.MaxPageSizeLimit}");

        // Assert
        result.StatusCode.ShouldBe(HttpStatusCode.OK);
    }
}
