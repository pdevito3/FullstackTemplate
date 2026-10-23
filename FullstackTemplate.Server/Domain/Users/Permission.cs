namespace FullstackTemplate.Server.Domain.Users;

using Ardalis.SmartEnum;
using Exceptions;

public partial class Permission : ValueObject
{
    // The key in the errors of the problem is the name of the request field
    private const string RequestField = "permission";

    private PermissionEnum _permission = null!;

    public string Value
    {
        get => _permission.Name;
        private set
        {
            if (!PermissionEnum.TryFromName(value, true, out var parsed))
                throw new ValidationException(RequestField, $"Invalid permission: {value}");

            _permission = parsed;
        }
    }

    public Permission(string value)
    {
        Value = value;
    }

    public static Permission Of(string value) => new(value);
    public static implicit operator string(Permission value) => value.Value;
    public static List<string> ListNames() => PermissionEnum.List.Select(x => x.Name).ToList();
    public static List<Permission> GetAll()
        => PermissionEnum.List.Select(x => new Permission(x.Name)).ToList();

    public static Permission ManageUsers() => new(PermissionEnum.ManageUsers.Name);
    public static Permission ManageUserAccess() => new(PermissionEnum.ManageUserAccess.Name);
    public static Permission ManageTenant() => new(PermissionEnum.ManageTenant.Name);

    protected Permission() { } // EF Core

    /// <summary>
    /// The name of each permission, as a constant for an attribute argument, for example
    /// <c>[ToolAccess(Permission.Names.ManageTenant)]</c>. The compiler checks the name.
    /// </summary>
    public static class Names
    {
        public const string ManageUsers = "manage_users";
        public const string ManageUserAccess = "manage_user_access";
        public const string ManageTenant = "manage_tenant";
    }

    private sealed class PermissionEnum(string name, int value) : SmartEnum<PermissionEnum>(name, value)
    {
        public static readonly PermissionEnum ManageUsers = new(Names.ManageUsers, 0);
        public static readonly PermissionEnum ManageUserAccess = new(Names.ManageUserAccess, 1);
        public static readonly PermissionEnum ManageTenant = new(Names.ManageTenant, 2);
    }
}
