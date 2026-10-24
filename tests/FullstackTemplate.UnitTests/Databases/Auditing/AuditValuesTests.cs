namespace FullstackTemplate.UnitTests.Databases.Auditing;

using System.Text.Json;
using FullstackTemplate.Server.Databases;
using FullstackTemplate.Server.Domain;
using FullstackTemplate.Server.Databases.Auditing;
using FullstackTemplate.Server.Domain.AuditEntries;
using Microsoft.EntityFrameworkCore;
using Shouldly;

public class AuditValuesTests
{
    [Test]
    public void created_value_has_no_old_value()
    {
        // Act
        var json = AuditValues.WriteChange(AuditEntryKind.Created, [new PropertySnapshot("FirstName", null, "Ann")]);

        // Assert
        JsonDocument.Parse(json).RootElement.GetProperty("FirstName").GetRawText().ShouldBe("""{"new":"Ann"}""");
    }

    [Test]
    public void updated_value_has_its_old_and_new_value()
    {
        // Act
        var json = AuditValues.WriteChange(AuditEntryKind.Updated, [new PropertySnapshot("IsActive", true, false)]);

        // Assert
        JsonDocument.Parse(json).RootElement.GetProperty("IsActive").GetRawText().ShouldBe("""{"old":true,"new":false}""");
    }

    [Test]
    public void action_holds_each_property_but_its_subject_and_names_its_enum_values()
    {
        // Arrange
        var tenant = new AuditSubject("Tenant", "ten_1", "Tenant", "ten_1", "ten_1");

        // Act
        var json = AuditValues.WriteAction(new UsersExported(tenant, ExportFormat.Json, 3));

        // Assert
        json.ShouldBe("""{"Format":"Json","Count":3,"Reason":null}""");
    }

    [Test]
    public void stored_change_reads_back_as_display_text()
    {
        // Arrange
        var json = AuditValues.WriteChange(AuditEntryKind.Updated, [
            new PropertySnapshot("FirstName", "Ann", "Anne"),
            new PropertySnapshot("Count", 2, 3),
            new PropertySnapshot("Nickname", null, "Annie"),
        ]);

        // Act
        var values = AuditValues.Read(AuditEntryKind.Updated, json);

        // Assert
        values.ShouldBe([
            new StoredValue("FirstName", "Ann", "Anne"),
            new StoredValue("Count", "2", "3"),
            new StoredValue("Nickname", null, "Annie"),
        ]);
    }

    [Test]
    public void stored_action_reads_back_as_its_properties()
    {
        // Arrange
        var tenant = new AuditSubject("Tenant", "ten_1", "Tenant", "ten_1", "ten_1");
        var json = AuditValues.WriteAction(new UsersExported(tenant, ExportFormat.Csv, 3));

        // Act
        var values = AuditValues.Read(AuditEntryKind.Action, json);

        // Assert
        values.ShouldBe([new StoredValue("Format", null, "Csv"), new StoredValue("Count", null, "3"),
            new StoredValue("Reason", null, null)]);
    }

    [Test]
    public void value_of_each_type_that_needs_no_mark_is_stored()
    {
        // Arrange
        var values = AuditSensitivity.PlainTypes.Select(Activator.CreateInstance).Append(ExportFormat.Csv);

        // Act
        var json = AuditValues.WriteChange(AuditEntryKind.Created,
            values.Select((value, index) => new PropertySnapshot($"Value{index}", null, value)).ToList());

        // Assert
        AuditValues.Read(AuditEntryKind.Created, json)[^1].New.ShouldBe("Csv");
    }

    [Test]
    [Arguments('A', "A")]
    [Arguments(1.5f, "1.5")]
    [Arguments(2.25, "2.25")]
    public void value_of_a_type_that_needs_a_mark_is_stored_as_display_text(object value, string display)
    {
        // Act
        var json = AuditValues.WriteChange(AuditEntryKind.Created, [new PropertySnapshot("Value", null, value)]);

        // Assert
        AuditValues.Read(AuditEntryKind.Created, json).Single().New.ShouldBe(display);
    }

    [Test]
    public void value_of_a_date_or_a_time_is_stored_as_invariant_text()
    {
        // Arrange
        PropertySnapshot[] values =
        [
            new("Date", null, new DateOnly(2026, 10, 24)),
            new("Time", null, new TimeOnly(13, 8, 1, 500)),
            new("Span", null, new TimeSpan(1, 2, 3, 4)),
            new("Amount", null, 12.50m),
            new("Moment", null, new DateTimeOffset(2026, 10, 24, 13, 8, 1, TimeSpan.Zero)),
        ];

        // Act
        var json = AuditValues.WriteChange(AuditEntryKind.Created, values);

        // Assert
        AuditValues.Read(AuditEntryKind.Created, json).Select(v => v.New).ShouldBe([
            "2026-10-24", "13:08:01.5000000", "1.02:03:04", "12.50", "2026-10-24T13:08:01+00:00",
        ]);
    }

    [Test]
    public void value_of_each_recorded_column_of_the_model_is_stored()
    {
        // Arrange
        using var context = new AppDbContextFactory().CreateDbContext([]);
        var audit = AuditModel.Of(context.Model);
        var values = context.Model.GetEntityTypes()
            .Where(entityType => entityType.ClrType.IsAssignableTo(typeof(BaseEntity)))
            .SelectMany(audit.ValuesOf)
            .Where(value => value.IsRecorded)
            .Select(value => new PropertySnapshot(value.Path, null, SampleOf(
                value.Column.Property.GetTypeMapping().Converter?.ProviderClrType ?? value.Column.Property.ClrType)))
            .ToList();

        // Act
        var act = () => AuditValues.WriteChange(AuditEntryKind.Created, values);

        // Assert
        values.ShouldNotBeEmpty();
        act.ShouldNotThrow();
    }

    [Test]
    public void value_of_a_type_with_no_json_form_is_refused()
    {
        // Act
        var act = () => AuditValues.WriteChange(AuditEntryKind.Created, [new PropertySnapshot("Photo", null, new byte[] { 1 })]);

        // Assert
        act.ShouldThrow<NotSupportedException>().Message.ShouldContain("'Photo'");
    }

    private static object SampleOf(Type providerType)
    {
        var type = Nullable.GetUnderlyingType(providerType) ?? providerType;
        return type == typeof(string) ? "x" : Activator.CreateInstance(type)!;
    }
}
