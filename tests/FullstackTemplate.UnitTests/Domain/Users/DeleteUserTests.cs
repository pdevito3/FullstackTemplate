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
        var user = User.Create(new FakeUserForCreation().Generate() with { Role = UserRole.User().Value });
        var kept = ((ManagerDecision.Kept)TenantManagers.Decide(new ManagerCase(user, new ManagerChange.Removal(), []))).Proof;

        // Act
        user.Delete(kept);

        // Assert
        user.DomainEvents.ShouldBe([new UserCreated(user.Id), new UserDeleted(user.Id)]);
    }

    [Test]
    public void the_proof_of_a_role_change_is_refused()
    {
        // Arrange
        var user = User.Create(new FakeUserForCreation().Generate() with { Role = UserRole.User().Value });
        var roleChange = ((ManagerDecision.Kept)TenantManagers.Decide(
            new ManagerCase(user, new ManagerChange.NewRole(UserRole.User()), []))).Proof;

        // Act
        var act = () => user.Delete(roleChange);

        // Assert
        act.ShouldThrow<ArgumentException>();
        user.DomainEvents.ShouldBe([new UserCreated(user.Id)]);
    }
}
