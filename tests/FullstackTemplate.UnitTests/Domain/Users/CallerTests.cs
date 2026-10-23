namespace FullstackTemplate.UnitTests.Domain.Users;

using FullstackTemplate.Server.Domain.Users;
using FullstackTemplate.Server.Exceptions;
using Shouldly;

/// <summary>
/// Each kind of caller maps to one variant of <see cref="Caller"/>. The denial message tells the caller what it lacks.
/// </summary>
public class CallerTests
{
    private const string SelfId = "usr_self";
    private const string OtherId = "usr_other";

    private const string DelegatedClientId = "clinic-assistant";
    private const string MachineClientId = "fleet-sync";

    private const string UnknownClientDenial =
        "This client is not a registered agent of your tenant. A tenant admin can register it.";

    private static Caller.Member MemberWith(params Permission[] permissions)
        => new Caller.Member(SelfId, permissions);

    private static Caller.DelegatedAgent DelegatedAgentOf(Permission[] userPermissions, Permission[] grant)
        => new("agt_delegated", DelegatedClientId, SelfId, userPermissions, grant);

    private static Caller.MachineAgent MachineAgentWith(params Permission[] grant)
        => new("agt_machine", MachineClientId, grant);

    public static IEnumerable<Func<(string Case, Caller Caller, string? Denial)>> RequireManageUsersCases()
    {
        yield return () => ("all tenants manager", Caller.AllTenantsManager.Instance, null);
        yield return () => ("member with the permission", MemberWith(Permission.ManageUsers()), null);
        yield return () => ("member with another permission", MemberWith(Permission.ManageTenant()),
            "You need the manage_users permission to do this.");
        yield return () => ("member with no permission", MemberWith(),
            "You need the manage_users permission to do this.");
        yield return () => ("anonymous", new Caller.Outsider(OutsiderKind.Anonymous), "Sign in to do this.");
        yield return () => ("machine client", new Caller.Outsider(OutsiderKind.MachineClient),
            "This client cannot change data.");
        yield return () => ("no user record", new Caller.Outsider(OutsiderKind.NoUserRecord),
            "You need a user record in a tenant to do this.");
        yield return () => ("unknown client", new Caller.Outsider(OutsiderKind.UnknownClient), UnknownClientDenial);
        yield return () => ("delegated agent whose user and grant hold it",
            DelegatedAgentOf([Permission.ManageUsers()], [Permission.ManageUsers()]), null);
        yield return () => ("delegated agent whose user lacks it",
            DelegatedAgentOf([Permission.ManageTenant()], [Permission.ManageUsers(), Permission.ManageTenant()]),
            "You need the manage_users permission to do this.");
        yield return () => ("delegated agent whose grant lacks it",
            DelegatedAgentOf([Permission.ManageUsers()], [Permission.ManageTenant()]),
            "The agent clinic-assistant is not granted the manage_users permission. A tenant admin can grant it.");
        yield return () => ("delegated agent whose user and grant both lack it", DelegatedAgentOf([], []),
            "You need the manage_users permission to do this.");
        yield return () => ("machine agent granted it", MachineAgentWith(Permission.ManageUsers()), null);
        yield return () => ("machine agent not granted it", MachineAgentWith(Permission.ManageTenant()),
            "The agent fleet-sync is not granted the manage_users permission. A tenant admin can grant it.");
    }

    public static IEnumerable<Func<(string Case, Caller Caller, string UserId, string? Denial)>> RequireSelfOrManageUsersCases()
    {
        yield return () => ("member on their own record", MemberWith(), SelfId, null);
        yield return () => ("member on another user, without the permission", MemberWith(), OtherId,
            "You need the manage_users permission to do this.");
        yield return () => ("member on another user, with the permission", MemberWith(Permission.ManageUsers()),
            OtherId, null);
        yield return () => ("all tenants manager", Caller.AllTenantsManager.Instance, OtherId, null);
        yield return () => ("anonymous", new Caller.Outsider(OutsiderKind.Anonymous), SelfId, "Sign in to do this.");
        yield return () => ("machine client", new Caller.Outsider(OutsiderKind.MachineClient), SelfId,
            "This client cannot change data.");
        yield return () => ("no user record", new Caller.Outsider(OutsiderKind.NoUserRecord), SelfId,
            "You need a user record in a tenant to do this.");
        yield return () => ("unknown client", new Caller.Outsider(OutsiderKind.UnknownClient), SelfId,
            UnknownClientDenial);
        yield return () => ("delegated agent on the record of its user", DelegatedAgentOf([Permission.ManageUsers()], []),
            SelfId, "The agent clinic-assistant is not granted the manage_users permission. A tenant admin can grant it.");
        yield return () => ("delegated agent on another user, with the permission in both",
            DelegatedAgentOf([Permission.ManageUsers()], [Permission.ManageUsers()]), OtherId, null);
    }

