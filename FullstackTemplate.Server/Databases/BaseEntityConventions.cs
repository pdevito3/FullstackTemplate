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
        => configurationBuilder.ConfigureEachRootEntity(entityType =>
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
        => configurationBuilder.ConfigureEachRootEntity(entityType =>
        {
            var idProperty = entityType.FindProperty(nameof(BaseEntity.Id))!;
            idProperty.SetField(BaseEntity.IdField);
            // Construction writes the field. Other reads use the getter, so a new entity whose ID was not read yet gets one.
            idProperty.SetPropertyAccessMode(PropertyAccessMode.PreferFieldDuringConstruction);
        });

    /// <summary>Postgres has the <c>xmin</c> system column on each table, so the migration has no DDL.</summary>
    public static void MapVersionToXmin(this ModelConfigurationBuilder configurationBuilder)
        => configurationBuilder.Conventions.Add(_ => new VersionToXminConvention());

    private static void ConfigureEachRootEntity(this ModelConfigurationBuilder configurationBuilder,
        Action<IConventionEntityType> configure)
        => configurationBuilder.Conventions.Add(_ => new EachRootEntityConvention(configure));

    private sealed class EachRootEntityConvention(Action<IConventionEntityType> configure) : IModelFinalizingConvention
    {
        public void ProcessModelFinalizing(IConventionModelBuilder modelBuilder,
            IConventionContext<IConventionModelBuilder> context)
        {
            foreach (var entityType in modelBuilder.Metadata.GetEntityTypes()
                         .Where(e => e.BaseType is null && e.ClrType.IsAssignableTo(typeof(BaseEntity))))
            {
                if (entityType.IsOwned())
                    throw new InvalidOperationException(
                        $"{entityType.ClrType.Name} is a BaseEntity that another entity owns. A BaseEntity must not be owned. " +
                        "Map a value object with ComplexProperty, or give a child entity its own key and a Cascade foreign key to its owner.");

                configure(entityType);
            }
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
