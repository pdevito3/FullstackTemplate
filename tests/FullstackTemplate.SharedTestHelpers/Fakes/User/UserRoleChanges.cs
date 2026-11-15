namespace FullstackTemplate.SharedTestHelpers.Fakes.User;

using FullstackTemplate.Server.Domain.Users;

public static class UserRoleChanges
{
    public static User ChangeRoleIgnoringLastManager(this User user, UserRole role)
        => user.UpdateRole(KeptIgnoringLastManager(user, new ManagerChange.NewRole(role)));

    public static User RemovePermissionIgnoringLastManager(this User user, Permission permission)
        => user.RemovePermission(KeptIgnoringLastManager(user, new ManagerChange.PermissionRemoval(permission)));

    private static ManagerKept KeptIgnoringLastManager(User user, ManagerChange change)
    {
        var managerIds = user.CanManageTenant ? [user.Id, "usr_another_manager"] : new List<string>();
        return ((ManagerDecision.Kept)TenantManagers.Decide(new ManagerCase(user, change, managerIds))).Proof;
    }
}
