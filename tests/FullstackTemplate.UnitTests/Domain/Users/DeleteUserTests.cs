namespace FullstackTemplate.UnitTests.Domain.Users;

using FullstackTemplate.Server.Domain.Users;
using FullstackTemplate.Server.Domain.Users.DomainEvents;
using FullstackTemplate.SharedTestHelpers.Fakes.User;
using Shouldly;

public class DeleteUserTests
{
    [Test]
    public void a_delete_queues_user_deleted_with_the_user_id()
    {
        // Arrange
        var user = User.Create(new FakeUserForCreation().Generate());

        // Act
        user.Delete();

        // Assert
        user.DomainEvents.ShouldBe([new UserCreated(user.Id), new UserDeleted(user.Id)]);
    }
}
