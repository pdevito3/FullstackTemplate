namespace FullstackTemplate.Server.Databases;

using System.Linq.Expressions;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

public static class TenantingExtension
{
    /// <summary>
    /// Applies tenant filtering through a navigation path.
    /// Use this for entities that don't directly implement ITenantable but have
    /// a navigation property chain that leads to a TenantId.
    ///
    /// Example usage:
    /// builder.ApplyTenanting(x => x.Test.Panel.TenantId);
    /// </summary>
    /// <param name="builder">The entity type builder.</param>
    /// <param name="tenantIdPath">Expression specifying the path to TenantId through navigation properties.</param>
    /// <typeparam name="TEntity">The entity type being configured.</typeparam>
    /// <returns>The entity type builder for method chaining.</returns>
    public static EntityTypeBuilder<TEntity> ApplyTenanting<TEntity>(
        this EntityTypeBuilder<TEntity> builder,
        Expression<Func<TEntity, Guid>> tenantIdPath)
        where TEntity : class
    {
        // Store the expression as metadata for the query filter to use
        builder.Metadata.AddAnnotation("TenantNavigationPath", tenantIdPath);
        return builder;
    }
}
