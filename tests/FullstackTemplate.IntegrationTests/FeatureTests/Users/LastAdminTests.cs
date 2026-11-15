namespace FullstackTemplate.IntegrationTests.FeatureTests.Users;

using FullstackTemplate.IntegrationTests.Databases;
using FullstackTemplate.Server.Domain.Users;
using FullstackTemplate.Server.Domain.Users.Features;
using FullstackTemplate.Server.Exceptions;
using FullstackTemplate.SharedTestHelpers.EntityGraphs;
using FullstackTemplate.SharedTestHelpers.Utilities;
using Microsoft.EntityFrameworkCore;
using Shouldly;

public class LastAdminTests : TestBase
{
    [Test]
    [Property("Rule", "UM-4")]
    public async Task the_only_admin_cannot_become_a_user()
    {
        // Arrange
        var testingServiceScope = new TestingServiceScope();
        await testingServiceScope.StartGraph(out _)
            .AddTenant(out var tenant)
            .AddUsers(out var users, u => u.Role = "Admin", u => u.Role = "User")
            .BuildAsync();
        var admin = users[0];
        testingServiceScope.SetUserWithDefaults(identifier: admin.Identifier);

        // Act
        var act = () => testingServiceScope.SendAsync(new UpdateUserRole.Command(admin.Id, "User"));

        // Assert
        var refusal = await act.ShouldThrowAsync<ValidationException>();
        refusal.Property.ShouldBe("role");
        refusal.Message.ShouldBe(TenantAdmins.LastAdminMessage);
        (await AdminIdsAsync(testingServiceScope, tenant.Id)).ShouldBe([admin.Id]);
    }

    [Test]
    [Property("Rule", "UM-4")]
    public async Task the_only_admin_cannot_be_removed()
    {
        // Arrange
        var testingServiceScope = new TestingServiceScope();
        await testingServiceScope.StartGraph(out _)
            .AddTenant(out var tenant)
            .AddUser(out var admin, u => u.Role = "Admin")
            .BuildAsync();
        testingServiceScope.SetUserWithDefaults(identifier: admin.Identifier);

        // Act
        var act = () => testingServiceScope.SendAsync(new DeleteUser.Command(admin.Id));

        // Assert
        (await act.ShouldThrowAsync<ValidationException>()).Message.ShouldBe(TenantAdmins.LastAdminMessage);
        (await AdminIdsAsync(testingServiceScope, tenant.Id)).ShouldBe([admin.Id]);
    }

    [Test]
    [Property("Rule", "UM-4")]
    public async Task one_of_two_admins_can_become_a_user_and_the_other_can_then_be_removed_only_with_a_new_admin()
    {
        // Arrange
        var testingServiceScope = new TestingServiceScope();
        await testingServiceScope.StartGraph(out _)
            .AddTenant(out var tenant)
            .AddUsers(out var admins, u => u.Role = "Admin", u => u.Role = "Admin")
            .BuildAsync();
        var (ada, bea) = (admins[0], admins[1]);
        testingServiceScope.SetUserWithDefaults(identifier: ada.Identifier);

        // Act
        await testingServiceScope.SendAsync(new UpdateUserRole.Command(bea.Id, "User"));
        var removeAda = () => testingServiceScope.SendAsync(new DeleteUser.Command(ada.Id));
        await removeAda.ShouldThrowAsync<ValidationException>();
        await testingServiceScope.SendAsync(new UpdateUserRole.Command(bea.Id, "Admin"));
        await testingServiceScope.SendAsync(new DeleteUser.Command(ada.Id));

        // Assert
        (await AdminIdsAsync(testingServiceScope, tenant.Id)).ShouldBe([bea.Id]);
    }

    [Test]
    [Property("Rule", "UM-4")]
    public async Task two_admins_who_demote_each_other_at_once_leave_one_admin()
    {
        // Arrange
        var pause = PauseAtQuery.After("users.load_admins");
        await using var services = pause.BuildServices();
        await new TestingServiceScope().StartGraph(out _)
            .AddTenant(out var tenant)
            .AddUsers(out var admins, u => u.Role = "Admin", u => u.Role = "Admin")
            .BuildAsync();
        var (ada, bea) = (admins[0], admins[1]);
        var underTest = new TestingServiceScope(services);
        var rival = new TestingServiceScope();
        underTest.SetUserWithDefaults(identifier: ada.Identifier);
        rival.SetUserWithDefaults(identifier: bea.Identifier);
        var demoteBea = underTest.SendAsync(new UpdateUserRole.Command(bea.Id, "User"));
        await pause.ReachedAsync();

        // Act
        var demoteAda = rival.SendAsync(new UpdateUserRole.Command(ada.Id, "User"));
        try
        {
            await pause.WaitUntilBlockedAsync(demoteAda);
        }
        finally
        {
            pause.Release();
        }

        // Assert
        (await demoteBea).Role.ShouldBe("User");
        (await Should.ThrowAsync<ValidationException>(demoteAda)).Message.ShouldBe(TenantAdmins.LastAdminMessage);
        (await AdminIdsAsync(rival, tenant.Id)).ShouldBe([ada.Id]);
    }

    [Test]
    [Property("Rule", "UM-4")]
    public async Task two_admins_who_remove_each_other_at_once_leave_one_admin()
    {
        // Arrange
        var pause = PauseAtQuery.After("users.load_admins");
        await using var services = pause.BuildServices();
        await new TestingServiceScope().StartGraph(out _)
            .AddTenant(out var tenant)
            .AddUsers(out var admins, u => u.Role = "Admin", u => u.Role = "Admin")
            .BuildAsync();
        var (ada, bea) = (admins[0], admins[1]);
        var underTest = new TestingServiceScope(services);
        var rival = new TestingServiceScope();
        underTest.SetUserWithDefaults(identifier: ada.Identifier);
        rival.SetUserWithDefaults(identifier: bea.Identifier);
        var removeBea = underTest.SendAsync(new DeleteUser.Command(bea.Id));
        await pause.ReachedAsync();

        // Act
        var removeAda = rival.SendAsync(new DeleteUser.Command(ada.Id));
        try
        {
            await pause.WaitUntilBlockedAsync(removeAda);
        }
        finally
        {
            pause.Release();
        }

        // Assert
        await removeBea;
        (await Should.ThrowAsync<ValidationException>(removeAda)).Message.ShouldBe(TenantAdmins.LastAdminMessage);
        (await AdminIdsAsync(rival, tenant.Id)).ShouldBe([ada.Id]);
    }

    private static Task<List<string>> AdminIdsAsync(TestingServiceScope scope, string tenantId)
        => scope.ExecuteDbContextAsync(db => db.Users
            .IgnoreQueryFilters()
            .Where(u => u.TenantId == tenantId && !u.IsDeleted && u.Role.Value == "Admin")
            .Select(u => u.Id)
            .ToListAsync());
}
