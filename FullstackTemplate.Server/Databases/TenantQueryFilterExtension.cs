namespace FullstackTemplate.Server.Databases;

using System.Linq.Expressions;
using Domain;
using Domain.Tenants;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Microsoft.EntityFrameworkCore.Metadata.Conventions;
using Resources;
using Services;

public static class TenantQueryFilterExtension
{
    /// <summary>
    /// Applies the tenant filter to each entity type that has a tenant, when the model is complete.
    /// Filter logic: scope.IncludesAllTenants OR (scope.TenantId != null AND tenant ID of the row == scope.TenantId).
    /// For <see cref="TenantScope.NoTenant"/> both sides are false, so the filter matches no rows.
    /// </summary>
    public static void FilterByTenant(this ModelConfigurationBuilder configurationBuilder, AppDbContext context)
        => configurationBuilder.Conventions.Add(_ => new TenantQueryFilterConvention(context));

    private sealed class TenantQueryFilterConvention(AppDbContext context) : IModelFinalizingConvention
    {
        public void ProcessModelFinalizing(IConventionModelBuilder modelBuilder,
            IConventionContext<IConventionModelBuilder> conventionContext)
        {
            var entityTypes = modelBuilder.Metadata.GetEntityTypes().ToList();
            foreach (var root in entityTypes.Where(e => e.BaseType is null))
            {
                if (TenantIdPathOf(root) is { } tenantIdPath)
                    root.SetQueryFilter(QueryFilterNames.Tenant, BuildFilter(tenantIdPath, context));
            }

            foreach (var derived in entityTypes.Where(e => e.BaseType is not null))
                GuardNoTenantOfItsOwn(derived);

            // A migration snapshot cannot hold the lambda of the path. The filter holds it now.
            foreach (var entityType in entityTypes)
                entityType.RemoveAnnotation(TenantingExtension.NavigationPathAnnotation);
        }
    }

    public static string? TenantIdOfDomainEvent(EntityEntry<BaseEntity> queuedBy) => queuedBy.Entity switch
    {
        Tenant tenant => tenant.Id,
        ITenantable tenantable => tenantable.TenantId,
        _ when queuedBy.Metadata.GetRootType().FindDeclaredQueryFilter(QueryFilterNames.Tenant) is not null =>
            throw new NotSupportedException(
                $"{queuedBy.Metadata.ClrType.Name} gets its tenant through ApplyTenanting, so the save cannot find the tenant of its domain event. " +
                $"Implement ITenantable on {queuedBy.Metadata.ClrType.Name}, or queue the event on its tenanted owner."),
        _ => null,
    };

    private static void GuardNoTenantOfItsOwn(IReadOnlyEntityType derived)
    {
        var root = derived.GetRootType();
        var ownTenant = derived.FindAnnotation(TenantingExtension.NavigationPathAnnotation) is not null
            ? "the ApplyTenanting call"
            : derived.ClrType.IsAssignableTo(typeof(ITenantable)) && !root.ClrType.IsAssignableTo(typeof(ITenantable))
                ? "ITenantable"
                : null;
        if (ownTenant is null)
            return;

        var fix = root.FindDeclaredQueryFilter(QueryFilterNames.Tenant) is not null
            ? $"{root.ClrType.Name} has a tenant, and its filter covers {derived.ClrType.Name}. Remove {ownTenant} from {derived.ClrType.Name}."
            : $"Give the tenant only to {root.ClrType.Name}: implement ITenantable on it, or call ApplyTenanting in its configuration.";
        throw new InvalidOperationException(
            $"{derived.ClrType.Name} has a tenant of its own, but a query filter can go only on {root.ClrType.Name}, the root type of its hierarchy. {fix}");
    }

    private static LambdaExpression? TenantIdPathOf(IReadOnlyEntityType entityType)
    {
        if (entityType.FindAnnotation(TenantingExtension.NavigationPathAnnotation)?.Value is LambdaExpression navigationPath)
            return navigationPath;

        var row = Expression.Parameter(entityType.ClrType, "e");
        if (entityType.ClrType == typeof(Tenant))
            return Expression.Lambda(Expression.Property(row, nameof(Tenant.Id)), row);
        if (entityType.ClrType.IsAssignableTo(typeof(ITenantable)))
            return Expression.Lambda(Expression.Property(row, nameof(ITenantable.TenantId)), row);

        return null;
    }

    private static LambdaExpression BuildFilter(LambdaExpression tenantIdPath, AppDbContext context)
        => Expression.Lambda(BuildFilterBody(tenantIdPath.Body, context), tenantIdPath.Parameters);

    private static BinaryExpression BuildFilterBody(Expression entityTenantId, AppDbContext context)
    {
        // context.CurrentTenantScope. EF Core reads the property for each query, once for each use below.
        // The context keeps the scope, so only the first read calls the scope provider.
        var scopeExpr = Expression.Property(Expression.Constant(context), nameof(AppDbContext.CurrentTenantScope));
        var includesAllTenants = Expression.Property(scopeExpr, nameof(TenantScope.IncludesAllTenants));
        var scopeTenantId = Expression.Property(scopeExpr, nameof(TenantScope.TenantId));

        // Build: scope.IncludesAllTenants || (scope.TenantId != null && tenantId == scope.TenantId)
        var hasTenant = Expression.NotEqual(scopeTenantId, Expression.Constant(null, typeof(string)));
        var tenantMatch = Expression.Equal(entityTenantId, scopeTenantId);

        return Expression.OrElse(includesAllTenants, Expression.AndAlso(hasTenant, tenantMatch));
    }
}
