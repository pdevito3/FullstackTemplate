namespace FullstackTemplate.UnitTests.Databases;

using System.Reflection;
using FullstackTemplate.Server.Databases;
using FullstackTemplate.Server.Domain.DPoPProofs;
using FullstackTemplate.Server.Resources;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Migrations.Design;
using Microsoft.Extensions.DependencyInjection;
using Shouldly;

public class TenantQueryFilterTests
{
    private static readonly Type[] EntitiesWithNoTenantData = [typeof(UsedDPoPProof)];

    [Test]
    public void every_entity_has_the_tenant_filter_unless_it_is_named_as_an_entity_with_no_tenant_filter()
    {
        // Arrange
        using var context = new AppDbContextFactory().CreateDbContext([]);

        // Act
        var unfiltered = context.Model.GetEntityTypes()
            .Where(e => !e.IsOwned() && !HasTenantFilter(e))
            .Select(e => e.ClrType)
            .ToList();

        // Assert
        unfiltered.ShouldBe(EntitiesWithNoTenantData, ignoreOrder: true,
            "Give each entity the tenant filter, or add it to the list of this test that gives its reason. See docs/multi-tenancy.md.");
    }

    [Test]
    public void each_entity_that_refers_to_an_entity_with_the_tenant_filter_has_the_tenant_filter()
    {
        // Arrange
        using var context = new AppDbContextFactory().CreateDbContext([]);

        // Act
        var unfilteredChildren = context.Model.GetEntityTypes()
            .Where(e => !e.IsOwned() && !HasTenantFilter(e))
            .Where(e => e.GetForeignKeys().Any(fk => HasTenantFilter(fk.PrincipalEntityType)))
            .Select(e => e.ClrType)
            .ToList();

        // Assert
        unfilteredChildren.ShouldBeEmpty(
            "An entity that refers to a tenant row holds the data of that tenant. Call ApplyTenanting in its configuration. See docs/multi-tenancy.md.");
    }

    [Test]
    public void an_entity_tenanted_after_the_model_of_app_db_context_gets_the_tenant_filter()
    {
        // Arrange
        using var context = new NavigationTenantedContext();

        // Act
        var orderLine = context.Model.FindEntityType(typeof(OrderLine))!;

        // Assert
        HasTenantFilter(orderLine).ShouldBeTrue();
    }

    [Test]
    public void a_model_with_an_entity_tenanted_through_a_navigation_can_get_a_migration()
    {
        // Arrange
        using var context = new NavigationTenantedContext();
        var services = new ServiceCollection()
            .AddEntityFrameworkDesignTimeServices()
            .AddDbContextDesignTimeServices(context);
        ProviderDesignTimeServices().ConfigureDesignTimeServices(services);
        var scaffolder = services.BuildServiceProvider().GetRequiredService<IMigrationsScaffolder>();

        // Act
        var migration = scaffolder.ScaffoldMigration("AddOrderLines", "FullstackTemplate.UnitTests");

        // Assert
        migration.SnapshotCode.ShouldContain(nameof(OrderLine));
    }

    private static bool HasTenantFilter(IReadOnlyEntityType entityType)
        => entityType.GetRootType().FindDeclaredQueryFilter(QueryFilterNames.Tenant) is not null;

    // The same lookup as `dotnet ef`: the provider names its design-time services in an assembly attribute.
    private static IDesignTimeServices ProviderDesignTimeServices()
    {
        var provider = typeof(NpgsqlDbContextOptionsBuilderExtensions).Assembly;
        var typeName = provider.GetCustomAttribute<DesignTimeProviderServicesAttribute>()!.TypeName;
        return (IDesignTimeServices)Activator.CreateInstance(provider.GetType(typeName, throwOnError: true)!)!;
    }
}
