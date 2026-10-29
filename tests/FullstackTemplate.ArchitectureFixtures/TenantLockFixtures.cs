// Each type in this file is a fixture for TenantLockTests. A type named "...With..." adds a tenant-owned root with no
// shared lock of its tenant. A type named "Clean..." breaks no rule. Do not fix the violations.

namespace FullstackTemplate.ArchitectureFixtures.Core.TenantLock;

using FullstackTemplate.Server.Databases;
using FullstackTemplate.Server.Domain.Agents;
using FullstackTemplate.Server.Domain.Tenants;
using FullstackTemplate.Server.Domain.Users;

public static class CleanAddInSharedWork
{
    public static Task<Agent> Add(AppDbContext dbContext, Agent agent, CancellationToken cancellationToken)
        => RootLock.Tenant.RunSharedAsync(dbContext, agent.TenantId, async ct =>
        {
            await dbContext.Agents.AddAsync(agent, ct);
            await dbContext.SaveChangesAsync(ct);
            return agent;
        }, cancellationToken);
}

public static class CleanAddOfANewTenant
{
    public static async Task Add(AppDbContext dbContext, Tenant tenant, User user, CancellationToken cancellationToken)
    {
        await dbContext.Tenants.AddAsync(tenant, cancellationToken);
        await dbContext.Users.AddAsync(user, cancellationToken);
        await dbContext.SaveChangesAsync(cancellationToken);
    }
}

public static class CleanAddOfAChild
{
    public static Task Add(AppDbContext dbContext, UserPermission permission, CancellationToken cancellationToken)
    {
        dbContext.UserPermissions.Add(permission);
        return dbContext.SaveChangesAsync(cancellationToken);
    }
}

public static class AddWithNoLock
{
    public static async Task Add(AppDbContext dbContext, Agent agent, CancellationToken cancellationToken)
    {
        await dbContext.Agents.AddAsync(agent, cancellationToken);
        await dbContext.SaveChangesAsync(cancellationToken);
    }
}

public static class AddThroughTheContextWithNoLock
{
    public static Task Add(AppDbContext dbContext, User user, CancellationToken cancellationToken)
    {
        dbContext.Add(user);
        return dbContext.SaveChangesAsync(cancellationToken);
    }
}

public static class AddRangeWithNoLock
{
    public static Task Add(AppDbContext dbContext, IEnumerable<User> users, CancellationToken cancellationToken)
    {
        dbContext.Users.AddRange(users);
        return dbContext.SaveChangesAsync(cancellationToken);
    }
}

public static class AddRangeThroughTheContextWithNoLock
{
    public static Task Add(AppDbContext dbContext, Agent agent, User user, CancellationToken cancellationToken)
    {
        dbContext.AddRange(agent, user);
        return dbContext.SaveChangesAsync(cancellationToken);
    }
}

public static class BulkInsertWithNoLock
{
    public static Task Add(AppDbContext dbContext, IEnumerable<Agent> agents, CancellationToken cancellationToken)
        => dbContext.BulkInsertAsync(agents, cancellationToken);
}

public static class AddAfterTheSharedWork
{
    public static async Task Add(AppDbContext dbContext, Agent agent, CancellationToken cancellationToken)
    {
        await RootLock.Tenant.RunSharedAsync(dbContext, agent.TenantId, _ => Task.FromResult(true), cancellationToken);
        await dbContext.Agents.AddAsync(agent, cancellationToken);
        await dbContext.SaveChangesAsync(cancellationToken);
    }
}

public static class AddInExclusiveWork
{
    public static Task<Agent> Add(AppDbContext dbContext, Agent agent, CancellationToken cancellationToken)
        => RootLock.Tenant.RunExclusiveAsync(dbContext, agent.TenantId, async ct =>
        {
            await dbContext.Agents.AddAsync(agent, ct);
            await dbContext.SaveChangesAsync(ct);
            return agent;
        }, cancellationToken);
}
