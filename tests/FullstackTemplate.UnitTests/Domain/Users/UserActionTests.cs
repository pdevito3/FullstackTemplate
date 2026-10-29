namespace FullstackTemplate.UnitTests.Domain.Users;

using FullstackTemplate.Server.Domain.Users;
using FullstackTemplate.Server.Domain.Users.Mappings;
using FullstackTemplate.Server.Exceptions;
using FullstackTemplate.SharedTestHelpers.EntityGraphs;
using Shouldly;

/// <summary>
/// <see cref="User.AllowedActions"/> lists an action only when <see cref="User.GuardAllows"/> lets it through, so a
/// button that the SPA shows from the list does not get a 403.
/// </summary>
public class UserActionTests
{
    private static readonly UserAction[] Actions =
    [
        UserAction.Update(), UserAction.UpdateRole(), UserAction.AddPermission(), UserAction.RemovePermission(),
        UserAction.Delete(),
    ];

    private static Caller.Member MemberWith(params Permission[] permissions) => new Caller.Member("usr_caller", permissions);

    private static readonly Func<User, Caller>[] Callers =
    [
        _ => Caller.AllTenantsManager.Instance,
        _ => new Caller.Outsider(OutsiderKind.Anonymous),
        _ => MemberWith(),
        OwnRecord,
        _ => MemberWith(Permission.ReadUsers()),
        _ => MemberWith(Permission.ManageUsers()),
        _ => MemberWith(Permission.ManageUsers(), Permission.ReadUsers()),
        _ => MemberWith(Permission.ManageUserAccess()),
        _ => MemberWith(Permission.ManageUserAccess(), Permission.ReadUsers()),
        _ => MemberWith([.. Permission.GetAll()]),
        _ => new Caller.DelegatedAgent("clinic-assistant", Permission.GetAll(), [Permission.ManageUsers()]),
    ];

    public static IEnumerable<Func<(string Case, string TargetRole, UserAction Action, Func<User, Caller> Caller, string? Denial)>>
        Cases()
    {
        const string manageUsersDenial = "You cannot manage users. Ask a tenant admin for access.";
        const string manageUserAccessDenial = "You cannot manage the access of users. Ask a tenant admin for access.";
        const string readUsersDenial = "You cannot see the users of the tenant. Ask a tenant admin for access.";
        // An admin holds each permission. The caller must hold them all to take them away, and manage_agents is first.
        const string adminPermissionDenial = "You cannot manage agents. Ask a tenant admin for access.";

        yield return () => ("user on their own record", "User", UserAction.Update(), OwnRecord, null);
        yield return () => ("user on another user", "User", UserAction.Update(), _ => MemberWith(), manageUsersDenial);
        yield return () => ("manager without read_users", "User", UserAction.Update(),
            _ => MemberWith(Permission.ManageUsers()), readUsersDenial);
        yield return () => ("manager with read_users", "User", UserAction.Update(),
            _ => MemberWith(Permission.ManageUsers(), Permission.ReadUsers()), null);

        yield return () => ("user on their own record", "User", UserAction.UpdateRole(),
            OwnRecord, manageUserAccessDenial);
        yield return () => ("admin on their own record", "Admin", UserAction.UpdateRole(), OwnRecord, null);
        yield return () => ("access manager without read_users", "User", UserAction.UpdateRole(),
            _ => MemberWith(Permission.ManageUserAccess()), readUsersDenial);
        yield return () => ("access manager on a user", "User", UserAction.UpdateRole(),
            _ => MemberWith(Permission.ManageUserAccess(), Permission.ReadUsers()), null);
        yield return () => ("access manager on an admin", "Admin", UserAction.UpdateRole(),
            _ => MemberWith(Permission.ManageUserAccess(), Permission.ReadUsers()), adminPermissionDenial);
        yield return () => ("admin on an admin", "Admin", UserAction.UpdateRole(),
            _ => MemberWith([.. Permission.GetAll()]), null);

        yield return () => ("user on another user", "User", UserAction.AddPermission(), _ => MemberWith(), manageUserAccessDenial);
        yield return () => ("access manager on an admin", "Admin", UserAction.AddPermission(),
            _ => MemberWith(Permission.ManageUserAccess(), Permission.ReadUsers()), null);
        yield return () => ("access manager without read_users", "User", UserAction.RemovePermission(),
            _ => MemberWith(Permission.ManageUserAccess()), readUsersDenial);
        yield return () => ("access manager on an admin", "Admin", UserAction.RemovePermission(),
            _ => MemberWith(Permission.ManageUserAccess(), Permission.ReadUsers()), null);

        yield return () => ("user on their own record", "User", UserAction.Delete(), OwnRecord, manageUsersDenial);
        yield return () => ("manager without read_users", "User", UserAction.Delete(),
            _ => MemberWith(Permission.ManageUsers()), null);
        yield return () => ("manager on an admin", "Admin", UserAction.Delete(),
            _ => MemberWith(Permission.ManageUsers()), adminPermissionDenial);
        yield return () => ("anonymous", "User", UserAction.Delete(),
            _ => new Caller.Outsider(OutsiderKind.Anonymous), "Sign in to do this.");
        yield return () => ("delegated agent whose grant lacks a permission of the user", "Admin", UserAction.Delete(),
            _ => new Caller.DelegatedAgent("clinic-assistant", Permission.GetAll(), [Permission.ManageUsers()]),
            "This agent has no grant to manage agents. A tenant admin can grant it.");
    }

