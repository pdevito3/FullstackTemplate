namespace FullstackTemplate.UnitTests.Databases;

using FullstackTemplate.Server.Databases;
using FullstackTemplate.Server.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata;
using Shouldly;

public class VersionColumnTests
{
    [Test]
    public void every_entity_maps_its_version_to_xmin_as_a_concurrency_token()
    {
        // Arrange
        using var context = new AppDbContextFactory().CreateDbContext([]);
        var model = context.GetService<IDesignTimeModel>().Model;
        var entityTypes = model.GetEntityTypes()
            .Where(e => e.ClrType.IsAssignableTo(typeof(BaseEntity)))
            .ToList();

        // Act
        var failures = entityTypes
            .Select(e => (Entity: e, Version: e.FindProperty(nameof(BaseEntity.Version))!))
            .Where(x => x.Version.GetColumnName() != "xmin"
                        || x.Version.GetColumnType() != "xid"
                        || !x.Version.IsConcurrencyToken
                        || x.Version.ValueGenerated != ValueGenerated.OnAddOrUpdate)
            .Select(x => x.Entity.DisplayName())
            .ToList();

        // Assert
        entityTypes.ShouldNotBeEmpty();
        failures.ShouldBeEmpty();
    }
}
