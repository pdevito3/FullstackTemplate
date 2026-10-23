namespace FullstackTemplate.UnitTests.Databases;

using FullstackTemplate.Server.Databases;
using FullstackTemplate.Server.Domain.Agents;
using FullstackTemplate.Server.Domain.DPoPProofs;
using FullstackTemplate.Server.Domain.Users;
using FullstackTemplate.Server.Resources;
using Shouldly;

public class TenantQueryFilterTests
{
    private static readonly Type[] ChildrenReachedOnlyThroughTheirOwner = [typeof(UserPermission), typeof(AgentPermission)];

    private static readonly Type[] EntitiesWithNoTenantData = [typeof(UsedDPoPProof)];

    [Test]
    public void every_entity_has_the_tenant_filter_unless_it_is_named_as_an_entity_with_no_tenant_filter()
    {
        // Arrange
        using var context = new AppDbContextFactory().CreateDbContext([]);

        // Act
        var unfiltered = context.Model.GetEntityTypes()
            .Where(e => !e.IsOwned() && e.GetRootType().FindDeclaredQueryFilter(QueryFilterNames.Tenant) is null)
            .Select(e => e.ClrType)
            .ToList();

        // Assert
        unfiltered.ShouldBe([.. ChildrenReachedOnlyThroughTheirOwner, .. EntitiesWithNoTenantData], ignoreOrder: true,
            "Give each entity the tenant filter, or add it to the list of this test that gives its reason. See docs/multi-tenancy.md.");
    }
}
