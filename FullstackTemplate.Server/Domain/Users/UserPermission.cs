namespace FullstackTemplate.Server.Domain.Users;

public class UserPermission : BaseEntity
{
    public Guid UserId { get; private set; }
    public Permission Permission { get; private set; } = default!;

    public static UserPermission Create(User user, Permission permission)
    {
        return new UserPermission
        {
            UserId = user.Id,
            Permission = permission
        };
    }

    protected UserPermission() { } // EF Core
}
