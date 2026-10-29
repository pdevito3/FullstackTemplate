namespace FullstackTemplate.IntegrationTests.Examples.Venues;

using FullstackTemplate.Server.Examples.Venues.Dtos;
using FullstackTemplate.Server.Examples.Venues.Features;
using FullstackTemplate.Server.Exceptions;
using FullstackTemplate.SharedTestHelpers.EntityGraphs;
using FullstackTemplate.SharedTestHelpers.Examples.EntityGraphs;
using Shouldly;

public class VenueListQueryTests : TestBase
{
    [Test]
    public async Task a_search_finds_the_names_that_contain_the_text_with_no_regard_to_case_and_a_percent_as_plain_text()
    {
        // Arrange
        var testingServiceScope = new TestingServiceScope();
        var marker = Guid.NewGuid().ToString("N");
        await testingServiceScope.StartGraph(out _)
            .AddVenues(out _,
                v => v.Name = $"North Hall {marker}",
                v => v.Name = $"South Hall {marker}",
                v => v.Name = $"{marker} 50% full",
                v => v.Name = $"{marker} 500 full")
            .BuildAsync();

        // Act
        var found = await testingServiceScope.SendAsync(new GetVenueList.Query(
            VenueParametersDto.From(null, null, null, null, $" hall {marker.ToUpperInvariant()} ")));
        var percent = await testingServiceScope.SendAsync(new GetVenueList.Query(
            VenueParametersDto.From(null, null, null, null, $"{marker} 50%")));

        // Assert
        found.Select(v => v.Name).ShouldBe([$"North Hall {marker}", $"South Hall {marker}"]);
        percent.Select(v => v.Name).ShouldBe([$"{marker} 50% full"]);
    }

    [Test]
    public async Task a_search_longer_than_its_maximum_gives_a_query_input_error()
    {
        // Arrange
        var testingServiceScope = new TestingServiceScope();
        var search = new string('a', VenueParametersDto.SearchMaxLength + 1);

        // Act
        var act = () => testingServiceScope.SendAsync(new GetVenueList.Query(
            VenueParametersDto.From(null, null, null, null, search)));

        // Assert
        (await act.ShouldThrowAsync<QueryInputException>()).Parameter.ShouldBe("search");
    }
}
