namespace FullstackTemplate.IntegrationTests.FeatureTests.Tenancy;

using System.Reflection;
using FullstackTemplate.Server.Databases;
using FullstackTemplate.Server.Domain;
using FullstackTemplate.Server.Domain.Tenants;
using FullstackTemplate.Server.Domain.Tenants.Features;
using FullstackTemplate.Server.Domain.Users;
using FullstackTemplate.Server.Resources;
using FullstackTemplate.SharedTestHelpers.EntityGraphs;
using Microsoft.EntityFrameworkCore;
using Shouldly;

/// <summary>
/// A tenant delete soft-deletes each row that the tenant owns. The test finds the tenant-owned types in the EF model,
/// not in a list, so its assert covers a new tenant-owned entity with no change. An app that adds one can prove its
/// delete with an arrange that adds a row of it.
/// </summary>
public class TenantDeleteOwnedRowsTests : TestBase
{
    private static readonly MethodInfo IdsOfType = typeof(TenantDeleteOwnedRowsTests)
        .GetMethod(nameof(LiveIdsAsync), BindingFlags.NonPublic | BindingFlags.Static)!;

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
        var owned = await scope.ExecuteDbContextAsync(db => LiveIdsOfEachOwnedTypeAsync(db, ids: null));
        owned.Where(type => type.Value.Count > 0).Select(type => type.Key)
            .ShouldBe(["Agent", "AgentPermission", "User", "UserPermission"], ignoreOrder: true);

        // Act
        await scope.SendAsync(new DeleteTenant.Command(tenant.Id));

        // Assert
        var stillLive = await new TestingServiceScope().ExecuteDbContextAsync(db => LiveIdsOfEachOwnedTypeAsync(db, owned));
        stillLive.Where(type => type.Value.Count > 0).ShouldBeEmpty();
    }

    /// <summary>
    /// The live IDs of each root type with the tenant filter, except the tenant. With <paramref name="ids"/>, only those
    /// IDs, in any tenant. Without them, the rows of the tenant of the caller.
    /// </summary>
    private static async Task<Dictionary<string, List<string>>> LiveIdsOfEachOwnedTypeAsync(
        AppDbContext db, Dictionary<string, List<string>>? ids)
    {
        var ownedTypes = db.Model.GetEntityTypes().Where(e => e.BaseType is null
            && e.ClrType != typeof(Tenant)
            && e.ClrType.IsAssignableTo(typeof(BaseEntity))
            && e.FindDeclaredQueryFilter(QueryFilterNames.Tenant) is not null);

        var live = new Dictionary<string, List<string>>();
        foreach (var type in ownedTypes)
        {
            var load = IdsOfType.MakeGenericMethod(type.ClrType);
            live[type.ClrType.Name] = await (Task<List<string>>)load.Invoke(null, [db, ids?[type.ClrType.Name]])!;
        }

        return live;
    }

    private static Task<List<string>> LiveIdsAsync<TEntity>(AppDbContext db, List<string>? ids) where TEntity : BaseEntity
        => ids is null
            ? db.Set<TEntity>().Select(e => e.Id).ToListAsync()
            : db.Set<TEntity>().IgnoreQueryFilters([QueryFilterNames.Tenant]).Where(e => ids.Contains(e.Id)).Select(e => e.Id).ToListAsync();
}
