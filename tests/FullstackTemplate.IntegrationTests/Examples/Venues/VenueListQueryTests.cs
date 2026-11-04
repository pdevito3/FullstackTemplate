namespace FullstackTemplate.IntegrationTests.Examples.Venues;

using FullstackTemplate.Server.Examples.Venues.Dtos;
using FullstackTemplate.Server.Examples.Venues.Features;
using FullstackTemplate.SharedTestHelpers.Examples.EntityGraphs;
using FullstackTemplate.SharedTestHelpers.Utilities;
using Shouldly;

public class VenueListQueryTests : TestBase
{
    [Test]
    public async Task a_name_filter_finds_the_names_that_contain_the_text_with_no_regard_to_case_and_a_percent_as_plain_text()
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
        var found = await NamesContainingAsync(testingServiceScope, $"hall {marker.ToUpperInvariant()}");
        var percent = await NamesContainingAsync(testingServiceScope, $"{marker} 50%");

        // Assert
        found.ShouldBe([$"North Hall {marker}", $"South Hall {marker}"]);
        percent.ShouldBe([$"{marker} 50% full"]);
    }

    private static async Task<List<string>> NamesContainingAsync(TestingServiceScope scope, string text)
    {
        var page = await scope.SendAsync(new GetVenueList.Query(
            VenueParametersDto.From(null, null, $"name @=* {QueryKitText.Quote(text)}", null)));
        return page.Select(v => v.Name).ToList();
    }
}
