namespace FullstackTemplate.IntegrationTests.Examples.Workshops;

using FullstackTemplate.Server.Examples.Workshops.Dtos;
using FullstackTemplate.Server.Examples.Workshops.Features;
using FullstackTemplate.Server.Exceptions;
using FullstackTemplate.SharedTestHelpers.EntityGraphs;
using FullstackTemplate.SharedTestHelpers.Examples.EntityGraphs;
using Shouldly;

public class WorkshopListQueryTests : TestBase
{
    private static readonly string[] Titles = ["Yard 50% full", "Yard 500 full", "a_b", "acb", @"c\d", "cd"];
    private static readonly string[] TextTitles = ["Say \"hi\" now", "a\"\"\"b", @"c\d", "in-progress", "a.b", "null", "[DRAFT]"];
    private static readonly string[] DraftActions = ["Update", "Schedule", "Cancel"];

    [Test]
    [Arguments("50%", "Yard 50% full")]
    [Arguments("a_b", "a_b")]
    [Arguments(@"c\d", @"c\d")]
    [Arguments("  A_B ", "a_b")]
    public async Task a_search_matches_a_percent_an_underscore_or_a_backslash_as_plain_text(string search, string match)
    {
        // Arrange
        var testingServiceScope = new TestingServiceScope();
        var venueId = await AddWorkshopsAsync(testingServiceScope);

        // Act
        var found = await testingServiceScope.SendAsync(new GetWorkshopList.Query(
            WorkshopParametersDto.From(null, null, $"venueId == \"{venueId}\"", null, search)));

        // Assert
        found.Select(w => w.Title).ShouldBe([match]);
    }

    [Test]
    public async Task the_list_sorts_by_the_start_and_gives_each_row_its_actions()
    {
        // Arrange
        var testingServiceScope = new TestingServiceScope();
        var venueId = await AddWorkshopsAsync(testingServiceScope);

        // Act
        var page = await testingServiceScope.SendAsync(new GetWorkshopList.Query(
            WorkshopParametersDto.From(1, 4, $"venueId == \"{venueId}\"", null, null)));

        // Assert
        page.TotalCount.ShouldBe(Titles.Length);
        page.Select(w => w.Title).ShouldBe(Titles.Take(4));
        page.ShouldAllBe(w => w.AllowedActions.SequenceEqual(DraftActions));
    }

    [Test]
    [Arguments("a\0b")]
    [Arguments("\uD800")]
    public async Task a_search_with_a_character_that_postgres_cannot_store_gives_a_query_input_error(string search)
    {
        // Arrange
        var testingServiceScope = new TestingServiceScope();

        // Act
        var act = () => testingServiceScope.SendAsync(new GetWorkshopList.Query(
            WorkshopParametersDto.From(null, null, null, null, search)));

        // Assert
        (await act.ShouldThrowAsync<QueryInputException>()).Parameter.ShouldBe("search");
    }

    [Test]
    public async Task a_search_longer_than_its_maximum_gives_a_query_input_error()
    {
        // Arrange
        var testingServiceScope = new TestingServiceScope();
        var search = new string('a', WorkshopParametersDto.SearchMaxLength + 1);

        // Act
        var act = () => testingServiceScope.SendAsync(new GetWorkshopList.Query(
            WorkshopParametersDto.From(null, null, null, null, search)));

        // Assert
        (await act.ShouldThrowAsync<QueryInputException>()).Parameter.ShouldBe("search");
    }

    public static IEnumerable<(string, string[])> FiltersThatTheFilterBuilderWrites =>
    [
        ("title == \"\"\"Say \"hi\" now\"\"\"", ["Say \"hi\" now"]),
        ("title == \"\"\"\"a\"\"\"b\"\"\"\"", ["a\"\"\"b"]),
        ("title @=* \"\"\"say \"HI\"\"\"", ["Say \"hi\" now"]),
        (@"title == ""c\d""", [@"c\d"]),
        ("title == \"in-progress\"", ["in-progress"]),
        ("title == \"a.b\"", ["a.b"]),
        ("title == \"null\"", ["null"]),
        ("title ^^ [\"in-progress\", \"\"\"Say \"hi\" now\"\"\"]", ["Say \"hi\" now", "in-progress"]),
        ("title ^^ [\"[DRAFT]\"]", ["[DRAFT]"]),
        ("title @=* \"[DRAFT\"", ["[DRAFT]"]),
    ];

    // QueryKit reads a value that starts with [ and ends with ] as a list, also in quotes, so the filter builder refuses
    // it. If this test fails, QueryKit can hold the value, and the filter builder can take it.
    [Test]
    [Arguments("title @=* \"[DRAFT]\"")]
    [Arguments("title @=* \"\"\"[DRAFT]\"\"\"")]
    [Arguments("title == \"[DRAFT]\"")]
    public async Task a_whole_text_value_in_brackets_is_a_list_to_querykit(string filter)
    {
        // Arrange
        var testingServiceScope = new TestingServiceScope();

        // Act
        var act = () => testingServiceScope.SendAsync(new GetWorkshopList.Query(
            WorkshopParametersDto.From(null, null, filter, null, null)));

        // Assert
        (await act.ShouldThrowAsync<QueryInputException>()).Parameter.ShouldBe("filters");
    }

    [Test]
    [MethodDataSource(nameof(FiltersThatTheFilterBuilderWrites))]
    public async Task a_filter_that_the_filter_builder_writes_finds_its_text(string filter, string[] matches)
    {
        // Arrange
        var testingServiceScope = new TestingServiceScope();
        var venueId = await AddWorkshopsAsync(testingServiceScope, TextTitles);

        // Act
        var found = await testingServiceScope.SendAsync(new GetWorkshopList.Query(
            WorkshopParametersDto.From(null, null, $"venueId == \"{venueId}\" && {filter}", null, null)));

        // Assert
        found.Select(w => w.Title).ShouldBe(matches, ignoreOrder: true);
    }

    private static async Task<string> AddWorkshopsAsync(TestingServiceScope testingServiceScope, string[]? titles = null)
    {
        await testingServiceScope.StartGraph(out _)
            .AddVenue(out var venue)
            .AddWorkshops(out _, (titles ?? Titles).Select((title, i) => (Action<WorkshopInput>)(w =>
            {
                w.Title = title;
                w.StartsAt = DateTimeOffset.UtcNow.AddDays(i + 2);
            })).ToArray())
            .BuildAsync();
        return venue.Id;
    }
}
