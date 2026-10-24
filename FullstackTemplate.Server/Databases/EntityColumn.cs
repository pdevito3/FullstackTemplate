namespace FullstackTemplate.Server.Databases;

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Metadata;

/// <summary>
/// A column that a write of an entity type sends: a property of the type, or a property of a complex property of the
/// type. The database makes the value of a store-generated column (<c>xmin</c>), so that column is not one of them.
/// The audit snapshot and the COPY of <see cref="AppDbContext.BulkInsertAsync{TRoot}"/> read the same columns.
/// </summary>
internal sealed record EntityColumn(IProperty Property, IComplexProperty? Complex)
{
    /// <exception cref="NotSupportedException">A complex property is a collection or holds a complex property.</exception>
    public static IReadOnlyList<EntityColumn> Of(IEntityType entityType)
    {
        var columns = entityType.GetProperties()
            .Where(p => p.ValueGenerated is not (ValueGenerated.OnUpdate or ValueGenerated.OnAddOrUpdate))
            .Select(p => new EntityColumn(p, null))
            .ToList();

        foreach (var complex in entityType.GetComplexProperties())
        {
            if (complex.IsCollection || complex.ComplexType.GetComplexProperties().Any())
                throw new NotSupportedException(
                    $"AppDbContext cannot read {entityType.ClrType.Name}.{complex.Name} for the audit log and the bulk insert. It reads only a complex property with plain values, not a collection or a nested complex property.");

            columns.AddRange(complex.ComplexType.GetProperties().Select(p => new EntityColumn(p, complex)));
        }

        return columns;
    }

    /// <summary>The value as the column holds it, after the value converter of the property.</summary>
    public object? ProviderValueOf(EntityEntry entry, bool original)
    {
        var property = Complex is null ? entry.Property(Property) : entry.ComplexProperty(Complex).Property(Property);
        var value = original ? property.OriginalValue : property.CurrentValue;
        return value is null || Property.GetTypeMapping().Converter is not { } converter
            ? value
            : converter.ConvertToProvider(value);
    }
}
