namespace FullstackTemplate.Server.Domain.Users;

using System.Collections.Frozen;
using System.Diagnostics;
using Exceptions;

/// <summary>
/// The sender of the current request, as far as permissions go. Each kind of caller is one of three variants.
/// <see cref="IPermissionGuard"/> loads it. <see cref="Has"/> is the one decision, so a check that throws and a
/// check that only asks give the same answer.
/// </summary>
public abstract record Caller
{
    private Caller() { }

    /// <summary>
    /// Each permission that the caller holds in the current tenant. The set cannot change. Give it to an entity
    /// method whose rule depends on a permission and on the state of the entity.
    /// </summary>
    public abstract IReadOnlySet<Permission> Permissions { get; }

    public bool Has(Permission permission) => Permissions.Contains(permission);

    /// <summary>
    /// System work in a system scope, or a machine client in <c>ManageAllTenantsClientIds</c>. It holds every
    /// permission. The tenant filter still decides which rows it sees.
    /// </summary>
    public sealed record AllTenantsManager : Caller
    {
        private static readonly FrozenSet<Permission> Every = Permission.GetAll().ToFrozenSet();

        private AllTenantsManager() { }

        public static AllTenantsManager Instance { get; } = new();

        public override IReadOnlySet<Permission> Permissions => Every;
    }

    /// <summary>A signed-in user with a live user record. It holds only its rows in <c>user_permissions</c>.</summary>
    public sealed record Member : Caller
    {
        public Member(string userId, IEnumerable<Permission> permissions)
        {
            UserId = userId;
            Permissions = permissions.ToFrozenSet();
        }

        public string UserId { get; }

        public override IReadOnlySet<Permission> Permissions { get; }
    }

    /// <summary>A caller that holds no permission.</summary>
    public sealed record Outsider(OutsiderKind Kind) : Caller
    {
        public override IReadOnlySet<Permission> Permissions => FrozenSet<Permission>.Empty;
    }

    /// <exception cref="ForbiddenAccessException">The caller does not hold <paramref name="permission"/>.</exception>
    public void Require(Permission permission) => Require([permission]);

    /// <exception cref="ForbiddenAccessException">
    /// The caller does not hold each of <paramref name="permissions"/>. The message names the first one it lacks.
    /// </exception>
    public void Require(IEnumerable<Permission> permissions)
    {
        foreach (var permission in permissions)
        {
            if (!Has(permission))
                throw new ForbiddenAccessException(DenialFor(permission));
        }
    }

    /// <summary>Passes for the member with <paramref name="userId"/>. Any other caller needs the permission.</summary>
    /// <exception cref="ForbiddenAccessException">The caller is another user and does not hold the permission.</exception>
    public void RequireSelfOr(string userId, Permission permission)
    {
        if (this is Member member && member.UserId == userId)
            return;

        Require(permission);
    }

    private string DenialFor(Permission permission) => this switch
    {
        Member => $"You need the {permission.Value} permission to do this.",
        Outsider { Kind: OutsiderKind.Anonymous } => "Sign in to do this.",
        Outsider { Kind: OutsiderKind.MachineClient } => "This client cannot change data.",
        Outsider { Kind: OutsiderKind.NoUserRecord } => "You need a user record in a tenant to do this.",
        _ => throw new UnreachableException(),
    };
}

/// <summary>Why a caller holds no permission.</summary>
public enum OutsiderKind
{
    /// <summary>The request has no signed-in user and no machine client.</summary>
    Anonymous,

    /// <summary>A machine client that is not in <c>ManageAllTenantsClientIds</c>, for example a read-only client.</summary>
    MachineClient,

    /// <summary>A signed-in user with no live user record in a tenant.</summary>
    NoUserRecord,
}
