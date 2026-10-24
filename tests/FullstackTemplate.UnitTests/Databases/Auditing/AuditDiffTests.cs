namespace FullstackTemplate.UnitTests.Databases.Auditing;

using FullstackTemplate.Server.Databases.Auditing;
using FullstackTemplate.Server.Domain.AuditEntries;
using Shouldly;

public class AuditDiffTests
{
    private static readonly AuditSubject User = new("User", "usr_1", "User", "usr_1", "ten_1");

    [Test]
    public void added_entity_is_created_with_each_value_that_is_not_null()
    {
        // Arrange
        var snapshot = Snapshot(isAdded: true, wasDeleted: false, isDeleted: false,
            new PropertySnapshot("FirstName", null, "Ann"), new PropertySnapshot("Nickname", null, null));

        // Act
        var change = AuditDiff.Changes([snapshot]).ShouldHaveSingleItem();

        // Assert
        change.Kind.ShouldBe(AuditEntryKind.Created);
        change.Properties.Select(p => p.Path).ShouldBe(["FirstName"]);
    }

    [Test]
    public void update_holds_only_the_values_that_changed()
    {
        // Arrange
        var snapshot = Snapshot(isAdded: false, wasDeleted: false, isDeleted: false,
            new PropertySnapshot("FirstName", "Ann", "Anne"), new PropertySnapshot("LastName", "Lee", "Lee"));

        // Act
        var change = AuditDiff.Changes([snapshot]).ShouldHaveSingleItem();

        // Assert
        change.Kind.ShouldBe(AuditEntryKind.Updated);
        change.Properties.ShouldBe([new PropertySnapshot("FirstName", "Ann", "Anne")]);
    }

    [Test]
    public void update_that_changed_no_recorded_value_gives_no_change()
    {
        // Arrange
        var ownerTouch = Snapshot(isAdded: false, wasDeleted: false, isDeleted: false,
            new PropertySnapshot("FirstName", "Ann", "Ann"));

        // Act
        var changes = AuditDiff.Changes([ownerTouch]);

        // Assert
        changes.ShouldBeEmpty();
    }

    [Test]
    [Arguments(false, true, AuditEntryKind.Deleted)]
    [Arguments(true, false, AuditEntryKind.Restored)]
    public void soft_delete_flag_that_changed_gives_its_kind_also_with_no_changed_value(
        bool wasDeleted, bool isDeleted, AuditEntryKind kind)
    {
        // Arrange
        var snapshot = Snapshot(isAdded: false, wasDeleted, isDeleted, new PropertySnapshot("FirstName", "Ann", "Ann"));

        // Act
        var change = AuditDiff.Changes([snapshot]).ShouldHaveSingleItem();

        // Assert
        change.Kind.ShouldBe(kind);
        change.Properties.ShouldBeEmpty();
    }

    [Test]
    public void entries_of_one_save_share_its_facts_and_are_numbered_in_order()
    {
        // Arrange
        var save = new SaveFacts(Guid.CreateVersion7(), DateTimeOffset.UnixEpoch, "sub-1", "spa", "UpdateUser.Command",
            "4bf92f3577b34da6a3ce929d0e0e4736");
        var permission = new AuditSubject("UserPermission", "uperm_1", "User", "usr_1", "ten_1");
        DataChange[] changes =
        [
            new(User, AuditEntryKind.Updated, [new PropertySnapshot("FirstName", "Ann", "Anne")]),
            new(permission, AuditEntryKind.Created, [new PropertySnapshot("Permission", null, "manage_users")]),
        ];

        // Act
        var entries = AuditDiff.Compose(save, changes);

        // Assert
        entries.Select(e => (e.Ordinal, e.EntityId, e.RootId)).ShouldBe([(0, "usr_1", "usr_1"), (1, "uperm_1", "usr_1")]);
        entries.ShouldAllBe(e => e.RecordId == save.RecordId && e.OccurredAt == save.OccurredAt
                                 && e.ActorUser == "sub-1" && e.ActorClient == "spa"
                                 && e.RequestName == "UpdateUser.Command" && e.TraceId == save.TraceId
                                 && e.TenantId == "ten_1" && e.Action == null);
    }

    [Test]
    [Arguments(null)]
    [Arguments("")]
    [Arguments(" ")]
    public void blank_actor_is_no_actor(string? blank)
    {
        // Act
        var save = SaveFacts.From(DateTimeOffset.UnixEpoch, blank, blank, requestName: null, traceId: null);

        // Assert
        save.ActorUser.ShouldBeNull();
        save.ActorClient.ShouldBeNull();
    }

    private static EntitySnapshot Snapshot(bool isAdded, bool wasDeleted, bool isDeleted,
        params PropertySnapshot[] properties)
        => new(User, isAdded, wasDeleted, isDeleted, properties);
}
