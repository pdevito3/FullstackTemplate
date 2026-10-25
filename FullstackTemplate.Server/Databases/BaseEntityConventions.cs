namespace FullstackTemplate.Server.Databases;

using System.Linq.Expressions;
using Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Microsoft.EntityFrameworkCore.Metadata.Conventions;
using Resources;

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

    public static void LoadOwnedChildrenWithOwner(this ModelConfigurationBuilder configurationBuilder)
        => configurationBuilder.ConfigureEachEntity(entityType =>
        {
            foreach (var ownerKey in entityType.GetForeignKeys().Where(fk => fk.IsOwnerKey()))
                ownerKey.PrincipalToDependent?.Builder.AutoInclude(true);
        });

    /// <summary>A Cascade foreign key to a <see cref="BaseEntity"/> makes its principal the owner of the dependent.</summary>
    internal static bool IsOwnerKey(this IReadOnlyForeignKey foreignKey)
        => foreignKey.DeleteBehavior == DeleteBehavior.Cascade
           && foreignKey.PrincipalEntityType.ClrType.IsAssignableTo(typeof(BaseEntity));

    /// <summary>Postgres has the <c>xmin</c> system column on each table, so the migration has no DDL.</summary>
    public static void MapVersionToXmin(this ModelConfigurationBuilder configurationBuilder)
        => configurationBuilder.Conventions.Add(_ => new VersionToXminConvention());

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

    // Maps the property when EF adds it, because EF gives the token to the other types of a shared table while the
    // model completes.
    private sealed class VersionToXminConvention : IPropertyAddedConvention
    {
        public void ProcessPropertyAdded(IConventionPropertyBuilder propertyBuilder,
            IConventionContext<IConventionPropertyBuilder> context)
        {
            var property = propertyBuilder.Metadata;
            if (property.Name != nameof(BaseEntity.Version)
                || !property.DeclaringType.ClrType.IsAssignableTo(typeof(BaseEntity)))
                return;

            property.SetColumnName("xmin");
            property.SetColumnType("xid");
            property.SetIsConcurrencyToken(true);
            // A convention computes the value generation again when the table mapping changes. It does not change a value
            // from a data annotation.
            property.SetValueGenerated(ValueGenerated.OnAddOrUpdate, fromDataAnnotation: true);
        }
    }
}
