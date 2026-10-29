namespace FullstackTemplate.IntegrationTests.Examples.Lists;

using FeatureTests.Lists;
using FullstackTemplate.Server.Examples.Venues.Dtos;
using FullstackTemplate.Server.Examples.Venues.Features;
using FullstackTemplate.Server.Examples.Workshops.Dtos;
using FullstackTemplate.Server.Examples.Workshops.Features;
using FullstackTemplate.Server.Exceptions;
using Shouldly;

/// <summary>The sweep of <see cref="ListFilterSweep"/> on the example lists.</summary>
public class ExampleListFilterSweepTests : TestBase
{
    [Test]
    public async Task each_operator_on_each_field_of_the_workshops_runs_or_gives_a_query_input_error()
    {
        // Arrange
        var testingServiceScope = new TestingServiceScope();

        // Act
        var faults = await ListFilterSweep.FaultsAsync(typeof(WorkshopDto), (filters, sort) =>
            testingServiceScope.SendAsync(new GetWorkshopList.Query(WorkshopParametersDto.From(null, null, filters, sort, null))));

        // Assert
        faults.ShouldBeEmpty();
    }

    [Test]
    public async Task each_operator_on_each_field_of_the_venues_runs_or_gives_a_query_input_error()
    {
        // Arrange
        var testingServiceScope = new TestingServiceScope();

        // Act
        var faults = await ListFilterSweep.FaultsAsync(typeof(VenueDto), (filters, sort) =>
            testingServiceScope.SendAsync(new GetVenueList.Query(VenueParametersDto.From(null, null, filters, sort, null))));

        // Assert
        faults.ShouldBeEmpty();
    }

    // QueryKit reads a member of a field, also a private one. EF cannot translate most of them.
    [Test]
    [Arguments("startsAt.ticks > 5", null)]
    [Arguments("startsAt.utcTicks > 5", null)]
    [Arguments("startsAt.totalOffsetMinutes > 3", null)]
    [Arguments("startsAt.dateTime > 2030-01-01T00:00:00Z", null)]
    [Arguments("capacity.m_value > 3", null)]
    [Arguments("title.length > 3", null)]
    [Arguments(null, "startsAt.ticks desc")]
    [Arguments(null, "startsAt.offset")]
    public async Task a_part_of_a_field_gives_a_query_input_error(string? filters, string? sortOrder)
    {
        // Arrange
        var testingServiceScope = new TestingServiceScope();

        // Act
        var act = () => testingServiceScope.SendAsync(new GetWorkshopList.Query(
            WorkshopParametersDto.From(null, null, filters, sortOrder, null)));

        // Assert
        (await act.ShouldThrowAsync<QueryInputException>()).Parameter.ShouldBe(filters is null ? "sortOrder" : "filters");
    }
}
