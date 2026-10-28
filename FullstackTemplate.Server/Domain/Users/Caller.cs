namespace FullstackTemplate.Server.Domain.Users;

using System.Collections.Frozen;
using System.Diagnostics;
using Exceptions;

/// <summary>
/// The sender of the current request, as far as permissions go. Each kind of caller is one variant.
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
    /// False for a client that no tenant registered and for a request with no authenticated principal. The MCP filters
    /// hide even the read tools from such a caller, and its calls say why. Each other caller can call a read tool, and
    /// the tenant filter decides the rows. A user with no user record is recognized and sees no rows.
    /// </summary>
    public bool IsRecognized => this is not Outsider { Kind: OutsiderKind.UnknownClient or OutsiderKind.Anonymous };

    /// <summary>
    /// True only for <see cref="AllTenantsManager"/>: it can act outside one tenant, for example add a tenant. It is a
    /// capability, not a permission (ADR 0009).
    /// </summary>
    public bool ManagesAllTenants => this is AllTenantsManager;

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

    /// <summary>
    /// A registered delegated agent that acts for a member. It holds only the permissions that both the member and the
    /// grant of the agent have, so it is never stronger than its user or its grant. It is not a <see cref="Member"/>,
    /// so <see cref="RequireSelfOr"/> gives it no exception for the record of its user.
    /// </summary>
    public sealed record DelegatedAgent : Caller
    {
        public DelegatedAgent(string clientId, IEnumerable<Permission> userPermissions, IEnumerable<Permission> grant)
        {
            ClientId = clientId;
            UserPermissions = userPermissions.ToFrozenSet();
            Permissions = UserPermissions.Intersect(grant).ToFrozenSet();
        }

        public string ClientId { get; }

        /// <summary>The rows of the user in <c>user_permissions</c>. A denial names the side that lacks it.</summary>
        public IReadOnlySet<Permission> UserPermissions { get; }

        public override IReadOnlySet<Permission> Permissions { get; }
    }

    /// <summary>A registered machine agent. It holds its grant, in the tenant of its record.</summary>
    public sealed record MachineAgent : Caller
    {
        public MachineAgent(string clientId, IEnumerable<Permission> grant)
        {
            ClientId = clientId;
            Permissions = grant.ToFrozenSet();
        }

        public string ClientId { get; }

        public override IReadOnlySet<Permission> Permissions { get; }
    }

    /// <summary>A caller that holds no permission.</summary>
    public sealed record Outsider(OutsiderKind Kind) : Caller
    {
        public override IReadOnlySet<Permission> Permissions => FrozenSet<Permission>.Empty;

        internal string Denial => Kind switch
        {
            OutsiderKind.Anonymous => "Sign in to do this.",
            OutsiderKind.MachineClient =>
                "This client has no permissions. It can read only the data that needs no permission.",
            OutsiderKind.NoUserRecord => "You need a user record in a tenant to do this.",
            OutsiderKind.UnknownClient =>
                "This client is not a registered agent of your tenant. A tenant admin can register it.",
            _ => throw new UnreachableException(),
        };
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

    /// <exception cref="ForbiddenAccessException">The caller cannot act outside one tenant.</exception>
    public void RequireManagesAllTenants()
    {
        if (!ManagesAllTenants)
            throw new ForbiddenAccessException("Only a client that can manage all tenants can do this.");
    }

    /// <exception cref="ForbiddenAccessException">The caller is not <see cref="IsRecognized"/>.</exception>
    public void RequireRecognized()
    {
        if (this is Outsider { Kind: OutsiderKind.UnknownClient or OutsiderKind.Anonymous } outsider)
            throw new ForbiddenAccessException(outsider.Denial);
    }

    /// <summary>True for the member with <paramref name="userId"/>, a user who acts on their own record.</summary>
    public bool IsUser(string userId) => this is Member member && member.UserId == userId;

    /// <summary>Passes for the member with <paramref name="userId"/>. Any other caller needs the permission.</summary>
    /// <exception cref="ForbiddenAccessException">The caller is another user and does not hold the permission.</exception>
    public void RequireSelfOr(string userId, Permission permission)
    {
        if (IsUser(userId))
            return;

        Require(permission);
    }

    private string DenialFor(Permission permission) => this switch
    {
        Member => UserLacks(permission),
        DelegatedAgent agent when !agent.UserPermissions.Contains(permission) => UserLacks(permission),
        DelegatedAgent agent => GrantLacks(agent.ClientId, permission),
        MachineAgent agent => GrantLacks(agent.ClientId, permission),
        Outsider outsider => outsider.Denial,
        _ => throw new UnreachableException(),
    };

    private static string UserLacks(Permission permission) => $"You need the {permission.Value} permission to do this.";

    private static string GrantLacks(string clientId, Permission permission)
        => $"The agent {clientId} is not granted the {permission.Value} permission. A tenant admin can grant it.";
}

/// <summary>Why a caller holds no permission.</summary>
public enum OutsiderKind
{
    /// <summary>The request has no authenticated principal.</summary>
    Anonymous,

    /// <summary>A client on <c>ReadAllTenantsClientIds</c> only.</summary>
    MachineClient,

    /// <summary>A signed-in user of a first-party client with no live user record in a tenant.</summary>
    NoUserRecord,

    /// <summary>
    /// A token from a client that is not first-party, is on no allow-list, and has no agent registered for the token.
    /// Also a token with no client claim, and an agent whose record or tenant is deleted during the request.
    /// </summary>
    UnknownClient,
}
