namespace FullstackTemplate.Server.Domain.Users;

using Ardalis.SmartEnum;
using Exceptions;

public partial class Permission : ValueObject, IFixedNames
{
    // The key in the errors of the problem is the name of the request field
    private const string RequestField = "permission";

    private PermissionEnum _permission = null!;

    public string Value
    {
        get => _permission.Name;
        private set => _permission = Parse(value, RequestField);
    }

    public Permission(string value)
    {
        Value = value;
    }

    /// <summary>
    /// What the permission lets a user do, as a message says it after "You cannot", for example "manage users". A
    /// message shows these words, not <see cref="Value"/> (docs/messages.md).
    /// </summary>
    public string Words => _permission.Words;

    public override string ToString() => Value;

    public static Permission Of(string value) => new(value);

    /// <summary>
    /// The permission of a request field with another name, for example <c>permissions[1]</c> for an item of a list.
    /// The field is the key of the error when the name is not valid.
    /// </summary>
    public static Permission Of(string value, string requestField) => new() { _permission = Parse(value, requestField) };

    public static implicit operator string(Permission value) => value.Value;
    public static List<string> ListNames() => PermissionEnum.List.Select(x => x.Name).ToList();
    public static List<Permission> GetAll()
        => PermissionEnum.List.Select(x => new Permission(x.Name)).ToList();

    public static Permission ManageUsers() => new(PermissionEnum.ManageUsers.Name);
    public static Permission ManageUserAccess() => new(PermissionEnum.ManageUserAccess.Name);
    public static Permission ManageTenant() => new(PermissionEnum.ManageTenant.Name);
    public static Permission ManageAgents() => new(PermissionEnum.ManageAgents.Name);
    public static Permission ReadAuditLog() => new(PermissionEnum.ReadAuditLog.Name);
    public static Permission ReadAuditLogSensitive() => new(PermissionEnum.ReadAuditLogSensitive.Name);
    public static Permission ReadUsers() => new(PermissionEnum.ReadUsers.Name);
    public static Permission ReadAgents() => new(PermissionEnum.ReadAgents.Name);

    protected Permission() { } // EF Core

    private static PermissionEnum Parse(string value, string requestField)
        => PermissionEnum.TryFromName(value, true, out var parsed)
            ? parsed
            : throw new ValidationException(requestField, $"Invalid permission: {value}");

    /// <summary>
    /// The name of each permission, as a constant for an attribute argument, for example
    /// <c>[ToolAccess(Permission.Names.ManageTenant)]</c>. The compiler checks the name.
    /// </summary>
    public static class Names
    {
        public const string ManageUsers = "manage_users";
        public const string ManageUserAccess = "manage_user_access";
        public const string ManageTenant = "manage_tenant";
        public const string ManageAgents = "manage_agents";
        public const string ReadAuditLog = "read_audit_log";
        public const string ReadAuditLogSensitive = "read_audit_log_sensitive";
        public const string ReadUsers = "read_users";
        public const string ReadAgents = "read_agents";
    }

    private sealed class PermissionEnum(string name, int value, string words) : SmartEnum<PermissionEnum>(name, value)
    {
        public static readonly PermissionEnum ManageUsers = new(Names.ManageUsers, 0, "manage users");
        public static readonly PermissionEnum ManageUserAccess = new(Names.ManageUserAccess, 1, "manage the access of users");
        public static readonly PermissionEnum ManageTenant = new(Names.ManageTenant, 2, "manage the tenant");
        public static readonly PermissionEnum ManageAgents = new(Names.ManageAgents, 3, "manage agents");
        public static readonly PermissionEnum ReadAuditLog = new(Names.ReadAuditLog, 4, "read the audit log");
        public static readonly PermissionEnum ReadAuditLogSensitive =
            new(Names.ReadAuditLogSensitive, 5, "read the sensitive values of the audit log");
        public static readonly PermissionEnum ReadUsers = new(Names.ReadUsers, 6, "see the users of the tenant");
        public static readonly PermissionEnum ReadAgents = new(Names.ReadAgents, 7, "see the agents of the tenant");

        public string Words { get; } = words;
    }
}
