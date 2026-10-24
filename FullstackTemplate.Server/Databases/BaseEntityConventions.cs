namespace FullstackTemplate.Server.Databases;

using System.Linq.Expressions;
using Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Microsoft.EntityFrameworkCore.Metadata.Conventions;
using Resources;

/// <summary>
/// The model rules of each <see cref="BaseEntity"/>. Each rule runs when the model is complete, so it also applies to
/// an entity that a context configures after the model of <see cref="AppDbContext"/>.
/// </summary>
public static class BaseEntityConventions
{
    public static void FilterSoftDeletedRecords(this ModelConfigurationBuilder configurationBuilder)
        => configurationBuilder.ConfigureEachEntity(entityType =>
        {
            var row = Expression.Parameter(entityType.ClrType, "e");
            var notDeleted = Expression.Not(Expression.Property(row, nameof(BaseEntity.IsDeleted)));
            entityType.SetQueryFilter(QueryFilterNames.SoftDelete, Expression.Lambda(notDeleted, row));
        });

    /// <summary>
    /// Makes EF write the ID field of each entity when it loads a row. <see cref="BaseEntity.Id"/> has no setter
    /// and makes a new ID when the field is empty, so EF must not use the getter to set the loaded ID.
    /// </summary>
    public static void MapEntityIdToField(this ModelConfigurationBuilder configurationBuilder)
        => configurationBuilder.ConfigureEachEntity(entityType =>
        {
            var idProperty = entityType.FindProperty(nameof(BaseEntity.Id))!;
            idProperty.SetField(BaseEntity.IdField);
            // Construction writes the field. Other reads use the getter, so a new entity whose ID was not read yet gets one.
            idProperty.SetPropertyAccessMode(PropertyAccessMode.PreferFieldDuringConstruction);
        });

    /// <summary>Postgres has the <c>xmin</c> system column on each table, so the migration has no DDL.</summary>
    public static void MapVersionToXmin(this ModelConfigurationBuilder configurationBuilder)
        => configurationBuilder.ConfigureEachEntity(entityType =>
        {
            var versionProperty = entityType.FindProperty(nameof(BaseEntity.Version))!;
            versionProperty.SetColumnName("xmin");
            versionProperty.SetColumnType("xid");
            versionProperty.SetIsConcurrencyToken(true);
            versionProperty.SetValueGenerated(ValueGenerated.OnAddOrUpdate);
        });

    private static void ConfigureEachEntity(this ModelConfigurationBuilder configurationBuilder,
        Action<IConventionEntityType> configure)
        => configurationBuilder.Conventions.Add(_ => new EachEntityConvention(configure));

    private sealed class EachEntityConvention(Action<IConventionEntityType> configure) : IModelFinalizingConvention
    {
        public void ProcessModelFinalizing(IConventionModelBuilder modelBuilder,
            IConventionContext<IConventionModelBuilder> context)
        {
            foreach (var entityType in modelBuilder.Metadata.GetEntityTypes()
                         .Where(e => e.ClrType.IsAssignableTo(typeof(BaseEntity))))
                configure(entityType);
        }
    }
}
