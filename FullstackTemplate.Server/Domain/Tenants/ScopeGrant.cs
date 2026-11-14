namespace FullstackTemplate.Server.Domain.Tenants;

using Domain.Users;

/// <summary>
/// The identity that Server code gives one DI scope with no request. Only
/// <see cref="SystemScopeExtensions.CreateSystemScope"/> and <see cref="Resources.Jobs.JobScope"/> grant it. The grant
/// applies to this scope only. It does not flow into other scopes or into work that this scope starts.
/// </summary>
public sealed class ScopeGrant
{
    public CallerIdentity? Identity { get; private set; }

    internal void GrantSystem() => Grant(new CallerIdentity.System());

    internal CallerIdentity.TenantJob GrantTenantJob(string tenantId) => Grant(new CallerIdentity.TenantJob(tenantId));

    private TIdentity Grant<TIdentity>(TIdentity identity) where TIdentity : CallerIdentity
    {
        if (Identity is not null)
            throw new InvalidOperationException(
                $"This DI scope already runs as {Identity.GetType().Name}. A scope gets one identity.");

        Identity = identity;
        return identity;
    }
}

public static class SystemScopeExtensions
{
    /// <summary>
    /// Creates a DI scope for system work that must see and manage all tenants, for example seeding or the REPL.
    /// A job of a tenant runs as that tenant, through <see cref="Resources.Jobs.JobHandler{TJob}"/>.
    /// </summary>
    /// <remarks>
    /// A scope from <see cref="IServiceScopeFactory.CreateScope"/> with no HTTP request gets
    /// <see cref="TenantScope.NoTenant"/>. All-tenant access must be an explicit decision.
    /// </remarks>
    public static AsyncServiceScope CreateSystemScope(this IServiceScopeFactory scopeFactory)
    {
        var scope = scopeFactory.CreateAsyncScope();
        scope.ServiceProvider.GetRequiredService<ScopeGrant>().GrantSystem();
        return scope;
    }
}
