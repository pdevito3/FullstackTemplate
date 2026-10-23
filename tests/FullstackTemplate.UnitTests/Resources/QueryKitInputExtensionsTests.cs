namespace FullstackTemplate.UnitTests.Resources;

using FullstackTemplate.Server.Exceptions;
using FullstackTemplate.Server.Resources;
using QueryKit.Exceptions;
using Shouldly;

public class QueryKitInputExtensionsTests
{
    private sealed record Row(string Name, uint Version = 0);

    private static readonly IQueryable<Row> Rows = new[] { new Row("a") }.AsQueryable();

    [Test]
    public void bad_sort_direction_is_keyed_by_sort_order()
    {
        // Act
        var exception = Should.Throw<QueryInputException>(() => Rows.ApplySortOrder("Name foo", new CustomQueryKitConfiguration()));

        // Assert
        exception.Parameter.ShouldBe("sortOrder");
        exception.Message.ShouldContain("Invalid direction: foo");
        exception.InnerException.ShouldBeOfType<QueryKitParsingException>();
    }

    [Test]
    public void unknown_filter_property_keeps_the_message_of_querykit()
    {
        // Act
        var exception = Should.Throw<QueryInputException>(() => Rows.ApplyFilters("Unknown == \"x\"", new CustomQueryKitConfiguration()));

        // Assert
        exception.Parameter.ShouldBe("filters");
        exception.Message.ShouldBe(exception.InnerException.ShouldBeOfType<UnknownFilterPropertyException>().Message);
    }

    // The message of the .NET exception names .NET types, so the caller gets a fixed message
    [Test]
    public void operator_that_does_not_fit_the_property_type_gets_a_fixed_message()
    {
        // Act
        var exception = Should.Throw<QueryInputException>(() => Rows.ApplyFilters("Name ^^ \"x\"", new CustomQueryKitConfiguration()));

        // Assert
        exception.Parameter.ShouldBe("filters");
        exception.Message.ShouldBe("The filters value is not valid.");
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
        exception.Message.ShouldBe("The filters value cannot use version. A version is opaque.");
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
        exception.Message.ShouldBe("The sortOrder value cannot use version. A version is opaque.");
    }
}