    [Test]
    [MethodDataSource(nameof(Cases))]
    public void an_action_is_allowed_only_when_its_guard_passes(
        string @case, string targetRole, UserAction action, Func<User, Caller> callerOf, string? denial)
    {
        // Arrange
        var user = UserWithRole(targetRole);
        var caller = callerOf(user);

        // Act
        var allowedActions = user.AllowedActions(caller);
        var act = () => user.GuardAllows(action, caller);

        // Assert
        if (denial is null)
        {
            act.ShouldNotThrow();
            allowedActions.ShouldContain(action);
        }
        else
        {
            act.ShouldThrow<ForbiddenAccessException>().Message.ShouldBe(denial);
            allowedActions.ShouldNotContain(action);
        }
    }

    [Test]
    [Arguments("User")]
    [Arguments("Admin")]
    public void the_list_and_the_guards_agree_for_each_caller_and_action(string targetRole)
    {
        // Arrange
        var user = UserWithRole(targetRole);

        foreach (var callerOf in Callers)
        {
            var caller = callerOf(user);

            // Act
            var allowedActions = user.AllowedActions(caller);

            // Assert
            foreach (var action in Actions)
            {
                var guardPasses = Passes(() => user.GuardAllows(action, caller));
                allowedActions.Contains(action).ShouldBe(guardPasses, $"{action} for {caller}");
                if (guardPasses)
                    Passes(() => action.GuardBeforeLoad(caller, user.Id))
                        .ShouldBeTrue($"The check before the load refuses the allowed {action} for {caller}");
            }
        }
    }

    [Test]
    [Arguments("User")]
    [Arguments("Admin")]
    public void a_row_of_the_list_query_gets_the_actions_of_the_record(string targetRole)
    {
        // Arrange
        var user = UserWithRole(targetRole);

        foreach (var callerOf in Callers)
        {
            var caller = callerOf(user);
            var rows = new[] { user }.AsQueryable().ToUserDtoQueryable().ToList();

            // Act
            rows.AddAllowedActions(caller);

            // Assert
            rows.Single().AllowedActions.ShouldBe(user.AllowedActions(caller).Select(a => a.Value), $"for {caller}");
        }
    }

    [Test]
    public void the_list_keeps_the_order_of_the_table()
    {
        // Arrange
        var user = UserWithRole("User");

        // Act
        var allowedActions = user.AllowedActions(Caller.AllTenantsManager.Instance);

        // Assert
        allowedActions.ShouldBe(Actions);
    }

    [Test]
    public void on_their_own_record_a_member_gets_the_actions_after_a_change_of_their_role()
    {
        // Arrange
        var user = UserWithRole("Admin");
        var callerBeforeTheChange = OwnRecord(user);
        user.UpdateRole(UserRole.User());

        // Act
        var allowedActions = user.AllowedActions(callerBeforeTheChange);

        // Assert
        allowedActions.ShouldBe([UserAction.Update()]);
    }

    // A member holds the permission rows of their own user.
    private static Caller.Member OwnRecord(User user) => new Caller.Member(user.Id, user.UserPermissions.Select(p => p.Permission));

    private static User UserWithRole(string role)
    {
        TestGraph.Start(out _).AddTenant(out _).AddUser(out var user, u => u.Role = role);
        return user;
    }

    private static bool Passes(Action act)
    {
        try
        {
            act();
            return true;
        }
        catch (ForbiddenAccessException)
        {
            return false;
        }
    }
}
