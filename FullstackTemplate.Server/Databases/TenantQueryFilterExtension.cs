namespace FullstackTemplate.Server.Databases;

using System.Linq.Expressions;
using Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Query;
using Resources;

public static class TenantQueryFilterExtension
{
    /// <summary>
    /// Applies tenant filtering to all entities implementing ITenantable.
    /// Filter logic: CurrentTenantId == null (allow all) OR entity.TenantId == CurrentTenantId
    /// </summary>
    public static void FilterByTenant(this ModelBuilder modelBuilder, AppDbContext context)
    {
        foreach (var mutableEntityType in modelBuilder.Model.GetEntityTypes()
                     .Where(m => m.ClrType.IsAssignableTo(typeof(ITenantable))))
        {
            var entityType = mutableEntityType.ClrType;

            // Check for navigation path annotation first
            var tenantPathAnnotation = mutableEntityType.FindAnnotation("TenantNavigationPath");
            if (tenantPathAnnotation?.Value is LambdaExpression pathExpr)
            {
                // Build filter using navigation path
                var filterLambda = BuildNavigationFilter(entityType, pathExpr, context);
                mutableEntityType.SetQueryFilter(QueryFilterNames.Tenant, filterLambda);
            }
            else
            {
                // Direct ITenantable implementation
                var filterLambda = BuildDirectFilter(entityType, context);
                mutableEntityType.SetQueryFilter(QueryFilterNames.Tenant, filterLambda);
            }
        }
    }

    private static LambdaExpression BuildDirectFilter(Type entityType, AppDbContext context)
    {
        var parameter = Expression.Parameter(entityType, "e");

        // e.TenantId
        var tenantIdProperty = Expression.Property(parameter, nameof(ITenantable.TenantId));

        // context.CurrentTenantId
        var contextExpr = Expression.Constant(context);
        var currentTenantIdExpr = Expression.Property(contextExpr, nameof(AppDbContext.CurrentTenantId));

        // Build: context.CurrentTenantId == null || e.TenantId == context.CurrentTenantId.Value
        var nullCheck = Expression.Equal(
            currentTenantIdExpr,
            Expression.Constant(null, typeof(Guid?)));

        var tenantIdAsNullable = Expression.Convert(tenantIdProperty, typeof(Guid?));
        var tenantMatch = Expression.Equal(tenantIdAsNullable, currentTenantIdExpr);

        var filterBody = Expression.OrElse(nullCheck, tenantMatch);
        return Expression.Lambda(filterBody, parameter);
    }

    private static LambdaExpression BuildNavigationFilter(Type entityType, LambdaExpression pathExpr, AppDbContext context)
    {
        var parameter = Expression.Parameter(entityType, "e");

        // Rewrite the path expression to use new parameter
        var navigationBody = ReplacingExpressionVisitor.Replace(
            pathExpr.Parameters[0],
            parameter,
            pathExpr.Body);

        // context.CurrentTenantId
        var contextExpr = Expression.Constant(context);
        var currentTenantIdExpr = Expression.Property(contextExpr, nameof(AppDbContext.CurrentTenantId));

        // Build: context.CurrentTenantId == null || navigation.TenantId == context.CurrentTenantId
        var nullCheck = Expression.Equal(
            currentTenantIdExpr,
            Expression.Constant(null, typeof(Guid?)));

        var tenantIdAsNullable = Expression.Convert(navigationBody, typeof(Guid?));
        var tenantMatch = Expression.Equal(tenantIdAsNullable, currentTenantIdExpr);

        var filterBody = Expression.OrElse(nullCheck, tenantMatch);
        return Expression.Lambda(filterBody, parameter);
    }
}
