namespace FullstackTemplate.Server.Domain.Users;

using Ardalis.SmartEnum;
using Exceptions;

public partial class UserRole : ValueObject, IFixedNames
{
    // The key in the errors of the problem is the name of the request field
    private const string RequestField = "role";

    private UserRoleEnum _role = null!;

    public string Value
    {
        get => _role.Name;
        private set
        {
            if (!UserRoleEnum.TryFromName(value, true, out var parsed))
                throw new ValidationException(RequestField, $"Invalid role: {value}");

            _role = parsed;
        }
    }

    public UserRole(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
            throw new ValidationException(RequestField, "Role cannot be null or empty.");

        Value = value;
    }

    public Permission[] GetDefaultPermissions() => _role.GetDefaultPermissions();

    public override string ToString() => Value;

    public static UserRole Of(string value) => new(value);
    public static implicit operator string(UserRole value) => value.Value;
    public static List<string> ListNames() => UserRoleEnum.List.Select(x => x.Name).ToList();

    public static UserRole Admin() => new(UserRoleEnum.Admin.Name);
    public static UserRole User() => new(UserRoleEnum.User.Name);

    protected UserRole() { } // EF Core

    private abstract class UserRoleEnum(string name, int value)
        : SmartEnum<UserRoleEnum>(name, value)
    {
        public static readonly UserRoleEnum Admin = new AdminType();
        public static readonly UserRoleEnum User = new UserType();

        public abstract Permission[] GetDefaultPermissions();

        private sealed class AdminType() : UserRoleEnum("Admin", 0)
        {
            public override Permission[] GetDefaultPermissions() => [.. Permission.GetAll()];
        }

        private sealed class UserType() : UserRoleEnum("User", 1)
        {
            public override Permission[] GetDefaultPermissions() => [];
        }
    }
}
