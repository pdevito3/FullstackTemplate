namespace FullstackTemplate.FunctionalTests.FunctionalTests.Pagination;

using System.Globalization;
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

    public static IEnumerable<(string, string)> QueryValuesThatCannotBeRead =>
    [
        ("filters", "Name @= "),
        ("filters", "Unknown == \"x\""),
        // TenantDto does not show LastModifiedOn, so the filter cannot use it
        ("filters", "LastModifiedOn == \"x\""),
        ("filters", $"Name == \"{new string('a', CustomQueryKitConfiguration.FilterMaxLength)}\""),
        ("filters", $"{new string('(', OverNestingLimit)}Name == \"x\"{new string(')', OverNestingLimit)}"),
        ("filters", "Name ^^ \"x\""),
        // The Server sets no DbContext type for QueryKit, so it cannot run the sounds-like operators
        ("filters", "Name ~~ \"paul\""),
        ("filters", "Name !~ \"paul\""),
        ("sortOrder", "Unknown desc,,"),
        ("sortOrder", "Name foo"),
        // A version is opaque, so a list cannot filter or sort on it
        ("filters", "version > 5"),
        ("filters", "Version == 5"),
        ("sortOrder", "version"),
        ("sortOrder", "Name, -version"),
        // Model binding gives these, so the key comes from the name of the query parameter
        ("pageNumber", "abc"),
        ("pageSize", "abc"),
        // A page starts at 1, and a page has at least one row
        ("pageNumber", "0"),
        ("pageNumber", "-1"),
        ("pageSize", "0"),
        ("pageSize", "-1"),
        // The offset of a page must fit in an int
        ("pageNumber", int.MaxValue.ToString(CultureInfo.InvariantCulture)),
        ("pageNumber", (BasePaginationParameters.MaxPageNumber + 1).ToString(CultureInfo.InvariantCulture)),
    ];

    [Test]
    [MethodDataSource(nameof(QueryValuesThatCannotBeRead))]
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

    [Test]
    [Arguments("filters", "Name == a\"b",
        "The filter could not be read at character 10. Check the operator and the quotes there, then try again.")]
    [Arguments("filters", "Title == \"x\"",
        "The filter names the field Title, which this list does not have. Use one of these fields: id, name.")]
    [Arguments("sortOrder", "Title desc",
        "The sort order names the field Title, which this list does not have. Use one of these fields: id, name.")]
    public async Task query_value_that_querykit_cannot_read_gets_a_message_that_points_to_the_fault(
        string parameter, string value, string message)
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
        var problem = (await result.Content.ReadFromJsonAsync<ValidationProblemDetails>()).ShouldNotBeNull();
        problem.Detail.ShouldBe(message);
        problem.Errors[parameter].ShouldBe([message]);
    }

    [Test]
    public async Task page_number_that_is_not_a_number_names_the_value_and_the_field()
    {
        // Arrange
        FactoryClient.SetFakeBearerToken(new Dictionary<string, object>
        {
            ["client_id"] = TestingWebApplicationFactory.TrustedMachineClientId
        });

        // Act
        var result = await FactoryClient.GetRequestAsync($"{ApiRoutes.Tenants.GetList}?pageNumber=abc&pageSize=xyz");

        // Assert
        result.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        var problem = (await result.Content.ReadFromJsonAsync<ValidationProblemDetails>()).ShouldNotBeNull();
        problem.Detail.ShouldBe("The value 'abc' is not valid for pageNumber.");
        // The framework stops at the first value that does not bind
        problem.Errors.ShouldHaveSingleItem().Value.ShouldBe(["The value 'abc' is not valid for pageNumber."]);
    }

    [Test]
    public async Task page_out_of_range_names_each_field_in_its_message()
    {
        // Arrange
        FactoryClient.SetFakeBearerToken(new Dictionary<string, object>
        {
            ["client_id"] = TestingWebApplicationFactory.TrustedMachineClientId
        });

        // Act
        var result = await FactoryClient.GetRequestAsync($"{ApiRoutes.Tenants.GetList}?pageNumber=0&pageSize=0");

        // Assert
        result.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        var problem = (await result.Content.ReadFromJsonAsync<ValidationProblemDetails>()).ShouldNotBeNull();
        problem.Title.ShouldBe("One or more validation errors occurred.");
        problem.Errors.Keys.ShouldBe(["pageNumber", "pageSize"], ignoreOrder: true);
        problem.Errors["pageNumber"].ShouldBe(["The field pageNumber must be between 1 and 4294967."]);
        problem.Errors["pageSize"].ShouldBe(["The field pageSize must be between 1 and 2147483647."]);
    }

    [Test]
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
