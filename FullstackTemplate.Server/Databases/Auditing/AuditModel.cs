namespace FullstackTemplate.Server.Databases.Auditing;

using System.Reflection;
using Domain;
using Domain.AuditEntries;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;

/// <summary>
/// One value column of an entity type, with its audit facts. <c>Complex</c> is the complex property that holds the
/// property, or null. <c>IsRecorded</c> is false for the key and the <see cref="BaseEntity"/> bookkeeping (the stamps
/// and <c>IsDeleted</c>).
/// </summary>
internal sealed record AuditedValue(
    string Path, IProperty Property, IComplexProperty? Complex, bool IsRecorded, Sensitivity Sensitivity);

/// <summary>
/// What the audit log knows about the EF model of the context. It derives everything from the model, so a new
/// entity or column needs no list. The context builds it once for each model and keeps it as a runtime annotation.
/// </summary>
internal sealed class AuditModel
{
    private const string AnnotationName = "Audit:Model";

    private static readonly HashSet<string> Bookkeeping =
    [
        nameof(BaseEntity.CreatedOn), nameof(BaseEntity.CreatedBy), nameof(BaseEntity.CreatedByClient),
        nameof(BaseEntity.LastModifiedOn), nameof(BaseEntity.LastModifiedBy), nameof(BaseEntity.LastModifiedByClient),
        nameof(BaseEntity.IsDeleted),
    ];

    private readonly Dictionary<IEntityType, IReadOnlyList<AuditedValue>> _values = [];
    private readonly Dictionary<IEntityType, IForeignKey?> _ownerKeys = [];

    private AuditModel(IModel model)
    {
        foreach (var entityType in model.GetEntityTypes().Where(e => e.ClrType.IsAssignableTo(typeof(BaseEntity))))
        {
            _values[entityType] = ValuesOfType(entityType);
            _ownerKeys[entityType] = OwnerKeyOfType(entityType);
        }

        Vocabulary = AuditVocabulary.Of(_values, typeof(AuditAction).Assembly.GetTypes());
        GuardedTables = GuardedTablesOf(model);
    }

    /// <summary>The sensitivity of each value that the code of today can find in an audit entry.</summary>
    public AuditVocabulary Vocabulary { get; }

    /// <summary>
    /// The tables that only the save can write, so that each change has its audit entry: each table of the model,
    /// <c>audit_entries</c> too, except the tables of a <see cref="NotAuditedAttribute"/> type.
    /// </summary>
    public IReadOnlySet<string> GuardedTables { get; }

    public static AuditModel Of(IModel model)
        => model.GetOrAddRuntimeAnnotationValue(AnnotationName, static m => new AuditModel(m!), model);

    /// <summary>Every value column of a <see cref="BaseEntity"/> type except store-generated ones (<c>xmin</c>).</summary>
    public IReadOnlyList<AuditedValue> ValuesOf(IEntityType entityType) => _values[entityType];

    /// <summary>
    /// The foreign key to the owner, or null for an aggregate root. A Cascade foreign key to a
    /// <see cref="BaseEntity"/> makes its principal the owner.
    /// </summary>
    public IForeignKey? OwnerKeyOf(IEntityType entityType) => _ownerKeys[entityType];

    private static List<AuditedValue> ValuesOfType(IEntityType entityType)
    {
        var values = entityType.GetProperties()
            .Where(p => !IsStoreGenerated(p))
            .Select(p => new AuditedValue(p.Name, p, null,
                IsRecorded: !p.IsPrimaryKey() && !Bookkeeping.Contains(p.Name),
                AuditSensitivity.Of(p.PropertyInfo ?? (MemberInfo?)p.FieldInfo,
                    p.GetTypeMapping().Converter?.ProviderClrType ?? p.ClrType)))
            .ToList();

        foreach (var complex in entityType.GetComplexProperties())
        {
            if (complex.IsCollection || complex.ComplexType.GetComplexProperties().Any())
                throw new NotSupportedException(
                    $"The audit log cannot read {entityType.ClrType.Name}.{complex.Name}. It reads only a complex property with plain values, not a collection or a nested complex property.");

            var properties = complex.ComplexType.GetProperties().ToList();
            var sensitivity = AuditSensitivity.Of(complex.PropertyInfo ?? (MemberInfo?)complex.FieldInfo, complex.ClrType);
            values.AddRange(properties.Select(p => new AuditedValue(
                properties.Count == 1 ? complex.Name : $"{complex.Name}.{p.Name}", p, complex, IsRecorded: true,
                sensitivity)));
        }

        return values;
    }

    private static HashSet<string> GuardedTablesOf(IModel model)
        => model.GetEntityTypes()
            .Where(e => !e.ClrType.IsDefined(typeof(NotAuditedAttribute)))
            .Select(e => e.GetTableName())
            .OfType<string>()
            .ToHashSet(StringComparer.Ordinal);

    private static bool IsStoreGenerated(IProperty property)
        => property.ValueGenerated is ValueGenerated.OnUpdate or ValueGenerated.OnAddOrUpdate;

    private static IForeignKey? OwnerKeyOfType(IEntityType entityType)
    {
        var ownerKeys = entityType.GetForeignKeys()
            .Where(fk => fk.DeleteBehavior == DeleteBehavior.Cascade
                         && fk.PrincipalEntityType.ClrType.IsAssignableTo(typeof(BaseEntity)))
            .ToList();
        return ownerKeys.Count <= 1
            ? ownerKeys.SingleOrDefault()
            : throw new NotSupportedException(
                $"{entityType.ClrType.Name} has more than one Cascade foreign key to an entity, so it has more than one owner. Give it one owner.");
    }
}
