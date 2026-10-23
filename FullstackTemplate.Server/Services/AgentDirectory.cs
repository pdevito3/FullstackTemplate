namespace FullstackTemplate.Server.Services;

using Databases;
using Domain.Agents;
using Microsoft.EntityFrameworkCore;
using Resources;
using Resources.Extensions;

/// <summary>
/// Finds the live agent that a token names, for <see cref="ICallerIdentityResolver"/>. It gives IDs only. The
/// permission guard reads the grant for each check (ADR 0009).
/// </summary>
/// <remarks>
/// It does not cache, so a deleted agent or a deleted tenant has no scope on the next request on each instance. Only a
/// token from a client that is not first-party pays the query.
/// </remarks>
public interface IAgentDirectory
{
    /// <summary>The live machine agent with the client ID, in a live tenant, or null.</summary>
    Task<AgentBinding?> FindMachineAgentAsync(string clientId, CancellationToken cancellationToken = default);

    /// <summary>The ID of the live delegated agent of the live tenant with the client ID, or null.</summary>
    Task<string?> FindDelegatedAgentAsync(string tenantId, string clientId, CancellationToken cancellationToken = default);
}

/// <summary>A machine agent and the tenant that its record gives the token.</summary>
public sealed record AgentBinding(string AgentId, string TenantId);

public sealed class AgentDirectory(IServiceScopeFactory scopeFactory) : IAgentDirectory, ISingletonService
{
    public async Task<AgentBinding?> FindMachineAgentAsync(string clientId, CancellationToken cancellationToken = default)
    {
        await using var scope = scopeFactory.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        return await LiveAgentsOfAllTenants(dbContext)
            .TagWithOperationCallSite("agents.directory.find_machine")
            .Where(a => a.ClientId == clientId && a.Mode.Value == AgentMode.MachineValue)
            .Select(a => new AgentBinding(a.Id, a.TenantId))
            .FirstOrDefaultAsync(cancellationToken);
    }

    public async Task<string?> FindDelegatedAgentAsync(
        string tenantId, string clientId, CancellationToken cancellationToken = default)
    {
        await using var scope = scopeFactory.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        return await LiveAgentsOfAllTenants(dbContext)
            .TagWithOperationCallSite("agents.directory.find_delegated")
            .Where(a => a.TenantId == tenantId && a.ClientId == clientId && a.Mode.Value == AgentMode.DelegatedValue)
            .Select(a => a.Id)
            .FirstOrDefaultAsync(cancellationToken);
    }

    private static IQueryable<Agent> LiveAgentsOfAllTenants(AppDbContext dbContext)
        => dbContext.Agents
            .IgnoreQueryFilters([QueryFilterNames.Tenant])
            .Join(dbContext.Tenants, a => a.TenantId, t => t.Id, (a, _) => a);
}
