namespace FullstackTemplate.Server.Domain.Users;

/// <summary>
/// A permission that someone added to a user or removed from a user by hand, on top of the roles of the user. It stays
/// when the roles change. A user has at most one override for each permission.
/// </summary>
public class UserPermissionOverride : BaseEntity
{
    public const string IdPrefix = "upovr";

    [NotSensitive] public string UserId { get; private set; } = default!;
    public User User { get; private set; } = default!;
    [NotSensitive] public Permission Permission { get; private set; } = default!;

    /// <summary>True when the user holds the permission whatever their roles, false when they never hold it.</summary>
    public bool Granted { get; private set; }

    public static UserPermissionOverride Create(User user, Permission permission, bool granted)
    {
        return new UserPermissionOverride
        {
            UserId = user.Id,
            Permission = permission,
            Granted = granted
        };
    }

    protected UserPermissionOverride() : base(IdPrefix) { } // EF Core
}
