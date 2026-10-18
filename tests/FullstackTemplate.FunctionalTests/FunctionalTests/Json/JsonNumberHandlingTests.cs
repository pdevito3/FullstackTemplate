namespace FullstackTemplate.FunctionalTests.FunctionalTests.Json;

using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Shouldly;

// The contract types numbers only as numbers, but the Server keeps the ASP.NET default and also reads a number in a JSON string.
// No request DTO has a number field yet, so these tests read a body with the JSON options of the running app.
public class JsonNumberHandlingTests : TestBase
{
    private sealed record BodyWithNumber(int Count);

    private const string NumberInString = """{ "count": "5" }""";

    [Fact]
    public async Task controllers_read_a_number_in_a_json_string()
    {
        // Arrange
        var options = await ExecuteScopeAsync(sp =>
            Task.FromResult(sp.GetRequiredService<IOptions<Microsoft.AspNetCore.Mvc.JsonOptions>>().Value.JsonSerializerOptions));

        // Act
        var body = JsonSerializer.Deserialize<BodyWithNumber>(NumberInString, options);

        // Assert
        body.ShouldNotBeNull().Count.ShouldBe(5);
    }

    [Fact]
    public async Task minimal_apis_read_a_number_in_a_json_string()
    {
        // Arrange
        var options = await ExecuteScopeAsync(sp =>
            Task.FromResult(sp.GetRequiredService<IOptions<Microsoft.AspNetCore.Http.Json.JsonOptions>>().Value.SerializerOptions));

        // Act
        var body = JsonSerializer.Deserialize<BodyWithNumber>(NumberInString, options);

        // Assert
        body.ShouldNotBeNull().Count.ShouldBe(5);
    }
}