    [Test]
    [MethodDataSource(nameof(RequireManageUsersCases))]
    public void require_passes_only_for_a_caller_that_holds_the_permission(
        string @case, Caller caller, string? denial)
    {
        // Act
        var act = () => caller.Require(Permission.ManageUsers());

        // Assert
        ShouldDenyWith(act, denial);
    }

    [Test]
    [MethodDataSource(nameof(RequireSelfOrManageUsersCases))]
    public void require_self_or_passes_for_the_user_themselves_or_a_holder_of_the_permission(
        string @case, Caller caller, string userId, string? denial)
    {
        // Act
        var act = () => caller.RequireSelfOr(userId, Permission.ManageUsers());

        // Assert
        ShouldDenyWith(act, denial);
    }

    [Test]
    public void require_of_many_names_the_first_permission_that_a_member_lacks()
    {
        // Arrange
        var caller = MemberWith(Permission.ManageUserAccess());

        // Act
        var act = () => caller.Require([Permission.ManageUserAccess(), Permission.ManageTenant(), Permission.ManageUsers()]);

        // Assert
        act.ShouldThrow<ForbiddenAccessException>().Message
            .ShouldBe("You need the manage_tenant permission to do this.");
    }

    [Test]
    public void require_of_many_passes_for_a_member_that_holds_each_one()
    {
        // Arrange
        var caller = MemberWith(Permission.ManageUsers(), Permission.ManageTenant());

        // Act
        var act = () => caller.Require([Permission.ManageUsers(), Permission.ManageTenant()]);

        // Assert
        act.ShouldNotThrow();
    }

    [Test]
    public void require_of_many_passes_for_an_all_tenants_manager()
    {
        // Act
        var act = () => Caller.AllTenantsManager.Instance.Require(Permission.GetAll());

        // Assert
        act.ShouldNotThrow();
    }

    [Test]
    public void require_of_none_passes_for_any_caller()
    {
        // Act
        var act = () => new Caller.Outsider(OutsiderKind.Anonymous).Require([]);

        // Assert
        act.ShouldNotThrow();
    }

    public static IEnumerable<Func<(string Case, Caller Caller, Permission[] Held)>> PermissionSetCases()
    {
        yield return () => ("all tenants manager", Caller.AllTenantsManager.Instance, [.. Permission.GetAll()]);
        yield return () => ("member", MemberWith(Permission.ManageUsers(), Permission.ManageTenant()),
            [Permission.ManageUsers(), Permission.ManageTenant()]);
        yield return () => ("member with no permission", MemberWith(), []);
        yield return () => ("anonymous", new Caller.Outsider(OutsiderKind.Anonymous), []);
        yield return () => ("machine client", new Caller.Outsider(OutsiderKind.MachineClient), []);
        yield return () => ("unknown client", new Caller.Outsider(OutsiderKind.UnknownClient), []);
        yield return () => ("delegated agent",
            DelegatedAgentOf(
                [Permission.ManageUsers(), Permission.ManageTenant()],
                [Permission.ManageTenant(), Permission.ManageAgents()]),
            [Permission.ManageTenant()]);
        yield return () => ("machine agent", MachineAgentWith(Permission.ManageTenant()), [Permission.ManageTenant()]);
    }

    [Test]
    [MethodDataSource(nameof(PermissionSetCases))]
    public void permissions_hold_exactly_what_require_lets_through(string @case, Caller caller, Permission[] held)
    {
        // Assert
        caller.Permissions.ShouldBe(held, ignoreOrder: true);
        foreach (var permission in Permission.GetAll())
        {
            caller.Has(permission).ShouldBe(held.Contains(permission));
            ShouldDenyWith(() => caller.Require(permission), held.Contains(permission) ? null : DenialOf(caller, permission));
        }
    }

