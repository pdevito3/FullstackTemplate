namespace FullstackTemplate.Server.Domain.Users;

using Databases;
using Microsoft.EntityFrameworkCore;

public static class UserRowTracking
{
    /// <summary>
    /// Runs <paramref name="change"/> on a tracked user and tells the context which role, override, and permission
    /// rows it added or removed. The navigations use <c>PropertyAccessMode.Field</c>, so the context does not see an add
    /// or a remove by itself.
    /// </summary>
    /// <remarks>Load <see cref="User.UserRoles"/>, <see cref="User.PermissionOverrides"/>, and <see cref="User.UserPermissions"/> first.</remarks>
    public static void ChangeUser(this AppDbContext dbContext, User user, Action<User> change)
    {
        var roles = user.UserRoles.ToList();
        var overrides = user.PermissionOverrides.ToList();
        var permissions = user.UserPermissions.ToList();

        change(user);

        Track(dbContext.UserRoles, roles, user.UserRoles);
        Track(dbContext.UserPermissionOverrides, overrides, user.PermissionOverrides);
        Track(dbContext.UserPermissions, permissions, user.UserPermissions);
    }

    private static void Track<T>(DbSet<T> set, IReadOnlyCollection<T> before, IReadOnlyCollection<T> after)
        where T : BaseEntity
    {
        var afterIds = after.Select(x => x.Id).ToHashSet();
        var beforeIds = before.Select(x => x.Id).ToHashSet();
        set.RemoveRange(before.Where(x => !afterIds.Contains(x.Id)));
        set.AddRange(after.Where(x => !beforeIds.Contains(x.Id)));
    }
}
