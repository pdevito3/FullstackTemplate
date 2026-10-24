namespace FullstackTemplate.Server.Databases.Auditing;

using System.Reflection;
using Domain;
using Domain.AuditEntries;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;

/// <summary>
/// One value column of an entity type, with its audit facts. <c>IsRecorded</c> is false for the key and the
/// <see cref="BaseEntity"/> bookkeeping (the stamps and <c>IsDeleted</c>).
/// </summary>
internal sealed record AuditedValue(string Path, EntityColumn Column, bool IsRecorded, Sensitivity Sensitivity);

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

    /// <summary>The <see cref="EntityColumn"/>s of a <see cref="BaseEntity"/> type, with their audit facts.</summary>
    public IReadOnlyList<AuditedValue> ValuesOf(IEntityType entityType) => _values[entityType];

    /// <summary>
    /// The foreign key to the owner, or null for an aggregate root. A Cascade foreign key to a
    /// <see cref="BaseEntity"/> makes its principal the owner.
    /// </summary>
    public IForeignKey? OwnerKeyOf(IEntityType entityType) => _ownerKeys[entityType];

    private static List<AuditedValue> ValuesOfType(IEntityType entityType)
        => EntityColumn.Of(entityType).Select(ValueOf).ToList();

    private static AuditedValue ValueOf(EntityColumn column)
    {
        var property = column.Property;
        if (column.Complex is not { } complex)
            return new AuditedValue(property.Name, column,
                IsRecorded: !property.IsPrimaryKey() && !Bookkeeping.Contains(property.Name),
                AuditSensitivity.Of(property.PropertyInfo ?? (MemberInfo?)property.FieldInfo, property.ClrType,
                    property.IsForeignKey()));

        var path = complex.ComplexType.GetProperties().Count() == 1 ? complex.Name : $"{complex.Name}.{property.Name}";
        return new AuditedValue(path, column, IsRecorded: true,
            AuditSensitivity.Of(complex.PropertyInfo ?? (MemberInfo?)complex.FieldInfo, complex.ClrType,
                isForeignKey: false));
    }

    private static HashSet<string> GuardedTablesOf(IModel model)
        => model.GetEntityTypes()
            .Where(e => !e.ClrType.IsDefined(typeof(NotAuditedAttribute)))
            .Select(e => e.GetTableName())
            .OfType<string>()
            .ToHashSet(StringComparer.Ordinal);

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
