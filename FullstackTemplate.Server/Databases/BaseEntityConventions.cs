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
                GuardRowOfATable(entityType);
                configure(entityType);
            }
        }

        // The ID, the xmin version, the soft delete, and the audit log each need a row of a table with a key.
        private static void GuardRowOfATable(IConventionEntityType root)
        {
            const string readModelFix =
                "For a read model, use a class that is not a BaseEntity and give it [NotAudited(reason)], and ITenantable for the tenant filter.";

            if (root.IsOwned())
                throw new InvalidOperationException(
                    $"{root.ClrType.Name} is a BaseEntity that another entity owns. A BaseEntity must not be owned. " +
                    "Map a value object with ComplexProperty, or give a child entity its own key and a Cascade foreign key to its owner.");

            if (root.FindPrimaryKey()?.Properties is not [{ Name: nameof(BaseEntity.Id) }])
                throw new InvalidOperationException(
                    $"{root.ClrType.Name} is a BaseEntity whose key is not its Id alone. A BaseEntity is a row of a table with its Id as the key. " +
                    $"Give it HasKey(e => e.Id). {readModelFix}");

            if (root.GetDerivedTypesInclusive().FirstOrDefault(e =>
                    e.GetViewName() is not null || e.GetSqlQuery() is not null || e.GetFunctionName() is not null)
                is { } readsFromAView)
                throw new InvalidOperationException(
                    $"{readsFromAView.ClrType.Name} is a BaseEntity that maps to a view, a SQL query, or a function. " +
                    "A BaseEntity is a row of a table, and a query of it selects the xmin column of the table. " +
                    $"Map it only with ToTable. {readModelFix}");
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
