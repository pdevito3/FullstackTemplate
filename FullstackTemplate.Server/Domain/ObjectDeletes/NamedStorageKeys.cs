namespace FullstackTemplate.Server.Domain.ObjectDeletes;

using System.Diagnostics.CodeAnalysis;
using System.Linq.Expressions;
using System.Reflection;
using Attachments;
using Databases;
using Databases.EntityConfigurations;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;

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
    // SAFETY: LoaderOf is a private static method of this type.
    private static readonly MethodInfo LoaderOfType = typeof(NamedStorageKeys)
        .GetMethod(nameof(LoaderOf), BindingFlags.NonPublic | BindingFlags.Static)!;

    private readonly IReadOnlyList<LoadKeys> _loaders;

    // SAFETY: LoaderOf returns a LoadKeys, and Invoke returns what LoaderOf returns.
    [SuppressMessage("ApiDesign", "RS0030:Do not use banned APIs",
        Justification = "Startup scan. It runs one time for each EF model, and the model keeps the loaders.")]
    private NamedStorageKeys(IModel model)
        => _loaders = [.. StoredFileMapping.StorageKeyColumns(model)
            .Select(column => (LoadKeys)LoaderOfType.MakeGenericMethod(column.EntityType.ClrType).Invoke(null, [column])!)];

    /// <summary>The keys of <paramref name="keys"/> that a live row names, in the tenant scope of the caller.</summary>
    public static async Task<HashSet<string>> AmongAsync(AppDbContext dbContext, IReadOnlyCollection<string> keys,
        CancellationToken cancellationToken)
    {
        // SAFETY: The third argument passes the model, so m is not null.
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
        return (dbContext, keys, cancellationToken) => dbContext.Set<TEntity>()
            .TagWithOperationCallSite(operationName)
            .Select(keyOf)
            .Where(key => keys.Contains(key))
            .ToListAsync(cancellationToken);
    }
}
