namespace FullstackTemplate.Server.Domain.Users;

public class UserPermission : BaseEntity
{
    public const string IdPrefix = "uperm";

    [NotSensitive] public string UserId { get; private set; } = default!;
    public User User { get; private set; } = default!;
    [NotSensitive] public Permission Permission { get; private set; } = default!;

    public static UserPermission Create(User user, Permission permission)
    {
        return new UserPermission
        {
            UserId = user.Id,
            Permission = permission
        };
    }

    protected UserPermission() : base(IdPrefix) { } // EF Core
}
