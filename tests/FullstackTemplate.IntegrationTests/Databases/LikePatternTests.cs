namespace FullstackTemplate.IntegrationTests.Databases;

using FullstackTemplate.Server.Databases;
using FullstackTemplate.Server.Domain.Users.Mappings;
using FullstackTemplate.Server.Resources;
using FullstackTemplate.SharedTestHelpers.EntityGraphs;
using Microsoft.EntityFrameworkCore;
using Shouldly;

public class LikePatternTests : TestBase
{
    private static readonly string[] FirstNames = ["Yard 50% full", "Yard 500 full", "a_b", "acb", @"c\d", "cd"];

    [Test]
    [Arguments("50%", "Yard 50% full")]
    [Arguments("a_b", "a_b")]
    [Arguments(@"c\d", @"c\d")]
    public async Task an_ilike_search_matches_a_percent_an_underscore_or_a_backslash_as_plain_text(string search, string match)
    {
        // Arrange
        var testingServiceScope = new TestingServiceScope();
        var lastName = await AddUsersAsync(testingServiceScope);
        var pattern = LikePattern.Containing(search);

        // Act
        var found = await testingServiceScope.ExecuteDbContextAsync(db => db.Users
            .Where(u => u.LastName == lastName)
            .Where(u => EF.Functions.ILike(u.FirstName, pattern, LikePattern.EscapeCharacter))
            .Select(u => u.FirstName)
            .ToListAsync());

        // Assert
        found.ShouldBe([match]);
    }

    [Test]
    public async Task an_exact_ilike_check_ignores_case_and_matches_an_underscore_as_plain_text()
    {
        // Arrange
        var testingServiceScope = new TestingServiceScope();
        var lastName = await AddUsersAsync(testingServiceScope);
        var pattern = LikePattern.Exactly("A_B");

        // Act
        var found = await testingServiceScope.ExecuteDbContextAsync(db => db.Users
            .Where(u => u.LastName == lastName)
            .Where(u => EF.Functions.ILike(u.FirstName, pattern, LikePattern.EscapeCharacter))
            .Select(u => u.FirstName)
            .ToListAsync());

        // Assert
        found.ShouldBe(["a_b"]);
    }

    // A filter needs no LikePattern. EF Core sends the Contains, StartsWith, and EndsWith of QueryKit as a LIKE pattern with each wildcard escaped.
    [Test]
    [Arguments("@=*", "50%", "Yard 50% full")]
    [Arguments("_=", "a_b", "a_b")]
    [Arguments("_-=*", @"c\d", @"c\d")]
    public async Task a_querykit_text_filter_matches_a_percent_an_underscore_or_a_backslash_as_plain_text(string op, string search, string match)
    {
        // Arrange
        var testingServiceScope = new TestingServiceScope();
        var lastName = await AddUsersAsync(testingServiceScope);

        // Act
        var found = await testingServiceScope.ExecuteDbContextAsync(db => db.Users
            .Where(u => u.LastName == lastName)
            .ToUserDtoQueryable()
            .ApplyFilters($"firstName {op} \"{search}\"", new CustomQueryKitConfiguration())
            .Select(u => u.FirstName)
            .ToListAsync());

        // Assert
        found.ShouldBe([match]);
    }

    private static async Task<string> AddUsersAsync(TestingServiceScope testingServiceScope)
    {
        var lastName = $"like-{Guid.NewGuid()}";
        foreach (var firstName in FirstNames)
        {
            await testingServiceScope.StartGraph(out _)
                .AddUser(out _, u =>
                {
                    u.FirstName = firstName;
                    u.LastName = lastName;
                })
                .BuildAsync();
        }

        return lastName;
    }
}
