namespace FullstackTemplate.IntegrationTests.FeatureTests.Tenancy;

using System.Reflection;
using FullstackTemplate.Server.Databases;
using FullstackTemplate.Server.Domain;
using FullstackTemplate.Server.Domain.AuditEntries;
using FullstackTemplate.Server.Domain.Tenants;
using FullstackTemplate.Server.Domain.Tenants.Features;
using FullstackTemplate.Server.Domain.Users;
using FullstackTemplate.Server.Resources;
using FullstackTemplate.SharedTestHelpers.EntityGraphs;
using Microsoft.EntityFrameworkCore;
using Shouldly;

/// <summary>
/// The assert reads the tenant-owned types from the EF model, so it covers a new tenant-owned entity once the arrange
/// adds a row of it.
/// </summary>
public class TenantDeleteOwnedRowsTests : TestBase
{
    private static readonly MethodInfo IdsOfTenant = typeof(TenantDeleteOwnedRowsTests)
        .GetMethod(nameof(IdsOfTheTenantOfTheCallerAsync), BindingFlags.NonPublic | BindingFlags.Static)!;

    private static readonly MethodInfo LiveIds = typeof(TenantDeleteOwnedRowsTests)
        .GetMethod(nameof(LiveIdsInAnyTenantAsync), BindingFlags.NonPublic | BindingFlags.Static)!;

    [Test]
    public async Task tenant_delete_soft_deletes_each_row_of_each_tenant_owned_type()
    {
        // Arrange
        var scope = new TestingServiceScope();
        await scope.StartGraph(out _)
            .AddTenant(out var tenant)
            .AddUser(out var admin, u => u.Role = UserRole.Admin().Value)
            .Under(tenant)
            .AddAgent(out _, a => a.Permissions = [Permission.Names.ReadAgents])
            .BuildAsync();
        scope.SetUserWithDefaults(identifier: admin.Identifier);
        var owned = await scope.ExecuteDbContextAsync(db => EachOwnedTypeAsync(db, type => Run(IdsOfTenant, type, db)));
        owned.Where(type => type.Value.Count > 0).Select(type => type.Key)
            .ShouldBe(["Agent", "AgentPermission", "User", "UserPermission"], ignoreOrder: true);

        // Act
        await scope.SendAsync(new DeleteTenant.Command(tenant.Id));

        // Assert
        var stillLive = await new TestingServiceScope().ExecuteDbContextAsync(db =>
            EachOwnedTypeAsync(db, type => Run(LiveIds, type, db, owned[type.Name])));
        stillLive.Where(type => type.Value.Count > 0).ShouldBeEmpty();
    }

    /// <summary>Each root type with the tenant filter, except the tenant and the audit log, which keeps its entries.</summary>
    private static async Task<Dictionary<string, List<string>>> EachOwnedTypeAsync(
        AppDbContext db, Func<Type, Task<List<string>>> idsOf)
    {
        var ownedTypes = db.Model.GetEntityTypes().Where(e => e.BaseType is null
            && e.ClrType != typeof(Tenant)
            && e.ClrType != typeof(AuditEntry)
            && e.FindDeclaredQueryFilter(QueryFilterNames.Tenant) is not null);

        var result = new Dictionary<string, List<string>>();
        foreach (var type in ownedTypes)
            result[type.ClrType.Name] = await idsOf(type.ClrType);

        return result;
    }

    private static Task<List<string>> Run(MethodInfo query, Type entityType, params object[] arguments)
        => (Task<List<string>>)query.MakeGenericMethod(entityType).Invoke(null, arguments)!;

    private static Task<List<string>> IdsOfTheTenantOfTheCallerAsync<TEntity>(AppDbContext db) where TEntity : BaseEntity
        => db.Set<TEntity>().Select(e => e.Id).ToListAsync();

    private static Task<List<string>> LiveIdsInAnyTenantAsync<TEntity>(AppDbContext db, List<string> ids) where TEntity : BaseEntity
        => db.Set<TEntity>().IgnoreQueryFilters([QueryFilterNames.Tenant]).Where(e => ids.Contains(e.Id)).Select(e => e.Id).ToListAsync();
}
