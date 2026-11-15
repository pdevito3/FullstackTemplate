namespace FullstackTemplate.Server.Domain.Users;

public abstract record AdminChange
{
    private AdminChange() { }

    public sealed record NewRole(UserRole Role) : AdminChange;
    public sealed record Removal : AdminChange;
}

public sealed record AdminCase
{
    public AdminCase(User user, AdminChange change, IReadOnlyCollection<string> adminIds)
    {
        if ((user.Role == UserRole.Admin()) != adminIds.Contains(user.Id))
            throw new ArgumentException("Give the admins of the tenant of the user.", nameof(adminIds));
        (User, Change, AdminIds) = (user, change, adminIds);
    }

    public User User { get; }
    public AdminChange Change { get; }
    public IReadOnlyCollection<string> AdminIds { get; }
}

public abstract record AdminRefusal
{
    private AdminRefusal(string field, string message) => (Field, Message) = (field, message);

    public string Field { get; }
    public string Message { get; }

    public sealed record LastAdmin(string Field, string Message) : AdminRefusal(Field, Message);
}

/// <summary>The proof that a change leaves the tenant of the user with an admin. Only <see cref="TenantAdmins.Decide"/> makes it.</summary>
public sealed class AdminKept
{
    internal AdminKept(string userId, AdminChange change) => (UserId, Change) = (userId, change);

    public string UserId { get; }
    public AdminChange Change { get; }
}

public abstract record AdminDecision
{
    private AdminDecision() { }

    public sealed record Kept(AdminKept Proof) : AdminDecision;
    public sealed record Refused(AdminRefusal Refusal) : AdminDecision;
}

/// <summary>UM-4: a tenant keeps at least one admin.</summary>
public static class TenantAdmins
{
    public const string LastAdminMessage = "This user is the only admin of the tenant. Make another user an admin first.";

    private static readonly Func<AdminCase, AdminRefusal?>[] Rules = [KeepsAnAdmin];

    public static AdminDecision Decide(AdminCase admins)
        => Rules.Select(rule => rule(admins)).FirstOrDefault(r => r is not null) is { } refusal
            ? new AdminDecision.Refused(refusal)
            : new AdminDecision.Kept(new AdminKept(admins.User.Id, admins.Change));

    private static AdminRefusal.LastAdmin? KeepsAnAdmin(AdminCase admins)
    {
        var staysAdmin = admins.Change is AdminChange.NewRole { Role: var role } && role == UserRole.Admin();
        var isTheOnlyAdmin = admins.AdminIds.Count == 1 && admins.AdminIds.Contains(admins.User.Id);
        return isTheOnlyAdmin && !staysAdmin ? new AdminRefusal.LastAdmin("role", LastAdminMessage) : null;
    }
}
