namespace FullstackTemplate.UnitTests.Databases.Auditing;

using FullstackTemplate.Server.Databases;
using FullstackTemplate.Server.Databases.Auditing;
using FullstackTemplate.Server.Domain;
using FullstackTemplate.Server.Domain.Tenants;
using Microsoft.EntityFrameworkCore.Metadata;
using Shouldly;

/// <summary>The rules that the model of <see cref="AppDbContext"/> must keep, so the save can audit each change.</summary>
public class AuditModelTests
{
    [Test]
    public void each_aggregate_root_has_a_tenant_of_its_own()
    {
        // Arrange
        using var context = new AppDbContextFactory().CreateDbContext([]);
        var auditModel = AuditModel.Of(context.Model);

        // Act
        var rootsWithNoTenant = EntityTypes(context.Model)
            .Where(e => auditModel.OwnerKeyOf(e) is null)
            .Select(e => e.ClrType)
            .Where(type => type != typeof(Tenant) && !type.IsAssignableTo(typeof(ITenantable)))
            .ToList();

        // Assert
        rootsWithNoTenant.ShouldBeEmpty(
            "The audit log reads the tenant of a change from its aggregate root. Implement ITenantable on each entity, or give it an owner through a Cascade foreign key.");
    }

    [Test]
    public void no_entity_type_is_owned()
    {
        // Arrange
        using var context = new AppDbContextFactory().CreateDbContext([]);

        // Act
        var owned = context.Model.GetEntityTypes().Where(e => e.IsOwned()).Select(e => e.ClrType).ToList();

        // Assert
        owned.ShouldBeEmpty(
            "The audit log reads only entities and their complex properties. Map a value object with ComplexProperty, not OwnsOne.");
    }

    [Test]
    public void each_entity_type_has_a_name_of_its_own()
    {
        // Arrange
        using var context = new AppDbContextFactory().CreateDbContext([]);

        // Act
        var sharedNames = EntityTypes(context.Model)
            .GroupBy(e => e.ClrType.Name)
            .Where(group => group.Count() > 1)
            .Select(group => group.Key)
            .ToList();

        // Assert
        sharedNames.ShouldBeEmpty("An audit entry names its entity type by the class name, so two entity classes must not share a name.");
    }

    private static IEnumerable<IEntityType> EntityTypes(IModel model)
        => model.GetEntityTypes().Where(e => e.ClrType.IsAssignableTo(typeof(BaseEntity)));
}
