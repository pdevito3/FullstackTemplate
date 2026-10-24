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
    public void value_of_a_type_with_no_json_form_is_refused()
    {
        // Act
        var act = () => AuditValues.WriteChange(AuditEntryKind.Created, [new PropertySnapshot("Photo", null, new byte[] { 1 })]);

        // Assert
        act.ShouldThrow<NotSupportedException>().Message.ShouldContain("'Photo'");
    }
}
