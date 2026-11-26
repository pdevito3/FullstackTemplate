namespace FullstackTemplate.Server.Domain.Users;

/// <summary>One role of a user. A user can have any number of roles, each at most one time.</summary>
public class UserRoleAssignment : BaseEntity
{
    public const string IdPrefix = "urole";

    [NotSensitive] public string UserId { get; private set; } = default!;
    public User User { get; private set; } = default!;
    [NotSensitive] public UserRole Role { get; private set; } = default!;

    public static UserRoleAssignment Create(User user, UserRole role)
    {
        return new UserRoleAssignment
        {
            UserId = user.Id,
            Role = role
        };
    }

    protected UserRoleAssignment() : base(IdPrefix) { } // EF Core
}
