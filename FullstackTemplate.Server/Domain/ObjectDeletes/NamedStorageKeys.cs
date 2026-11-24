namespace FullstackTemplate.Server.Domain.ObjectDeletes;

using System.Linq.Expressions;
using System.Reflection;
using Attachments;
using Databases;
using Databases.EntityConfigurations;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;
using Resources;

/// <summary>
/// The keys that the live rows name in each <see cref="StoredFile"/> column. The columns come from the EF model, so a
/// new stored file needs no change here. A soft-deleted row names no key, so the sweep deletes its object after the
/// grace, also when its save queued no delete.
/// </summary>
internal sealed class NamedStorageKeys
{
    private const string AnnotationName = "ObjectSweep:StorageKeyColumns";

    private delegate Task<List<string>> LoadKeys(
        AppDbContext dbContext, IReadOnlyCollection<string> keys, CancellationToken cancellationToken);

    // A startup scan: it runs one time for each EF model, and the model keeps the loaders as a runtime annotation.
    private static readonly MethodInfo LoaderOfType = typeof(NamedStorageKeys)
        .GetMethod(nameof(LoaderOf), BindingFlags.NonPublic | BindingFlags.Static)!;

    private readonly IReadOnlyList<LoadKeys> _loaders;

    private NamedStorageKeys(IModel model)
        => _loaders = [.. StoredFileMapping.StorageKeyColumns(model)
            .Select(column => (LoadKeys)LoaderOfType.MakeGenericMethod(column.EntityType.ClrType).Invoke(null, [column])!)];

    /// <summary>The keys of <paramref name="keys"/> that a live row names, in the tenant scope of the caller.</summary>
    public static async Task<HashSet<string>> AmongAsync(AppDbContext dbContext, IReadOnlyCollection<string> keys,
        CancellationToken cancellationToken)
    {
        var loaders = dbContext.Model
            .GetOrAddRuntimeAnnotationValue(AnnotationName, static m => new NamedStorageKeys(m!), dbContext.Model)
            ._loaders;
        var named = new HashSet<string>(StringComparer.Ordinal);
        foreach (var load in loaders)
            named.UnionWith(await load(dbContext, keys, cancellationToken));
        return named;
    }

    private static LoadKeys LoaderOf<TEntity>(StorageKeyColumn column)
        where TEntity : class
    {
        var row = Expression.Parameter(typeof(TEntity), "row");
        var keyOf = Expression.Lambda<Func<TEntity, string>>(
            column.PathToKey.Aggregate((Expression)row, Expression.MakeMemberAccess), row);
        var operationName = $"object_sweep.load_named_{column.EntityType.GetTableName() ?? column.EntityType.ClrType.Name}"
            + $".{column.StorageKey.GetColumnName()}";
        // Only the soft delete and the tenant scope decide which rows name a key. Another filter, such as one that
        // hides archived rows, hides live rows, and the sweep would delete their objects. The names come from the whole
        // model, because a tenant filter through a navigation joins the owner with the filters of the owner.
        string[] otherFilters = [.. column.EntityType.Model.GetEntityTypes()
            .SelectMany(entityType => entityType.GetDeclaredQueryFilters())
            .Select(filter => filter.Key)
            .OfType<string>()
            .Where(key => key is not (QueryFilterNames.SoftDelete or QueryFilterNames.Tenant))
            .Distinct(StringComparer.Ordinal)];
        return (dbContext, keys, cancellationToken) => (otherFilters.Length > 0
                ? dbContext.Set<TEntity>().IgnoreQueryFilters(otherFilters)
                : dbContext.Set<TEntity>())
            .TagWithOperationCallSite(operationName)
            .Select(keyOf)
            .Where(key => keys.Contains(key))
            .ToListAsync(cancellationToken);
    }
}
