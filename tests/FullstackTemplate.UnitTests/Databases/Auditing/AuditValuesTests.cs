namespace FullstackTemplate.UnitTests.Databases.Auditing;

using System.Text.Json;
using FullstackTemplate.Server.Databases.Auditing;
using FullstackTemplate.Server.Domain.AuditEntries;
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
    public void value_of_a_type_with_no_json_form_is_refused()
    {
        // Act
        var act = () => AuditValues.WriteChange(AuditEntryKind.Created, [new PropertySnapshot("Photo", null, new byte[] { 1 })]);

        // Assert
        act.ShouldThrow<NotSupportedException>().Message.ShouldContain("'Photo'");
    }
}
