namespace FullstackTemplate.UnitTests.Databases.Auditing;

using FullstackTemplate.Server.Databases.Auditing;
using FullstackTemplate.Server.Domain.AuditEntries;
using FullstackTemplate.Server.Services;
using Shouldly;

public class AuditDiffTests
{
    private static readonly AuditSubject User = new("User", "usr_1", "User", "usr_1", "ten_1");
    private static readonly AuditActor NoActor = new(Subject: null, ClientId: null, UserId: null, AgentId: null);

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
    public void restore_with_no_changed_value_gives_a_restored_change_with_no_values()
    {
        // Arrange
        var snapshot = Snapshot(isAdded: false, wasDeleted: true, isDeleted: false, new PropertySnapshot("FirstName", "Ann", "Ann"));

        // Act
        var change = AuditDiff.Changes([snapshot]).ShouldHaveSingleItem();

        // Assert
        change.Kind.ShouldBe(AuditEntryKind.Restored);
        change.Properties.ShouldBeEmpty();
    }

    [Test]
    public void delete_keeps_every_value_that_the_row_held()
    {
        // Arrange
        var snapshot = Snapshot(isAdded: false, wasDeleted: false, isDeleted: true,
            new PropertySnapshot("Permission", "manage_users", "manage_users"),
            new PropertySnapshot("Note", null, null));

        // Act
        var change = AuditDiff.Changes([snapshot]).ShouldHaveSingleItem();

        // Assert
        change.Kind.ShouldBe(AuditEntryKind.Deleted);
        change.Properties.Select(p => p.Path).ShouldBe(["Permission"]);
        AuditValues.Read(change.Kind, AuditValues.WriteChange(change.Kind, change.Properties))
            .ShouldHaveSingleItem().ShouldBe(new StoredValue("Permission", Old: "manage_users", New: null));
    }

    [Test]
    public void entries_of_one_save_share_its_facts_and_are_numbered_in_order()
    {
        // Arrange
        var actor = new AuditActor("sub-1", "spa", "usr_9", AgentId: null);
        var save = new SaveFacts(Guid.CreateVersion7(), DateTimeOffset.UnixEpoch, actor, "UpdateUser.Command",
            "4bf92f3577b34da6a3ce929d0e0e4736");
        var permission = new AuditSubject("UserPermission", "uperm_1", "User", "usr_1", "ten_1");
        DataChange[] changes =
        [
            new(User, AuditEntryKind.Updated, [new PropertySnapshot("FirstName", "Ann", "Anne")]),
            new(permission, AuditEntryKind.Created, [new PropertySnapshot("Permission", null, "manage_users")]),
        ];

        // Act
        var entries = AuditDiff.Compose(save, changes, [new UsersExported(User, ExportFormat.Csv, 1)]);

        // Assert
        entries.Select(e => (e.Ordinal, e.EntityId, e.Kind, e.Action)).ShouldBe([
            (0, "usr_1", AuditEntryKind.Updated, null),
            (1, "uperm_1", AuditEntryKind.Created, null),
            (2, "usr_1", AuditEntryKind.Action, "users_exported"),
        ]);
        entries.ShouldAllBe(e => e.RecordId == save.RecordId && e.OccurredAt == save.OccurredAt
                                 && e.ActorSubject == "sub-1" && e.ActorClient == "spa" && e.ActorUserId == "usr_9"
                                 && e.ActorAgentId == null
                                 && e.RequestName == "UpdateUser.Command" && e.TraceId == save.TraceId
                                 && e.TenantId == "ten_1" && e.RootId == "usr_1");
    }

    [Test]
    public void action_type_with_no_name_is_refused()
    {
        // Arrange
        var save = SaveFacts.From(DateTimeOffset.UnixEpoch, NoActor, requestName: null, traceId: null);

        // Act
        var act = () => AuditDiff.Compose(save, [], [new ActionWithNoName(User)]);

        // Assert
        act.ShouldThrow<InvalidOperationException>().Message.ShouldContain("ActionWithNoName has no [AuditActionName]");
    }

    [Test]
    [Arguments(null)]
    [Arguments("")]
    [Arguments(" ")]
    public void blank_actor_is_no_actor(string? blank)
    {
        // Act
        var actor = AuditActor.Of(blank, blank, new CallerIdentity.System());

        // Assert
        actor.ShouldBe(NoActor);
    }

    public static IEnumerable<Func<(string Case, CallerIdentity Caller, string? UserId, string? AgentId)>> Callers()
    {
        yield return () => ("user", new CallerIdentity.User("sub-1", new LiveUser("usr_1", "ten_1")), "usr_1", null);
        yield return () => ("user with no user record yet", new CallerIdentity.User("sub-1", LiveUser: null), null, null);
        yield return () => ("delegated agent",
            new CallerIdentity.DelegatedAgent("agt_1", "assistant", "sub-1", new LiveUser("usr_1", "ten_1")), "usr_1", "agt_1");
        yield return () => ("machine agent", new CallerIdentity.MachineAgent("agt_1", "sync", "ten_1"), null, "agt_1");
        yield return () => ("client on an all-tenants list", new CallerIdentity.AllTenantsClient("sync", CanManage: true),
            null, null);
        yield return () => ("system work", new CallerIdentity.System(), null, null);
    }

    [Test]
    [MethodDataSource(nameof(Callers))]
    public void actor_names_the_user_and_the_agent_of_the_caller(string @case, CallerIdentity caller, string? userId,
        string? agentId)
    {
        // Act
        var actor = AuditActor.Of("sub-1", "spa", caller);

        // Assert
        actor.ShouldBe(new AuditActor("sub-1", "spa", userId, agentId));
    }

    private static EntitySnapshot Snapshot(bool isAdded, bool wasDeleted, bool isDeleted,
        params PropertySnapshot[] properties)
        => new(User, isAdded, wasDeleted, isDeleted, properties);
}
