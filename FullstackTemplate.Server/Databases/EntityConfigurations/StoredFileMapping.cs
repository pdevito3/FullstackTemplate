namespace FullstackTemplate.Server.Databases.EntityConfigurations;

using System.Reflection;
using Domain.Attachments;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

/// <summary>A column that holds the <see cref="StoredFile.StorageKey"/> of a row.</summary>
/// <param name="EntityType">The entity type of the rows.</param>
/// <param name="PathToKey">The members from a row to its key, such as <c>File</c> and then <c>StorageKey</c>.</param>
/// <param name="StorageKey">The property of the key.</param>
public sealed record StorageKeyColumn(IEntityType EntityType, IReadOnlyList<MemberInfo> PathToKey, IProperty StorageKey);

public static class StoredFileMapping
{
    /// <summary>
    /// Maps a <see cref="StoredFile"/> to the columns <c>file_name</c>, <c>media_type</c>, <c>size_bytes</c>, and
    /// <c>storage_key</c>: <c>builder.ComplexProperty(e => e.File, file => file.MapStoredFile());</c>
    /// </summary>
    public static void MapStoredFile(this ComplexPropertyBuilder<StoredFile> file)
    {
        file.Property(f => f.Name)
            .HasColumnName("file_name")
            .HasMaxLength(StoredFile.NameMaxLength)
            .IsRequired();
        file.Property(f => f.MediaType)
            .HasColumnName("media_type")
            .HasMaxLength(StoredFile.MediaTypeMaxLength)
            .IsRequired();
        file.Property(f => f.SizeBytes)
            .HasColumnName("size_bytes");
        file.Property(f => f.StorageKey)
            .HasColumnName("storage_key")
            .HasMaxLength(StoredFile.StorageKeyMaxLength)
            .IsRequired();
    }

    /// <summary>
    /// Each column of the model that holds the key of a <see cref="StoredFile"/>: on each entity type, also a derived
    /// one, and also inside another value object.
    /// </summary>
    /// <exception cref="NotSupportedException">A collection holds stored files, or one column holds a stored file.</exception>
    public static IReadOnlyList<StorageKeyColumn> StorageKeyColumns(IModel model)
    {
        foreach (var entityType in model.GetEntityTypes())
            GuardNoStoredFileInOneColumn(entityType, entityType.GetDeclaredProperties());
        return [.. model.GetEntityTypes().SelectMany(e => ColumnsIn(e, e.GetDeclaredComplexProperties(), []))];
    }

    private static List<StorageKeyColumn> ColumnsIn(IEntityType entityType, IEnumerable<IComplexProperty> properties,
        MemberInfo[] path)
    {
        var columns = new List<StorageKeyColumn>();
        foreach (var property in properties)
        {
            MemberInfo[] pathToProperty = [.. path, MemberOf(property)];
            var found = columns.Count;
            if (property.ComplexType.ClrType == typeof(StoredFile))
                columns.Add(ColumnOf(entityType, property.ComplexType, pathToProperty));
            else
            {
                GuardNoStoredFileInOneColumn(entityType, property.ComplexType.GetProperties());
                columns.AddRange(ColumnsIn(entityType, property.ComplexType.GetComplexProperties(), pathToProperty));
            }

            if (property.IsCollection && columns.Count > found)
                throw new NotSupportedException(
                    $"{entityType.DisplayName()}.{property.Name} is a collection that holds stored files, and the sweep of the bucket cannot read their keys. Give each file a row of its own.");
        }

        return columns;
    }

    private static void GuardNoStoredFileInOneColumn(IEntityType entityType, IEnumerable<IProperty> properties)
    {
        if (properties.FirstOrDefault(p => p.ClrType == typeof(StoredFile) || p.ClrType.IsAssignableTo(typeof(IEnumerable<StoredFile>)))
            is { } property)
            throw new NotSupportedException(
                $"{entityType.DisplayName()}.{property.Name} keeps stored files in one column, and the sweep of the bucket cannot read their keys. Map each file with ComplexProperty and MapStoredFile.");
    }

    private static StorageKeyColumn ColumnOf(IEntityType entityType, IComplexType file, MemberInfo[] pathToFile)
    {
        // SAFETY: The complex type of a StoredFile maps its StorageKey.
        var storageKey = file.FindProperty(nameof(StoredFile.StorageKey))!;
        return new StorageKeyColumn(entityType, [.. pathToFile, MemberOf(storageKey)], storageKey);
    }

    // SAFETY: A property that EF maps on a CLR type has a CLR property or a field.
    private static MemberInfo MemberOf(IReadOnlyPropertyBase property) => (MemberInfo?)property.PropertyInfo ?? property.FieldInfo!;
}
