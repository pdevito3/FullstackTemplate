namespace FullstackTemplate.Server.Databases;

using System.Reflection;
using Auditing;
using Domain;
using Domain.Tenants;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;
using Resources;

/// <summary>
/// The audit log of a deleted tenant stays: <c>AuditEntry</c> is not a <see cref="BaseEntity"/>, so no loader covers it.
/// </summary>
internal sealed class TenantOwnedRows
{
    private const string AnnotationName = "Tenancy:OwnedRows";

    private delegate Task<List<BaseEntity>> LoadRows(
        AppDbContext dbContext, string operationName, CancellationToken cancellationToken);

    private sealed record OwnedType(string OperationName, LoadRows Load);

    // A startup scan: it runs one time for each EF model, and the model keeps the loaders as a runtime annotation.
    private static readonly MethodInfo LoadRowsOfType = typeof(TenantOwnedRows)
        .GetMethod(nameof(LoadRowsAsync), BindingFlags.NonPublic | BindingFlags.Static)!;

    private readonly IReadOnlyList<OwnedType> _ownedTypes;

    private TenantOwnedRows(IModel model)
        => _ownedTypes = [.. InLoadOrder(model).Select(e => new OwnedType(
            $"tenants.delete.load_{e.GetTableName() ?? e.ClrType.Name}",
            LoadRowsOfType.MakeGenericMethod(e.ClrType).CreateDelegate<LoadRows>()))];

    /// <summary>
    /// Each owner loads before its children. A child add bumps the version of its owner, so a child that a rival adds
    /// after the owner loads makes the save fail its version check and the delete run again.
    /// </summary>
    internal static IReadOnlyList<IEntityType> InLoadOrder(IModel model)
    {
        var auditModel = AuditModel.Of(model);
        return [.. model.GetEntityTypes()
            .Where(e => e.BaseType is null
                && e.ClrType != typeof(Tenant)
                && e.ClrType.IsAssignableTo(typeof(BaseEntity))
                && e.FindDeclaredQueryFilter(QueryFilterNames.Tenant) is not null)
            .OrderBy(e => OwnerDepth(auditModel, e))];
    }

    private static int OwnerDepth(AuditModel auditModel, IEntityType entityType)
    {
        var seen = new HashSet<IEntityType> { entityType };
        while (auditModel.OwnerKeyOf(entityType)?.PrincipalEntityType.GetRootType() is { } owner && seen.Add(owner))
            entityType = owner;
        return seen.Count;
    }

    public static Task<List<BaseEntity>> LoadAsync(AppDbContext dbContext, string tenantId, CancellationToken cancellationToken)
    {
        var ownedTypes = dbContext.Model
            .GetOrAddRuntimeAnnotationValue(AnnotationName, static m => new TenantOwnedRows(m!), dbContext.Model)
            ._ownedTypes;
        return dbContext.InTenantScopeAsync(tenantId, async () =>
        {
            var rows = new List<BaseEntity>();
            foreach (var ownedType in ownedTypes)
                rows.AddRange(await ownedType.Load(dbContext, ownedType.OperationName, cancellationToken));
            return rows;
        });
    }

    private static async Task<List<BaseEntity>> LoadRowsAsync<TEntity>(
        AppDbContext dbContext, string operationName, CancellationToken cancellationToken)
        where TEntity : BaseEntity
        => [.. await dbContext.Set<TEntity>().TagWithOperationCallSite(operationName).ToListAsync(cancellationToken)];
}
