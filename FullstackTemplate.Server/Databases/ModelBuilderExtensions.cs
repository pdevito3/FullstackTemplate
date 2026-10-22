namespace FullstackTemplate.Server.Databases;

using System.Linq.Expressions;
using Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Query;
using Resources;

public static class ModelBuilderExtensions
{
    public static void FilterSoftDeletedRecords(this ModelBuilder modelBuilder)
    {
        Expression<Func<BaseEntity, bool>> filterExpr = e => !e.IsDeleted;
        foreach (var mutableEntityType in modelBuilder.Model.GetEntityTypes()
                     .Where(m => m.ClrType.IsAssignableTo(typeof(BaseEntity))))
        {
            // modify expression to handle correct child type
            var parameter = Expression.Parameter(mutableEntityType.ClrType);
            var body = ReplacingExpressionVisitor
                .Replace(filterExpr.Parameters.First(), parameter, filterExpr.Body);
            var lambdaExpression = Expression.Lambda(body, parameter);

            // set filter with name for selective disabling
            mutableEntityType.SetQueryFilter(QueryFilterNames.SoftDelete, lambdaExpression);
        }
    }

    /// <summary>
    /// Makes EF write the ID field of each entity when it loads a row. <see cref="BaseEntity.Id"/> has no setter
    /// and makes a new ID when the field is empty, so EF must not use the getter to set the loaded ID.
    /// </summary>
    public static void MapEntityIdToField(this ModelBuilder modelBuilder)
    {
        foreach (var mutableEntityType in modelBuilder.Model.GetEntityTypes()
                     .Where(m => m.ClrType.IsAssignableTo(typeof(BaseEntity))))
        {
            var idProperty = mutableEntityType.FindProperty(nameof(BaseEntity.Id))!;
            idProperty.SetField(BaseEntity.IdField);
            // Construction writes the field. Other reads use the getter, so a new entity whose ID was not read yet gets one.
            idProperty.SetPropertyAccessMode(PropertyAccessMode.PreferFieldDuringConstruction);
        }
    }

    /// <summary>Postgres has the <c>xmin</c> system column on each table, so the migration has no DDL.</summary>
    public static void MapVersionToXmin(this ModelBuilder modelBuilder)
    {
        foreach (var mutableEntityType in modelBuilder.Model.GetEntityTypes()
                     .Where(m => m.ClrType.IsAssignableTo(typeof(BaseEntity))))
        {
            modelBuilder.Entity(mutableEntityType.ClrType)
                .Property(nameof(BaseEntity.Version))
                .HasColumnName("xmin")
                .HasColumnType("xid")
                .IsRowVersion();
        }
    }
}
