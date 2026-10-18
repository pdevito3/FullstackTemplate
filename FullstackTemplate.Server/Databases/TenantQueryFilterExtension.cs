namespace FullstackTemplate.Server.Databases;

using System.Linq.Expressions;
using Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Query;
using Resources;
using Services;

public static class TenantQueryFilterExtension
{
    /// <summary>
    /// Applies tenant filtering to all entities implementing ITenantable.
    /// Filter logic: scope.IncludesAllTenants OR (scope.TenantId != null AND entity.TenantId == scope.TenantId).
    /// For <see cref="TenantScope.NoTenant"/> both sides are false, so the filter matches no rows.
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

        return Expression.Lambda(BuildFilterBody(tenantIdProperty, context), parameter);
    }

    private static LambdaExpression BuildNavigationFilter(Type entityType, LambdaExpression pathExpr, AppDbContext context)
    {
        var parameter = Expression.Parameter(entityType, "e");

        // Rewrite the path expression to use new parameter
        var navigationBody = ReplacingExpressionVisitor.Replace(
            pathExpr.Parameters[0],
            parameter,
            pathExpr.Body);

        return Expression.Lambda(BuildFilterBody(navigationBody, context), parameter);
    }

    private static Expression BuildFilterBody(Expression entityTenantId, AppDbContext context)
    {
        // context.CurrentTenantScope. EF Core reads it again for each query.
        var scopeExpr = Expression.Property(Expression.Constant(context), nameof(AppDbContext.CurrentTenantScope));
        var includesAllTenants = Expression.Property(scopeExpr, nameof(TenantScope.IncludesAllTenants));
        var scopeTenantId = Expression.Property(scopeExpr, nameof(TenantScope.TenantId));

        // Build: scope.IncludesAllTenants || (scope.TenantId != null && tenantId == scope.TenantId)
        var hasTenant = Expression.NotEqual(scopeTenantId, Expression.Constant(null, typeof(Guid?)));
        var tenantMatch = Expression.Equal(Expression.Convert(entityTenantId, typeof(Guid?)), scopeTenantId);

        return Expression.OrElse(includesAllTenants, Expression.AndAlso(hasTenant, tenantMatch));
    }
}
