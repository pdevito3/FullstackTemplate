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

    public UserRole(string value) : this(value, RequestField) { }

    private UserRole(string value, string requestField)
    {
        if (string.IsNullOrWhiteSpace(value))
            throw new ValidationException(requestField, "Role cannot be null or empty.");
        if (!UserRoleEnum.TryFromName(value, true, out var parsed))
            throw new ValidationException(requestField, $"Invalid role: {value}");

        _role = parsed;
    }

    /// <summary>
    /// The chunk of permissions that the role gives. A user holds the union of the permissions of each of their
    /// roles, with the permissions that someone added to the user and without the ones that someone removed.
    /// </summary>
    public Permission[] GetPermissions() => _role.GetPermissions();

    /// <summary>The union of the permissions of the roles.</summary>
    public static IReadOnlySet<Permission> PermissionsOf(IEnumerable<UserRole> roles)
        => roles.SelectMany(r => r.GetPermissions()).ToHashSet();

    public override string ToString() => Value;

    public static UserRole Of(string value) => new(value);

    /// <summary>
    /// The role of a request field with another name, for example <c>roles[1]</c> for an item of a list. The field is
    /// the key of the error when the name is not valid.
    /// </summary>
    public static UserRole Of(string value, string requestField) => new(value, requestField);
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

        public abstract Permission[] GetPermissions();

        private sealed class AdminType() : UserRoleEnum("Admin", 0)
        {
            public override Permission[] GetPermissions() => [.. Permission.GetAll()];
        }

        private sealed class UserType() : UserRoleEnum("User", 1)
        {
            public override Permission[] GetPermissions() => [];
        }
    }
}
