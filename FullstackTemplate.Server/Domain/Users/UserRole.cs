namespace FullstackTemplate.Server.Domain.Users;

using Ardalis.SmartEnum;
using Exceptions;

public class UserRole : ValueObject
{
    private UserRoleEnum _role = null!;

    public string Value
    {
        get => _role.Name;
        private set
        {
            if (!UserRoleEnum.TryFromName(value, true, out var parsed))
                throw new ValidationException(nameof(UserRole), $"Invalid role: {value}");

            _role = parsed;
        }
    }

    public UserRole(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
            throw new ValidationException(nameof(UserRole), "Role cannot be null or empty.");

        Value = value;
    }

    public Permission[] GetDefaultPermissions() => _role.GetDefaultPermissions();

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

        private class AdminType() : UserRoleEnum("Admin", 0)
        {
            public override Permission[] GetDefaultPermissions() =>
            [
                Permission.DoSomethingSpecial()
            ];
        }

        private class UserType() : UserRoleEnum("User", 1)
        {
            public override Permission[] GetDefaultPermissions() => [];
        }
    }
}
