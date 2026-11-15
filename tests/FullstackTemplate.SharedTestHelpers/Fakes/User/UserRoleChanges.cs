namespace FullstackTemplate.SharedTestHelpers.Fakes.User;

using FullstackTemplate.Server.Domain.Users;

public static class UserRoleChanges
{
    public static User ChangeRoleIgnoringLastAdmin(this User user, UserRole role)
    {
        var adminIds = user.Role == UserRole.Admin() ? [user.Id, "usr_another_admin"] : new List<string>();
        var decision = TenantAdmins.Decide(new AdminCase(user, new AdminChange.NewRole(role), adminIds));
        return user.UpdateRole(((AdminDecision.Kept)decision).Proof);
    }
}
