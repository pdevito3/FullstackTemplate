namespace FullstackTemplate.UnitTests.Databases;

using FullstackTemplate.Server.Databases;
using FullstackTemplate.Server.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata;
using Shouldly;

public class EntityIdColumnTests
{
    [Fact]
    public void every_id_and_id_foreign_key_has_the_entity_id_length_and_collation()
    {
        // Arrange
        using var context = new AppDbContextFactory().CreateDbContext([]);
        var model = context.GetService<IDesignTimeModel>().Model;
        var entityTypes = model.GetEntityTypes().Where(e => !e.IsOwned()).ToList();
        var idColumns = new HashSet<IProperty>();

        foreach (var entityType in entityTypes)
        {
            if (entityType.ClrType.IsAssignableTo(typeof(BaseEntity)))
                idColumns.Add(entityType.FindProperty(nameof(BaseEntity.Id))!);

            if (entityType.ClrType.IsAssignableTo(typeof(ITenantable)))
                idColumns.Add(entityType.FindProperty(nameof(ITenantable.TenantId))!);

            foreach (var foreignKey in entityType.GetForeignKeys()
                         .Where(fk => fk.PrincipalEntityType.ClrType.IsAssignableTo(typeof(BaseEntity))
                                      && fk.PrincipalKey.Properties is [{ Name: nameof(BaseEntity.Id) }]))
                idColumns.UnionWith(foreignKey.Properties);
        }

        // Act
        var failures = idColumns
            .Where(p => p.GetMaxLength() != EntityId.MaxLength || p.GetCollation() != EntityId.Collation)
            .Select(p => $"{p.DeclaringType.DisplayName()}.{p.Name}: max length {p.GetMaxLength()?.ToString() ?? "none"}, collation {p.GetCollation() ?? "none"}")
            .Order()
            .ToList();

        // Assert
        idColumns.ShouldNotBeEmpty();
        failures.ShouldBeEmpty();
    }
}
