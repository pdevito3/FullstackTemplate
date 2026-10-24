namespace FullstackTemplate.Server.Databases.Auditing;

using System.Buffers;
using System.Globalization;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Domain.AuditEntries;

/// <summary>
/// The one owner of the jsonb format of <see cref="AuditEntry.Data"/>. A change is
/// <c>{"FirstName":{"old":"Ann","new":"Anne"},"Email":{"old":"a@example.com","new":"b@example.com"}}</c>.
/// A <see cref="AuditEntryKind.Created"/> change has no "old". An action is its properties, for example
/// <c>{"Format":"Csv","Count":3}</c>.
/// </summary>
/// <summary>One stored value as display text. <c>Old</c> is null for a created value and for a property of an action.</summary>
internal readonly record struct StoredValue(string Name, string? Old, string? New);

internal static class AuditValues
{
    // An enum is stored by name, so a reorder of its members does not change the meaning of stored entries.
    private static readonly JsonSerializerOptions ActionOptions = new() { Converters = { new JsonStringEnumConverter() } };

    /// <summary>Each property of the action except its subject, by its C# name.</summary>
    public static string WriteAction(AuditAction action)
        => JsonSerializer.Serialize(action, action.GetType(), ActionOptions);

    /// <exception cref="NotSupportedException">A value has a provider type with no case here, for example
    /// <c>byte[]</c>. A new column type then fails its first save in the tests.</exception>
    public static string WriteChange(AuditEntryKind kind, IReadOnlyList<PropertySnapshot> properties)
    {
        var buffer = new ArrayBufferWriter<byte>();
        using (var writer = new Utf8JsonWriter(buffer))
        {
            writer.WriteStartObject();
            foreach (var property in properties)
            {
                writer.WriteStartObject(property.Path);
                if (kind != AuditEntryKind.Created)
                {
                    writer.WritePropertyName("old");
                    WriteValue(writer, property.Path, property.Original);
                }

                writer.WritePropertyName("new");
                WriteValue(writer, property.Path, property.Current);
                writer.WriteEndObject();
            }

            writer.WriteEndObject();
        }

        return Encoding.UTF8.GetString(buffer.WrittenSpan);
    }

    /// <summary>Each value of a stored entry as display text: a string as it is, and other JSON as its JSON text.</summary>
    public static IReadOnlyList<StoredValue> Read(AuditEntryKind kind, string data)
    {
        using var document = JsonDocument.Parse(data);
        return document.RootElement.EnumerateObject()
            .Select(property => kind == AuditEntryKind.Action
                ? new StoredValue(property.Name, Old: null, Display(property.Value))
                : new StoredValue(property.Name,
                    property.Value.TryGetProperty("old", out var old) ? Display(old) : null,
                    Display(property.Value.GetProperty("new"))))
            .ToList();
    }

    private static string? Display(JsonElement value) => value.ValueKind switch
    {
        JsonValueKind.Null => null,
        JsonValueKind.String => value.GetString(),
        _ => value.GetRawText(),
    };

    private static void WriteValue(Utf8JsonWriter writer, string path, object? value)
    {
        switch (value)
        {
            case null: writer.WriteNullValue(); break;
            case string text: writer.WriteStringValue(text); break;
            case bool flag: writer.WriteBooleanValue(flag); break;
            case int number: writer.WriteNumberValue(number); break;
            case long number: writer.WriteNumberValue(number); break;
            case short number: writer.WriteNumberValue(number); break;
            case uint number: writer.WriteNumberValue(number); break;
            case decimal number: writer.WriteNumberValue(number); break;
            case double number: writer.WriteNumberValue(number); break;
            case Guid guid: writer.WriteStringValue(guid); break;
            case DateTimeOffset moment: writer.WriteStringValue(moment); break;
            case DateTime moment: writer.WriteStringValue(moment); break;
            case DateOnly date: writer.WriteStringValue(date.ToString("O", CultureInfo.InvariantCulture)); break;
            default:
                throw new NotSupportedException(
                    $"The audit log cannot store '{path}', because it has no JSON form for {value.GetType().Name}. Add a case to AuditValues.");
        }
    }
}
