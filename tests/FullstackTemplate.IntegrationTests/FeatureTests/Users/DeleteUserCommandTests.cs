namespace FullstackTemplate.IntegrationTests.FeatureTests.Users;

using FullstackTemplate.Server.Domain.Users.Features;
using FullstackTemplate.SharedTestHelpers.Fakes.User;
using Microsoft.EntityFrameworkCore;
using Shouldly;

public class DeleteUserCommandTests : TestBase
{
    [Fact]
    public async Task can_delete_existing_user()
    {
        // Arrange
        var testingServiceScope = new TestingServiceScope();
        var fakeUser = new FakeUserBuilder().Build();
        await testingServiceScope.InsertAsync(fakeUser);

        // Act
        var command = new DeleteUser.Command(fakeUser.Id);
        await testingServiceScope.SendAsync(command);

        // Assert - User should be soft deleted
        var userInDb = await testingServiceScope.ExecuteDbContextAsync(db =>
            db.Users
                .IgnoreQueryFilters()
                .FirstOrDefaultAsync(u => u.Id == fakeUser.Id));

        userInDb.ShouldNotBeNull();
        userInDb.IsDeleted.ShouldBeTrue();
    }

    [Fact]
    public async Task deleted_user_not_returned_in_queries()
    {
        // Arrange
        var testingServiceScope = new TestingServiceScope();
        var fakeUser = new FakeUserBuilder().Build();
        await testingServiceScope.InsertAsync(fakeUser);

        // Act
        var deleteCommand = new DeleteUser.Command(fakeUser.Id);
        await testingServiceScope.SendAsync(deleteCommand);

        // Assert - User should not be found without IgnoreQueryFilters
        var userInDb = await testingServiceScope.ExecuteDbContextAsync(db =>
            db.Users.FirstOrDefaultAsync(u => u.Id == fakeUser.Id));

        userInDb.ShouldBeNull();
    }
}
