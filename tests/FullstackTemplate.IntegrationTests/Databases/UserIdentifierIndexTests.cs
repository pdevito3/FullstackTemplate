namespace FullstackTemplate.IntegrationTests.Databases;

using FullstackTemplate.Server.Databases;
using FullstackTemplate.Server.Domain.Users.Features;
using FullstackTemplate.SharedTestHelpers.EntityGraphs;
using Microsoft.EntityFrameworkCore;
using Shouldly;

public class UserIdentifierIndexTests : TestBase
{
    [Test]
    public async Task two_live_users_cannot_have_one_identifier()
    {
        // Arrange
        var testingServiceScope = new TestingServiceScope();
        var identifier = $"live-{Guid.NewGuid()}";
        await testingServiceScope.StartGraph(out _)
            .AddUser(out _, u => u.Identifier = identifier)
            .BuildAsync();

        // Act
        var addSecond = () => testingServiceScope.StartGraph(out _)
            .AddUser(out _, u => u.Identifier = identifier)
            .BuildAsync();

        // Assert
        (await addSecond.ShouldThrowAsync<ConstraintConflictException>()).Constraint.ShouldBe("ix_users_identifier");
    }

    [Test]
    public async Task a_deleted_user_frees_its_identifier()
    {
        // Arrange
        var testingServiceScope = new TestingServiceScope();
        var identifier = $"deleted-{Guid.NewGuid()}";
        await testingServiceScope.StartGraph(out _)
            .AddUser(out var deleted, u => u.Identifier = identifier)
            .BuildAsync();
        await testingServiceScope.SendAsync(new DeleteUser.Command(deleted.Id));

        // Act
        await testingServiceScope.StartGraph(out _)
            .AddUser(out var live, u => u.Identifier = identifier)
            .BuildAsync();

        // Assert
        var rows = await testingServiceScope.ExecuteDbContextAsync(db => db.Users
            .IgnoreQueryFilters()
            .Where(u => u.Identifier == identifier)
            .Select(u => new { u.Id, u.IsDeleted })
            .ToListAsync());
        rows.ShouldBe([new { deleted.Id, IsDeleted = true }, new { live.Id, IsDeleted = false }], ignoreOrder: true);
    }
}
