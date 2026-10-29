namespace FullstackTemplate.UnitTests.Resources;

using System.Diagnostics;
using FullstackTemplate.Server.Exceptions;
using FullstackTemplate.Server.Resources;
using QueryKit.Exceptions;
using Shouldly;

public class QueryKitInputExtensionsTests : IDisposable
{
    private sealed record Row(string Name, uint Version = 0);

    private static readonly IQueryable<Row> Rows = new[] { new Row("a") }.AsQueryable();

    private readonly ActivitySource _source = new("FullstackTemplate.UnitTests.QueryKitInput");
    private readonly ActivityListener _listener;

    public QueryKitInputExtensionsTests()
    {
        _listener = new ActivityListener
        {
            ShouldListenTo = source => source == _source,
            Sample = (ref ActivityCreationOptions<ActivityContext> _) => ActivitySamplingResult.AllDataAndRecorded,
        };
        ActivitySource.AddActivityListener(_listener);
    }

    public void Dispose()
    {
        _listener.Dispose();
        _source.Dispose();
        GC.SuppressFinalize(this);
    }

    [Test]
    public void bad_sort_direction_gets_the_sort_order_message()
    {
        // Act
        var exception = Should.Throw<QueryInputException>(() => Rows.ApplySortOrder("Name foo", new CustomQueryKitConfiguration()));

        // Assert
        exception.Parameter.ShouldBe("sortOrder");
        exception.Message.ShouldBe("The sort order could not be read. Change the sort, then try again.");
        exception.InnerException.ShouldBeOfType<QueryKitParsingException>();
    }

    [Test]
    [Arguments("Name == a\"b")]
    [Arguments("Name @= ")]
    [Arguments("Unknown == \"x\"")]
    public void filter_that_querykit_cannot_read_gets_the_filter_message(string filters)
    {
        // Act
        var exception = Should.Throw<QueryInputException>(() => Rows.ApplyFilters(filters, new CustomQueryKitConfiguration()));

        // Assert
        exception.Parameter.ShouldBe("filters");
        exception.Message.ShouldBe("The filter could not be read. Change the filter, then try again.");
        exception.InnerException.ShouldBeAssignableTo<QueryKitException>();
    }

    [Test]
    public void the_span_gets_the_place_of_a_parse_fault()
    {
        // Arrange
        using var activity = _source.StartActivity("list").ShouldNotBeNull();

        // Act
        Should.Throw<QueryInputException>(() => Rows.ApplyFilters("Name == a\"b", new CustomQueryKitConfiguration()));

        // Assert
        var recorded = activity.Events.ShouldHaveSingleItem();
        recorded.Name.ShouldBe("exception");
        recorded.Tags.ShouldContain(new KeyValuePair<string, object?>("exception.message", "Failed at Line 1, Column 10."));
    }

    // A value that does not convert gets a QueryKit message that holds the value
    [Test]
    [Arguments("Version == \"jane@example.com\"")]
    [Arguments("Name == \"jane@example.com\" && Version == \"jane@example.com\"")]
    public void the_span_does_not_get_the_text_of_the_filter(string filters)
    {
        // Arrange
        using var activity = _source.StartActivity("list").ShouldNotBeNull();

        // Act
        var exception = Should.Throw<QueryInputException>(() => Rows.ApplyFilters(filters, new CustomQueryKitConfiguration()));

        // Assert
        exception.InnerException.ShouldNotBeNull().ToString().ShouldContain("jane@example.com");
        activity.Events.ShouldHaveSingleItem().Tags.ShouldAllBe(tag => !(tag.Value ?? "").ToString()!.Contains("jane@example.com"));
    }

    [Test]
    public void filter_longer_than_its_maximum_gets_the_too_long_message()
    {
        // Arrange
        var filters = $"Name == \"{new string('a', CustomQueryKitConfiguration.FilterMaxLength)}\"";

        // Act
        var exception = Should.Throw<QueryInputException>(() => Rows.ApplyFilters(filters, new CustomQueryKitConfiguration()));

        // Assert
        exception.Message.ShouldBe("The filter is too long. Remove a condition or shorten a value, then try again.");
        exception.InnerException.ShouldBeOfType<QueryKitInputLengthExceededException>();
    }

    [Test]
    public void filter_nested_deeper_than_its_maximum_gets_the_too_deep_message()
    {
        // Arrange
        var depth = CustomQueryKitConfiguration.FilterMaxNestingDepth + 1;
        var filters = $"{new string('(', depth)}Name == \"x\"{new string(')', depth)}";

        // Act
        var exception = Should.Throw<QueryInputException>(() => Rows.ApplyFilters(filters, new CustomQueryKitConfiguration()));

        // Assert
        exception.Message.ShouldBe("The filter has too many nested groups. Remove a group, then try again.");
        exception.InnerException.ShouldBeOfType<QueryKitNestingDepthExceededException>();
    }

    // The message of the .NET exception names .NET types, so the caller gets a fixed message
    [Test]
    public void operator_that_does_not_fit_the_property_type_gets_the_filter_message()
    {
        // Act
        var exception = Should.Throw<QueryInputException>(() => Rows.ApplyFilters("Name ^^ \"x\"", new CustomQueryKitConfiguration()));

        // Assert
        exception.Parameter.ShouldBe("filters");
        exception.Message.ShouldBe("The filter could not be read. Change the filter, then try again.");
        exception.InnerException.ShouldBeAssignableTo<ArgumentException>();
    }

    [Test]
    [Arguments("version == 5")]
    [Arguments("VERSION > 5")]
    [Arguments("Name == \"a\" || Version != 5")]
    public void filter_on_the_version_is_refused(string filters)
    {
        // Act
        var exception = Should.Throw<QueryInputException>(() => Rows.ApplyFilters(filters, new CustomQueryKitConfiguration()));

        // Assert
        exception.Parameter.ShouldBe("filters");
        exception.Message.ShouldBe("The filter cannot use the version. Choose another field.");
    }

    [Test]
    [Arguments("version")]
    [Arguments("Name, -Version")]
    public void sort_on_the_version_is_refused(string sortOrder)
    {
        // Act
        var exception = Should.Throw<QueryInputException>(() => Rows.ApplySortOrder(sortOrder, new CustomQueryKitConfiguration()));

        // Assert
        exception.Parameter.ShouldBe("sortOrder");
        exception.Message.ShouldBe("The sort order cannot use the version. Choose another field.");
    }
}
