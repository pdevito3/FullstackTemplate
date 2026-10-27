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

    [Test]
    public void value_of_the_wrong_type_in_a_child_is_an_error_under_its_path()
    {
        // Arrange
        var exception = BindingFailureFor<WithLines>("""{ "lines": [{ "hours": 1 }, { "hours": "two" }] }""");

        // Act
        var errors = BindingProblem.ErrorsFor(exception);
        var detail = BindingProblem.DetailFor(exception);

        // Assert
        errors.Keys.ShouldBe(["lines[1].hours"]);
        errors["lines[1].hours"].ShouldBe(["The lines[1].hours field is not valid."]);
        detail.ShouldBe("The lines[1].hours field is not valid.");
    }

    [Test]
    public void body_of_the_wrong_type_is_an_error_under_the_empty_key()
    {
        // Arrange
        var exception = BindingFailureFor<WithLines>("[]");

        // Act
        var errors = BindingProblem.ErrorsFor(exception);
        var detail = BindingProblem.DetailFor(exception);

        // Assert
        errors.Keys.ShouldBe([string.Empty]);
        errors[string.Empty].ShouldBe(["The request body is not valid."]);
        detail.ShouldBe("The request body is not valid.");
        detail.ShouldNotContain(nameof(WithLines));
    }

    private static BadHttpRequestException BindingFailureFor(string body) => BindingFailureFor<TwoRequiredMembers>(body);

    private static BadHttpRequestException BindingFailureFor<T>(string body)
    {
        var options = new JsonSerializerOptions(JsonSerializerDefaults.Web);
        var jsonException = Should.Throw<JsonException>(() => JsonSerializer.Deserialize<T>(body, options));
        return new BadHttpRequestException($"Failed to read parameter \"{typeof(T).Name} dto\" from the request body as JSON.", jsonException);
    }

    private sealed record TwoRequiredMembers
    {
        public required uint Version { get; init; }
        public required DateOnly StartsOn { get; init; }
    }

    private sealed record WithLines
    {
        public IReadOnlyList<Line> Lines { get; init; } = [];
    }

    private sealed record Line
    {
        public decimal Hours { get; init; }
    }
}
