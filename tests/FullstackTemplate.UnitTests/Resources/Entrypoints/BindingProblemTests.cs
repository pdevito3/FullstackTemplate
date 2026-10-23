namespace FullstackTemplate.UnitTests.Resources.Entrypoints;

using System.Text.Json;
using FullstackTemplate.Server.Resources.Entrypoints.Http;
using Microsoft.AspNetCore.Http;
using Shouldly;

public class BindingProblemTests
{
    [Test]
    public void each_missing_required_member_is_an_error_under_its_json_name()
    {
        // Arrange
        var exception = BindingFailureFor("{}");

        // Act
        var errors = BindingProblem.ErrorsFor(exception);
        var detail = BindingProblem.DetailFor(exception);

        // Assert
        errors.Keys.ShouldBe(["version", "startsOn"]);
        errors["version"].ShouldBe(["The version field is required."]);
        errors["startsOn"].ShouldBe(["The startsOn field is required."]);
        detail.ShouldBe("The version field is required. The startsOn field is required.");
        detail.ShouldNotContain(nameof(TwoRequiredMembers));
    }

    private static BadHttpRequestException BindingFailureFor(string body)
    {
        var options = new JsonSerializerOptions(JsonSerializerDefaults.Web);
        var jsonException = Should.Throw<JsonException>(() => JsonSerializer.Deserialize<TwoRequiredMembers>(body, options));
        return new BadHttpRequestException("Failed to read parameter \"TwoRequiredMembers dto\" from the request body as JSON.", jsonException);
    }

    private sealed record TwoRequiredMembers
    {
        public required uint Version { get; init; }
        public required DateOnly StartsOn { get; init; }
    }
}