    [Test]
    public void permissions_of_a_member_cannot_change_after_the_load()
    {
        // Arrange
        var rows = new List<Permission> { Permission.ManageUsers() };
        var caller = new Caller.Member(SelfId, rows);

        // Act
        rows.Add(Permission.ManageTenant());
        var add = () => ((ICollection<Permission>)caller.Permissions).Add(Permission.ManageTenant());

        // Assert
        add.ShouldThrow<NotSupportedException>();
        caller.Permissions.ShouldBe([Permission.ManageUsers()]);
    }

    private static string DenialOf(Caller caller, Permission permission) => caller switch
    {
        Caller.Member => $"You need the {permission.Value} permission to do this.",
        Caller.DelegatedAgent agent when !agent.UserPermissions.Contains(permission)
            => $"You need the {permission.Value} permission to do this.",
        Caller.DelegatedAgent agent => NotGranted(agent.ClientId, permission),
        Caller.MachineAgent agent => NotGranted(agent.ClientId, permission),
        Caller.Outsider { Kind: OutsiderKind.Anonymous } => "Sign in to do this.",
        Caller.Outsider { Kind: OutsiderKind.MachineClient } => "This client cannot change data.",
        Caller.Outsider { Kind: OutsiderKind.UnknownClient } => UnknownClientDenial,
        _ => throw new ArgumentOutOfRangeException(nameof(caller)),
    };

    private static string NotGranted(string clientId, Permission permission)
        => $"The agent {clientId} is not granted the {permission.Value} permission. A tenant admin can grant it.";

    public static IEnumerable<Func<(string Case, Caller Caller, bool Manages)>> ManagesAllTenantsCases()
    {
        yield return () => ("all tenants manager", Caller.AllTenantsManager.Instance, true);
        yield return () => ("member with every permission", MemberWith([.. Permission.GetAll()]), false);
        yield return () => ("read-only machine client", new Caller.Outsider(OutsiderKind.MachineClient), false);
        yield return () => ("unknown client", new Caller.Outsider(OutsiderKind.UnknownClient), false);
        yield return () => ("machine agent with every permission", MachineAgentWith([.. Permission.GetAll()]), false);
    }

    [Test]
    [MethodDataSource(nameof(ManagesAllTenantsCases))]
    public void only_an_all_tenants_manager_manages_all_tenants(string @case, Caller caller, bool manages)
    {
        // Act
        var act = caller.RequireManagesAllTenants;

        // Assert
        caller.ManagesAllTenants.ShouldBe(manages);
        ShouldDenyWith(act, manages ? null : "Only a client that can manage all tenants can do this.");
    }

    public static IEnumerable<Func<(string Case, Caller Caller, string? Denial)>> RecognizedCases()
    {
        yield return () => ("all tenants manager", Caller.AllTenantsManager.Instance, null);
        yield return () => ("member with no permission", MemberWith(), null);
        yield return () => ("read-only machine client", new Caller.Outsider(OutsiderKind.MachineClient), null);
        yield return () => ("no user record", new Caller.Outsider(OutsiderKind.NoUserRecord), null);
        yield return () => ("delegated agent with no permission", DelegatedAgentOf([], []), null);
        yield return () => ("machine agent with no permission", MachineAgentWith(), null);
        yield return () => ("anonymous", new Caller.Outsider(OutsiderKind.Anonymous), "Sign in to do this.");
        yield return () => ("unknown client", new Caller.Outsider(OutsiderKind.UnknownClient), UnknownClientDenial);
    }

    [Test]
    [MethodDataSource(nameof(RecognizedCases))]
    public void only_an_unknown_client_and_an_anonymous_caller_are_not_recognized(
        string @case, Caller caller, string? denial)
    {
        // Act
        var act = caller.RequireRecognized;

        // Assert
        caller.IsRecognized.ShouldBe(denial is null);
        ShouldDenyWith(act, denial);
    }

    private static void ShouldDenyWith(Action act, string? denial)
    {
        if (denial is null)
            act.ShouldNotThrow();
        else
            act.ShouldThrow<ForbiddenAccessException>().Message.ShouldBe(denial);
    }
}
