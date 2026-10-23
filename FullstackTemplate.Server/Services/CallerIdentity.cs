namespace FullstackTemplate.Server.Services;

/// <summary>
/// Who sent the current request, before permissions. The tenant scope, the permission guard, and the start-a-tenant
/// check read this one answer. <see cref="ICallerIdentityResolver"/> makes it once for each DI scope.
/// It holds IDs only. The permission guard reads the permissions as rows for each check (ADR 0009).
/// </summary>
public abstract record CallerIdentity
{
    private CallerIdentity() { }

    /// <summary>System work in a scope from <see cref="SystemScopeExtensions.CreateSystemScope"/>.</summary>
    public sealed record System : CallerIdentity;

    /// <summary>
    /// A client on <see cref="TenancyOptions.ReadAllTenantsClientIds"/> or
    /// <see cref="TenancyOptions.ManageAllTenantsClientIds"/>, in a token with no app user.
    /// </summary>
    public sealed record AllTenantsClient(string ClientId, bool CanManage) : CallerIdentity;

    /// <summary>
    /// A user of a first-party client (the BFF or Swagger UI). <paramref name="TenantId"/> is null when the user has
    /// no live user record.
    /// </summary>
    public sealed record User(string Subject, string? TenantId) : CallerIdentity;

    /// <summary>
    /// A client that is not first-party, not on an allow-list, and not registered for this token. Also a token with
    /// no client claim. <paramref name="ClientId"/> is null for such a token.
    /// </summary>
    public sealed record UnknownClient(string? ClientId) : CallerIdentity;

    /// <summary>A request with no authenticated principal.</summary>
    public sealed record Anonymous : CallerIdentity;

    /// <summary>The tenants whose rows the caller sees.</summary>
    public TenantScope Scope => this switch
    {
        System or AllTenantsClient => TenantScope.AllTenants,
        User { TenantId: { } tenantId } => TenantScope.ForTenant(tenantId),
        _ => TenantScope.NoTenant,
    };

    /// <summary>
    /// True for system work and for a client on the manage allow-list. It is a capability, not a permission
    /// (ADR 0009).
    /// </summary>
    public bool CanManageAllTenants => this is System or AllTenantsClient { CanManage: true };
}
