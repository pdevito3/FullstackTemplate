namespace FullstackTemplate.IntegrationTests.FeatureTests.Users;

using FullstackTemplate.Server.Domain.Users.Features;
using FullstackTemplate.SharedTestHelpers.Fakes.User;
using Microsoft.EntityFrameworkCore;
using Shouldly;

public class UpdateUserCommandTests : TestBase
{
    [Fact]
    public async Task can_update_existing_user()
    {
        // Arrange
        var testingServiceScope = new TestingServiceScope();
        var fakeUser = new FakeUserBuilder().Build();
        await testingServiceScope.InsertAsync(fakeUser);

        var updateDto = new FakeUserForUpdateDto()
            .RuleFor(x => x.FirstName, _ => "UpdatedFirst")
            .RuleFor(x => x.LastName, _ => "UpdatedLast")
            .Generate();

        // Act
        var command = new UpdateUser.Command(fakeUser.Id, updateDto);
        var userReturned = await testingServiceScope.SendAsync(command);

        // Assert
        var userInDb = await testingServiceScope.ExecuteDbContextAsync(db =>
            db.Users.FirstOrDefaultAsync(u => u.Id == fakeUser.Id));

        userInDb.ShouldNotBeNull();
        userInDb.FirstName.ShouldBe("UpdatedFirst");
        userInDb.LastName.ShouldBe("UpdatedLast");
        userReturned.FirstName.ShouldBe("UpdatedFirst");
        userReturned.LastName.ShouldBe("UpdatedLast");
    }

    [Fact]
    public async Task can_update_user_email()
    {
        // Arrange
        var testingServiceScope = new TestingServiceScope();
        var fakeUser = new FakeUserBuilder().Build();
        await testingServiceScope.InsertAsync(fakeUser);

        var updateDto = new FakeUserForUpdateDto()
            .RuleFor(x => x.Email, _ => "newemail@example.com")
            .Generate();

        // Act
        var command = new UpdateUser.Command(fakeUser.Id, updateDto);
        var userReturned = await testingServiceScope.SendAsync(command);

        // Assert
        var userInDb = await testingServiceScope.ExecuteDbContextAsync(db =>
            db.Users.FirstOrDefaultAsync(u => u.Id == fakeUser.Id));

        userInDb.ShouldNotBeNull();
        userInDb.Email.Value.ShouldBe("newemail@example.com");
    }
}
