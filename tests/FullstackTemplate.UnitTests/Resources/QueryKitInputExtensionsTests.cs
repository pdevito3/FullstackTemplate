namespace FullstackTemplate.UnitTests.Resources;

using System.Diagnostics;
using FullstackTemplate.Server.Exceptions;
using FullstackTemplate.Server.Resources;
using QueryKit.Exceptions;
using Shouldly;

public class QueryKitInputExtensionsTests : IDisposable
{
    private sealed record Row(string Name, int Seats = 0, uint Version = 0);

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
    [Arguments("Name == a\"b", "The filter could not be read at character 10. Check the operator and the quotes there, then try again.")]
    [Arguments("Name @= ", "The filter could not be read at character 9. Check the operator and the quotes there, then try again.")]
    public void parse_fault_gets_a_message_with_its_place(string filters, string message)
    {
        // Act
        var exception = Should.Throw<QueryInputException>(() => Rows.ApplyFilters(filters, new CustomQueryKitConfiguration()));

        // Assert
        exception.Parameter.ShouldBe("filters");
        exception.Message.ShouldBe(message);
        exception.InnerException.ShouldBeOfType<ParsingException>();
    }

    [Test]
    public void unknown_filter_field_gets_a_message_with_the_field_and_the_fields_of_the_list()
    {
        // Act
        var exception = Should.Throw<QueryInputException>(() => Rows.ApplyFilters("Title == \"x\"", new CustomQueryKitConfiguration()));

        // Assert
        exception.Message.ShouldBe(
            "The filter names the field Title, which this list does not have. Use one of these fields: name, seats.");
        exception.InnerException.ShouldBeOfType<UnknownFilterPropertyException>();
    }

    [Test]
    public void unknown_sort_field_gets_a_message_with_the_field_and_the_fields_of_the_list()
    {
        // Act
        var exception = Should.Throw<QueryInputException>(() => Rows.ApplySortOrder("Title desc", new CustomQueryKitConfiguration()));

        // Assert
        exception.Parameter.ShouldBe("sortOrder");
        exception.Message.ShouldBe("The sort order names the field Title, which this list does not have. Use one of these fields: name, seats.");
        exception.InnerException.ShouldBeOfType<SortParsingException>();
    }

    [Test]
    public void value_that_does_not_fit_its_field_gets_a_message_with_the_field_and_not_the_value()
    {
        // Act
        var exception = Should.Throw<QueryInputException>(() => Rows.ApplyFilters("Seats == \"jane@example.com\"", new CustomQueryKitConfiguration()));

        // Assert
        exception.Message.ShouldBe("A value in the filter does not fit the field Seats. Change the value, then try again.");
        exception.InnerException.ShouldBeOfType<ParsingException>();
    }

    [Test]
    public void value_that_holds_the_words_of_the_querykit_message_does_not_get_into_the_message()
    {
        // Act
        var exception = Should.Throw<QueryInputException>(() =>
            Rows.ApplyFilters("Seats == \"x for the filter property 'jane@example.com\"", new CustomQueryKitConfiguration()));

        // Assert
        exception.Message.ShouldBe("A value in the filter does not fit the field Seats. Change the value, then try again.");
    }

    // A value can hold a line of its own that reads as a whole QueryKit message. The regex reads only the whole message.
    [Test]
    [Arguments("\n")]
    [Arguments("\r\n")]
    public void value_that_holds_a_querykit_message_on_a_line_of_its_own_does_not_get_into_the_message(string lineBreak)
    {
        // Arrange
        var line = "The value 'b' is not a valid Int32 for the filter property 'jane@example.com'.";
        var filters = $"Seats == \"a{lineBreak}{line}{lineBreak}\"";

        // Act
        var exception = Should.Throw<QueryInputException>(() => Rows.ApplyFilters(filters, new CustomQueryKitConfiguration()));

        // Assert
        exception.InnerException.ShouldNotBeNull().Message.ShouldContain(line);
        exception.Message.ShouldNotContain("jane@example.com");
    }

