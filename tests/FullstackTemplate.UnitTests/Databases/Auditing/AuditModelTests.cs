namespace FullstackTemplate.UnitTests.Databases.Auditing;

using FullstackTemplate.Server.Databases;
using FullstackTemplate.Server.Databases.Auditing;
using FullstackTemplate.Server.Domain;
using FullstackTemplate.Server.Domain.AuditEntries;
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
    public void each_entity_with_a_tenant_of_its_own_is_an_aggregate_root()
    {
        // Arrange
        using var context = new AppDbContextFactory().CreateDbContext([]);
        var auditModel = AuditModel.Of(context.Model);

        // Act
        var tenantedChildren = EntityTypes(context.Model)
            .Where(e => e.ClrType.IsAssignableTo(typeof(ITenantable)) && auditModel.OwnerKeyOf(e) is not null)
            .Select(e => e.ClrType)
            .ToList();

        // Assert
        tenantedChildren.ShouldBeEmpty(
            "AuditSubject.Of files an action under the entity that it names as the aggregate root. Remove ITenantable from a child, because its owner gives its tenant.");
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

    [Test]
    public void each_entity_type_that_the_save_does_not_audit_says_why()
    {
        // Arrange
        using var context = new AppDbContextFactory().CreateDbContext([]);

        // Act
        var unexplained = context.Model.GetEntityTypes()
            .Select(e => e.ClrType)
            .Where(type => !type.IsAssignableTo(typeof(BaseEntity)) && type != typeof(AuditEntry))
            .Where(type => string.IsNullOrWhiteSpace(
                ((NotAuditedAttribute?)Attribute.GetCustomAttribute(type, typeof(NotAuditedAttribute)))?.Reason))
            .ToList();

        // Assert
        unexplained.ShouldBeEmpty(
            "The save audits only a BaseEntity. Make the type a BaseEntity, or give it [NotAudited(reason)] with the reason that its rows need no audit.");
    }

    [Test]
    public void no_audited_entity_type_has_the_not_audited_mark()
    {
        // Arrange
        using var context = new AppDbContextFactory().CreateDbContext([]);

        // Act
        var marked = context.Model.GetEntityTypes()
            .Select(e => e.ClrType)
            .Where(type => (type.IsAssignableTo(typeof(BaseEntity)) || type == typeof(AuditEntry))
                           && Attribute.IsDefined(type, typeof(NotAuditedAttribute)))
            .ToList();

        // Assert
        marked.ShouldBeEmpty(
            "The save audits each BaseEntity, so the write guard must keep its table. Remove [NotAudited] from it.");
    }

    [Test]
    public void guarded_tables_are_the_audited_tables_and_the_audit_log()
    {
        // Arrange
        using var context = new AppDbContextFactory().CreateDbContext([]);

        // Act
        var guarded = AuditModel.Of(context.Model).GuardedTables;

        // Assert
        guarded.ShouldContain("users");
        guarded.ShouldContain("audit_entries");
        guarded.ShouldNotContain("used_dpop_proofs");
    }

    [Test]
    public void no_column_that_a_write_sends_gets_its_value_from_the_database_on_insert()
    {
        // Arrange
        using var context = new AppDbContextFactory().CreateDbContext([]);

        // Act
        var generated = EntityTypes(context.Model)
            .SelectMany(EntityColumn.Of)
            .Where(column => !column.Property.IsPrimaryKey() && column.Property.ValueGenerated == ValueGenerated.OnAdd)
            .Select(column => $"{column.Property.DeclaringType.DisplayName()}.{column.Property.Name}")
            .ToList();

        // Assert
        generated.ShouldBeEmpty(
            "The COPY of BulkInsertAsync sends the C# value of each column, and the audit entry records it, so a database default or an identity never applies. Set the value in the entity, and remove HasDefaultValueSql, HasDefaultValue, or the identity.");
    }

    private static IEnumerable<IEntityType> EntityTypes(IModel model)
        => model.GetEntityTypes().Where(e => e.ClrType.IsAssignableTo(typeof(BaseEntity)));
}
