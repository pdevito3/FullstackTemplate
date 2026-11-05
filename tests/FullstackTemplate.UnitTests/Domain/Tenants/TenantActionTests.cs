namespace FullstackTemplate.UnitTests.Domain.Tenants;

using FullstackTemplate.Server.Domain.Tenants;
using FullstackTemplate.Server.Domain.Users;
using FullstackTemplate.Server.Exceptions;
using FullstackTemplate.SharedTestHelpers.EntityGraphs;
using Shouldly;

/// <summary>
/// <see cref="Tenant.AllowedActions"/> lists an action only when its guard lets the caller through, so the invite
/// button that the SPA shows from the list does not get a 403.
/// </summary>
public class TenantActionTests
{
    public static IEnumerable<Func<Caller>> Callers()
    {
        yield return () => Caller.AllTenantsManager.Instance;
        yield return () => new Caller.Outsider(OutsiderKind.Anonymous);
        yield return () => new Caller.Member("usr_caller", []);
        yield return () => new Caller.Member("usr_caller", [Permission.ReadUsers()]);
        yield return () => new Caller.Member("usr_caller", [Permission.ManageUsers()]);
        yield return () => new Caller.Member("usr_caller", [.. Permission.GetAll()]);
        yield return () => new Caller.DelegatedAgent("clinic-assistant", Permission.GetAll(), [Permission.ReadUsers()]);
    }

    [Test, Property("Rule", "INV-1")]
    [MethodDataSource(nameof(Callers))]
    public void the_list_has_invite_user_exactly_when_the_guard_lets_the_caller_through(Caller caller)
    {
        // Arrange
        TestGraph.Start(out _).AddTenant(out var tenant);
        var guard = () => TenantAction.InviteUser().GuardCaller(caller);

        // Act
        var actions = tenant.AllowedActions(caller);

        // Assert
        if (actions.Contains(TenantAction.InviteUser()))
            guard.ShouldNotThrow();
        else
            guard.ShouldThrow<ForbiddenAccessException>();
        actions.Contains(TenantAction.InviteUser()).ShouldBe(caller.Has(Permission.ManageUsers()));
    }
}