    [Test]
    [Arguments("Name ~~ \"paul\"")]
    [Arguments("Name !~ \"paul\"")]
    public void sounds_like_operator_gets_a_message_that_names_the_operators(string filters)
    {
        // Act
        var exception = Should.Throw<QueryInputException>(() => Rows.ApplyFilters(filters, new CustomQueryKitConfiguration()));

        // Assert
        exception.Parameter.ShouldBe("filters");
        exception.Message.ShouldBe(
            "This list cannot filter with the sounds-like operators ~~ and !~. Choose another operator, then try again.");
        exception.InnerException.ShouldBeOfType<QueryKitDbContextTypeException>();
    }

    [Test]
    public void the_span_does_not_get_a_field_name_that_can_be_text_of_the_filter()
    {
        // Arrange
        using var activity = _source.StartActivity("list").ShouldNotBeNull();

        // Act
        Should.Throw<QueryInputException>(() => Rows.ApplyFilters("Paul == \"x\"", new CustomQueryKitConfiguration()));
        Should.Throw<QueryInputException>(() => Rows.ApplySortOrder("jane@example.com", new CustomQueryKitConfiguration()));

        // Assert
        activity.Events.Count().ShouldBe(2);
        activity.Events.SelectMany(e => e.Tags).ShouldAllBe(tag =>
            !(tag.Value ?? "").ToString()!.Contains("Paul") && !(tag.Value ?? "").ToString()!.Contains("jane@example.com"));
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
    [Arguments("jane@example.com")]
    [Arguments("Name jane@example.com")]
    public void the_span_does_not_get_the_text_of_the_sort_order(string sortOrder)
    {
        // Arrange
        using var activity = _source.StartActivity("list").ShouldNotBeNull();

        // Act
        Should.Throw<QueryInputException>(() => Rows.ApplySortOrder(sortOrder, new CustomQueryKitConfiguration()));

        // Assert
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

    // QueryKit reads a member of a field, also a private one, and EF cannot translate most of them
    [Test]
    [Arguments("Name.Length > 3")]
    [Arguments("Seats.m_value > 3")]
    [Arguments("Name == \"a\" || Name.Length == 1")]
    public void filter_on_a_part_of_a_field_gets_a_message_with_the_fields_of_the_list(string filters)
    {
        // Act
        var exception = Should.Throw<QueryInputException>(() => Rows.ApplyFilters(filters, new CustomQueryKitConfiguration()));

        // Assert
        exception.Parameter.ShouldBe("filters");
        exception.Message.ShouldBe("The filter can use a field of the list, not a part of a field. Use one of these fields: name, seats.");
    }

    [Test]
    public void sort_on_a_part_of_a_field_gets_a_message_with_the_fields_of_the_list()
    {
        // Act
        var exception = Should.Throw<QueryInputException>(() => Rows.ApplySortOrder("Name.Length desc", new CustomQueryKitConfiguration()));

        // Assert
        exception.Parameter.ShouldBe("sortOrder");
        exception.Message.ShouldBe("The sort order can use a field of the list, not a part of a field. Use one of these fields: name, seats.");
    }

    [Test]
    [Arguments("Name ^^ \"x\"")]
    [Arguments("Name ^^* \"x\"")]
    [Arguments("Name !^^* \"x\"")]
    [Arguments("Seats %^^ 5")]
    public void in_operator_with_one_value_gets_a_message_that_asks_for_a_list(string filters)
    {
        // Act
        var exception = Should.Throw<QueryInputException>(() => Rows.ApplyFilters(filters, new CustomQueryKitConfiguration()));

        // Assert
        exception.Parameter.ShouldBe("filters");
        exception.Message.ShouldBe(
            "The operators ^^ and !^^ need a list of values, for example [\"a\", \"b\"]. Put the values in brackets, then try again.");
    }

    // QueryKit builds a case-insensitive comparison with null as a call on null, so the query throws when it runs
    [Test]
    [Arguments("Name ==* null")]
    [Arguments("Name !=* null")]
    [Arguments("Name @=* null")]
    [Arguments("Name _= null")]
    public void comparison_with_null_that_is_not_equals_gets_a_message_that_names_the_operators(string filters)
    {
        // Act
        var exception = Should.Throw<QueryInputException>(() => Rows.ApplyFilters(filters, new CustomQueryKitConfiguration()));

        // Assert
        exception.Parameter.ShouldBe("filters");
        exception.Message.ShouldBe("Only the operators == and != can compare with null. Change the operator, then try again.");
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
