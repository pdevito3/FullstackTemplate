namespace FullstackTemplate.Server.Domain.Users;

public abstract record ManagerChange
{
    private ManagerChange() { }

    internal abstract string Field { get; }

    internal abstract IEnumerable<Permission> PermissionsAfter(User user);

    public sealed record NewRoles(IReadOnlySet<UserRole> Roles) : ManagerChange
    {
        internal override string Field => "roles";

        internal override IEnumerable<Permission> PermissionsAfter(User user) => user.PermissionsWithRoles(Roles);
    }

    public sealed record PermissionRemoval(Permission Permission) : ManagerChange
    {
        internal override string Field => "permission";

        internal override IEnumerable<Permission> PermissionsAfter(User user) => user.CurrentPermissions().Where(p => p != Permission);
    }

    public sealed record Removal : ManagerChange
    {
        internal override string Field => "role";

        internal override IEnumerable<Permission> PermissionsAfter(User user) => [];
    }
}

public sealed record ManagerCase
{
    public ManagerCase(User user, ManagerChange change, IReadOnlyCollection<string> managerIds)
    {
        if (user.CanManageTenant != managerIds.Contains(user.Id))
            throw new ArgumentException("Give the managers of the tenant of the user.", nameof(managerIds));
        (User, Change, ManagerIds) = (user, change, managerIds);
    }

    public User User { get; }
    public ManagerChange Change { get; }
    public IReadOnlyCollection<string> ManagerIds { get; }
}

public abstract record ManagerRefusal
{
    private ManagerRefusal(string field, string message) => (Field, Message) = (field, message);

    public string Field { get; }
    public string Message { get; }

    public sealed record LastManager(string Field, string Message) : ManagerRefusal(Field, Message);
}

/// <summary>The proof that a change leaves the tenant of the user with a manager. Only <see cref="TenantManagers.Decide"/> makes it.</summary>
public sealed class ManagerKept
{
    internal ManagerKept(string userId, ManagerChange change) => (UserId, Change) = (userId, change);

    public string UserId { get; }
    public ManagerChange Change { get; }
}

public abstract record ManagerDecision
{
    private ManagerDecision() { }

    public sealed record Kept(ManagerKept Proof) : ManagerDecision;
    public sealed record Refused(ManagerRefusal Refusal) : ManagerDecision;
}

/// <summary>UM-4: a tenant keeps at least one user who can manage it.</summary>
public static class TenantManagers
{
    public const string LastManagerMessage =
        "This user is the only user who can manage the tenant. Make another user an admin first.";
    public const string FirstUserMessage = "The first user of a tenant must be able to manage it. Start it as an admin.";

    private static readonly Func<ManagerCase, ManagerRefusal?>[] Rules = [KeepsAManager];

    public static IReadOnlyList<Permission> Needs { get; } = Permission.GetAll();

    public static bool CanManage(IEnumerable<Permission> permissions) => !Needs.Except(permissions).Any();

    public static ManagerDecision Decide(ManagerCase managers)
        => Rules.Select(rule => rule(managers)).FirstOrDefault(r => r is not null) is { } refusal
            ? new ManagerDecision.Refused(refusal)
            : new ManagerDecision.Kept(new ManagerKept(managers.User.Id, managers.Change));

    private static ManagerRefusal.LastManager? KeepsAManager(ManagerCase managers)
    {
        var isTheOnlyManager = managers.ManagerIds.Count == 1 && managers.ManagerIds.Contains(managers.User.Id);
        var staysManager = CanManage(managers.Change.PermissionsAfter(managers.User));
        return isTheOnlyManager && !staysManager
            ? new ManagerRefusal.LastManager(managers.Change.Field, LastManagerMessage)
            : null;
    }
}
