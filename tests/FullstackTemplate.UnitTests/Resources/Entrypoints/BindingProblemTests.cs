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
        errors["lines[1].hours"].ShouldBe(["The hours field is not a valid number."]);
        detail.ShouldBe("The hours field is not a valid number.");
    }

    [Test]
    [Arguments("count", "\"two\"", "The count field is not a valid whole number.")]
    [Arguments("count", "1.5", "The count field is not a valid whole number.")]
    [Arguments("count", "99999999999", "The count field is not a valid whole number.")]
    [Arguments("maybeCount", "\"two\"", "The maybeCount field is not a valid whole number.")]
    [Arguments("hours", "null", "The hours field is not a valid number.")]
    [Arguments("startsAt", "\"tomorrow\"", "The startsAt field is not a valid date and time.")]
    [Arguments("maybeStartsAt", "\"tomorrow\"", "The maybeStartsAt field is not a valid date and time.")]
    [Arguments("startsOn", "\"2026-13-01\"", "The startsOn field is not a valid date.")]
    [Arguments("name", "1", "The name field is not valid.")]
    public void value_of_the_wrong_type_names_what_the_field_takes(string field, string value, string message)
    {
        // Arrange
        var exception = BindingFailureFor<EachType>($$"""{ "{{field}}": {{value}} }""");

        // Act
        var errors = BindingProblem.ErrorsFor(exception);
        var detail = BindingProblem.DetailFor(exception);

        // Assert
        errors.Keys.ShouldBe([field]);
        errors[field].ShouldBe([message]);
        detail.ShouldBe(message);
    }

    [Test]
    public void value_that_is_not_valid_json_is_an_error_under_its_field()
    {
        // Arrange
        var exception = BindingFailureFor("""{ "version": 1.2.3, "startsOn": "2026-01-01" }""");

        // Act
        var errors = BindingProblem.ErrorsFor(exception);
        var detail = BindingProblem.DetailFor(exception);

        // Assert
        errors.Keys.ShouldBe(["version"]);
        errors["version"].ShouldBe(["The version field is not valid JSON."]);
        detail.ShouldBe("The version field is not valid JSON.");
    }

    [Test]
    public void value_that_is_not_valid_json_in_a_child_is_an_error_under_its_path()
    {
        // Arrange
        var exception = BindingFailureFor<WithLines>("""{ "lines": [{ "hours": 1 }, { "hours": 1.2.3 }] }""");

        // Act
        var errors = BindingProblem.ErrorsFor(exception);
        var detail = BindingProblem.DetailFor(exception);

        // Assert
        errors.Keys.ShouldBe(["lines[1].hours"]);
        errors["lines[1].hours"].ShouldBe(["The hours field is not valid JSON."]);
        detail.ShouldBe("The hours field is not valid JSON.");
    }

    [Test]
    public void body_that_is_not_valid_json_is_an_error_under_the_empty_key()
    {
        // Arrange
        var exception = BindingFailureFor<WithLines>("not json");

        // Act
        var errors = BindingProblem.ErrorsFor(exception);
        var detail = BindingProblem.DetailFor(exception);

        // Assert
        errors.Keys.ShouldBe([string.Empty]);
        errors[string.Empty].ShouldBe(["The request body is not valid JSON."]);
        detail.ShouldBe("The request body is not valid JSON.");
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

    private sealed record EachType
    {
        public int Count { get; init; }
        public long? MaybeCount { get; init; }
        public decimal Hours { get; init; }
        public DateTimeOffset StartsAt { get; init; }
        public DateTime? MaybeStartsAt { get; init; }
        public DateOnly StartsOn { get; init; }
        public string Name { get; init; } = string.Empty;
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
