namespace FullstackTemplate.Server.Databases.Auditing;

using System.Buffers;
using System.Globalization;
using System.Text;
using System.Text.Json;
using Domain.AuditEntries;

/// <summary>
/// The one owner of the jsonb format of <see cref="AuditEntry.Data"/>. A change is
/// <c>{"FirstName":{"old":"Ann","new":"Anne"},"Email":{"old":"a@example.com","new":"b@example.com"}}</c>.
/// A <see cref="AuditEntryKind.Created"/> change has no "old".
/// </summary>
internal static class AuditValues
{
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
