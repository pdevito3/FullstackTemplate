namespace FullstackTemplate.UnitTests.Domain.AuditEntries;

using FullstackTemplate.Server.Databases.Auditing;
using FullstackTemplate.Server.Domain.AuditEntries;
using FullstackTemplate.Server.Domain.AuditEntries.Dtos;
using Shouldly;

public class AuditRedactionTests
{
    private static readonly AuditSubject User = new("User", "usr_1", "User", "usr_1", "ten_1");

    private static readonly AuditVocabulary Vocabulary = new(
        new Dictionary<string, IReadOnlyDictionary<string, Sensitivity>>
        {
            ["User"] = new Dictionary<string, Sensitivity>
            {
                ["FirstName"] = Sensitivity.Sensitive,
                ["Role"] = Sensitivity.NotSensitive,
                ["Nickname"] = Sensitivity.Unclassified,
            },
        },
        new Dictionary<string, IReadOnlyDictionary<string, Sensitivity>>
        {
            ["users_exported"] = new Dictionary<string, Sensitivity>
            {
                ["Count"] = Sensitivity.NotSensitive,
                ["Reason"] = Sensitivity.Sensitive,
            },
        });

    [Test]
    public void reader_without_the_sensitive_permission_sees_only_the_values_known_as_not_sensitive()
    {
        // Arrange
        var entry = Entry(User, AuditEntryKind.Updated, action: null,
            """{"FirstName":{"old":"Ann","new":"Anne"},"Role":{"old":"User","new":"Admin"},"Nickname":{"old":"a","new":"b"},"Removed":{"old":"x","new":"y"}}""");

        // Act
        var dto = AuditRedaction.Redact(entry, Vocabulary, canReadSensitive: false);

        // Assert
        dto.Values.ShouldBe([
            new AuditValueDto { Name = "FirstName", Hidden = true },
            new AuditValueDto { Name = "Role", Old = "User", New = "Admin" },
            new AuditValueDto { Name = "Nickname", Hidden = true },
            new AuditValueDto { Name = "Removed", Hidden = true },
        ], ignoreOrder: true);
    }

    [Test]
    public void reader_with_the_sensitive_permission_sees_each_value()
    {
        // Arrange
        var entry = Entry(User, AuditEntryKind.Created, action: null, """{"FirstName":{"new":"Ann"},"Removed":{"new":"x"}}""");

        // Act
        var dto = AuditRedaction.Redact(entry, Vocabulary, canReadSensitive: true);

        // Assert
        dto.Values.ShouldBe([
            new AuditValueDto { Name = "FirstName", New = "Ann" },
            new AuditValueDto { Name = "Removed", New = "x" },
        ], ignoreOrder: true);
    }

    [Test]
    public void each_value_of_an_entity_type_that_the_code_no_longer_has_is_hidden()
    {
        // Arrange
        var memo = new AuditSubject("Memo", "memo_1", "Memo", "memo_1", "ten_1");
        var entry = Entry(memo, AuditEntryKind.Created, action: null, """{"Role":{"new":"x"}}""");

        // Act
        var dto = AuditRedaction.Redact(entry, Vocabulary, canReadSensitive: false);

        // Assert
        dto.Values.ShouldBe([new AuditValueDto { Name = "Role", Hidden = true }]);
    }

    [Test]
    public void action_shows_its_values_by_the_sensitivity_of_its_properties_and_hides_an_unknown_action()
    {
        // Arrange
        var known = Entry(User, AuditEntryKind.Action, "users_exported", """{"Count":3,"Reason":"audit"}""");
        var unknown = Entry(User, AuditEntryKind.Action, "users_imported", """{"Count":3}""");

        // Act
        var knownDto = AuditRedaction.Redact(known, Vocabulary, canReadSensitive: false);
        var unknownDto = AuditRedaction.Redact(unknown, Vocabulary, canReadSensitive: false);

        // Assert
        knownDto.Values.ShouldBe([
            new AuditValueDto { Name = "Count", New = "3" },
            new AuditValueDto { Name = "Reason", Hidden = true },
        ], ignoreOrder: true);
        unknownDto.Values.ShouldBe([new AuditValueDto { Name = "Count", Hidden = true }]);
        (knownDto.Kind, knownDto.Action).ShouldBe(("Action", "users_exported"));
    }

    [Test]
    public void entry_shows_its_save_subject_and_actor_ids_to_each_reader()
    {
        // Arrange
        var entry = Entry(User, AuditEntryKind.Deleted, action: null, "{}");

        // Act
        var dto = AuditRedaction.Redact(entry, Vocabulary, canReadSensitive: false);

        // Assert
        (dto.Kind, dto.EntityType, dto.EntityId, dto.RootType, dto.RootId, dto.TenantId)
            .ShouldBe(("Deleted", "User", "usr_1", "User", "usr_1", "ten_1"));
        (dto.ActorClient, dto.ActorUserId, dto.ActorAgentId, dto.RequestName, dto.TraceId, dto.RecordId, dto.Ordinal)
            .ShouldBe(("assistant", "usr_9", "agt_9", "DeleteUser.Command", "4bf92f3577b34da6a3ce929d0e0e4736",
                entry.RecordId, 2));
        dto.Values.ShouldBeEmpty();
    }

    [Test]
    [Arguments(false, null)]
    [Arguments(true, "ann@example.com")]
    public void subject_of_the_actor_shows_only_with_sensitive_values(bool canReadSensitive, string? subject)
    {
        // Arrange
        var entry = Entry(User, AuditEntryKind.Deleted, action: null, "{}");

        // Act
        var dto = AuditRedaction.Redact(entry, Vocabulary, canReadSensitive);

        // Assert
        dto.ActorSubject.ShouldBe(subject);
    }

    private static AuditEntry Entry(AuditSubject subject, AuditEntryKind kind, string? action, string data)
        => new(new SaveFacts(Guid.CreateVersion7(), DateTimeOffset.UnixEpoch,
                new AuditActor("ann@example.com", "assistant", "usr_9", "agt_9"), "DeleteUser.Command",
                "4bf92f3577b34da6a3ce929d0e0e4736"), ordinal: 2, subject, kind, action, data);
}
