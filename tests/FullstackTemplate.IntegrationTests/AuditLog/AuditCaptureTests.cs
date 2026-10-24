namespace FullstackTemplate.IntegrationTests.AuditLog;

using System.Diagnostics;
using System.Text.Json;
using FullstackTemplate.IntegrationTests.DomainEvents;
using FullstackTemplate.Server.Databases;
using FullstackTemplate.Server.Domain.AuditEntries;
using FullstackTemplate.Server.Domain.Tenants;
using FullstackTemplate.Server.Domain.Tenants.DomainEvents;
using FullstackTemplate.Server.Domain.Tenants.Dtos;
using FullstackTemplate.Server.Domain.Tenants.Features;
using FullstackTemplate.Server.Domain.Users;
using FullstackTemplate.Server.Domain.Users.Dtos;
using FullstackTemplate.Server.Domain.Users.Features;
using FullstackTemplate.Server.Services;
using FullstackTemplate.SharedTestHelpers.EntityGraphs;
using FullstackTemplate.SharedTestHelpers.Fakes.Tenant;
using FullstackTemplate.SharedTestHelpers.Fakes.User;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;
using Shouldly;
using static FullstackTemplate.IntegrationTests.DomainEvents.DomainEventTestServices;

public class AuditCaptureTests : TestBase
{
    [Test]
    public async Task added_user_has_a_created_entry_and_its_permissions_have_entries_in_the_history_of_the_user()
    {
        // Arrange
        var scope = new TestingServiceScope();
        var dto = new FakeUserForCreationDto().Generate() with { Role = UserRole.Admin().Value };

        // Act
        var user = await scope.SendAsync(new AddUser.Command(dto));

        // Assert
        var entries = await EntriesOfRootAsync(user.Id);
        var created = entries.Single(e => e.EntityId == user.Id);
        created.Kind.ShouldBe(AuditEntryKind.Created);
        (created.EntityType, created.RootType, created.TenantId).ShouldBe(("User", "User", TestFixture.DefaultTenant.Id));
        ChangeOf(created, "FirstName").ShouldBe(new Change(HasOld: false, Old: null, New: dto.FirstName));
        ChangeOf(created, "Email").ShouldBe(new Change(HasOld: false, Old: null, New: dto.Email));
        Paths(created).ShouldNotContain(nameof(User.Id));
        Paths(created).ShouldNotContain(nameof(User.CreatedOn));
        entries.Where(e => e.EntityType == nameof(UserPermission))
            .Select(e => ChangeOf(e, "Permission").New)
            .ShouldBe(Permission.GetAll().Select(p => p.Value), ignoreOrder: true);
        entries.ShouldAllBe(e => e.RecordId == created.RecordId && e.Kind == AuditEntryKind.Created
                                 && e.ActorUser == TestFixture.DefaultUser.Identifier
                                 && e.ActorClient == TestFixture.FirstPartyClientId
                                 && e.RequestName == "AddUser.Command");
    }

    [Test]
    public async Task update_records_the_old_and_new_value_of_each_changed_value_and_the_trace()
    {
        // Arrange
        var scope = new TestingServiceScope();
        await scope.StartGraph(out _).AddUser(out var user).BuildAsync();
        var newEmail = $"{Guid.NewGuid():N}@example.com";
        using var trace = new Activity("audit-test").Start();

        // Act
        await scope.SendAsync(new UpdateUser.Command(user.Id, new UserForUpdateDto
        {
            FirstName = $"{user.FirstName}x",
            LastName = user.LastName,
            Email = newEmail,
            Username = user.Username,
            Version = user.Version,
        }));

        // Assert
        var updated = (await EntriesOfRootAsync(user.Id)).Single(e => e.Kind == AuditEntryKind.Updated);
        Paths(updated).ShouldBe(["FirstName", "Email"], ignoreOrder: true);
        ChangeOf(updated, "FirstName").ShouldBe(new Change(HasOld: true, Old: user.FirstName, New: $"{user.FirstName}x"));
        ChangeOf(updated, "Email").ShouldBe(new Change(HasOld: true, Old: user.Email.Value, New: newEmail));
        updated.RequestName.ShouldBe("UpdateUser.Command");
        updated.TraceId.ShouldBe(trace.TraceId.ToHexString());
    }

    [Test]
    public async Task delete_records_a_deleted_entry_for_the_user_and_each_of_its_permissions()
    {
        // Arrange
        var scope = new TestingServiceScope();
        await scope.StartGraph(out _).AddUser(out var user, u => u.Role = UserRole.Admin().Value).BuildAsync();

        // Act
        await scope.SendAsync(new DeleteUser.Command(user.Id));

        // Assert
        var deleted = (await EntriesOfRootAsync(user.Id)).Where(e => e.Kind == AuditEntryKind.Deleted).ToList();
        deleted.Select(e => e.EntityId)
            .ShouldBe(user.UserPermissions.Select(p => p.Id).Append(user.Id), ignoreOrder: true);
        deleted.Select(e => e.RecordId).Distinct().ShouldHaveSingleItem();
        deleted.Single(e => e.EntityId == user.Id).Data.ShouldContain(user.Identifier);
    }

    [Test]
    public async Task permission_removed_from_the_user_is_deleted_in_its_history_and_the_owner_touch_records_nothing()
    {
        // Arrange
        var scope = new TestingServiceScope();
        await scope.StartGraph(out _).AddUser(out var user, u => u.Role = UserRole.Admin().Value).BuildAsync();
        var db = scope.GetService<AppDbContext>();
        var loaded = await db.Users.Include(u => u.UserPermissions).SingleAsync(u => u.Id == user.Id);
        var removed = loaded.UserPermissions.First();

        // Act
        loaded.RemovePermission(removed.Permission);
        await db.SaveChangesAsync();

        // Assert
        var entries = await EntriesOfRootAsync(user.Id);
        var deletion = entries.Single(e => e.Kind == AuditEntryKind.Deleted);
        (deletion.EntityType, deletion.EntityId, deletion.RootType, deletion.TenantId)
            .ShouldBe((nameof(UserPermission), removed.Id, nameof(User), user.TenantId));
        entries.Where(e => e.RecordId == deletion.RecordId).ShouldHaveSingleItem();
        deletion.Data.ShouldContain($"\"{removed.Permission.Value}\"");
    }

    [Test]
    public async Task change_of_a_domain_event_handler_is_in_the_record_of_the_save_under_its_own_tenant()
    {
        // Arrange
        var addedIdentifier = $"added-{Guid.NewGuid()}";
        await using var services = TestFixture.BuildServiceProvider(s => s.AddTestHandler<TenantUpdated>((e, db, _) =>
        {
            db.Users.Add(NewUser(e.Id, addedIdentifier));
            return Task.CompletedTask;
        }));
        var scope = new TestingServiceScope(services);
        var scenario = await scope.StartTenantScenarioAsync();

        // Act
        await scope.SendAsync(new UpdateTenant.Command(scenario.Tenant.Id,
            new TenantForUpdateDto { Name = $"renamed-{Guid.NewGuid()}", Version = scenario.Tenant.Version }));

        // Assert
        var tenantUpdate = (await EntriesOfRootAsync(scenario.Tenant.Id)).Single(e => e.Kind == AuditEntryKind.Updated);
        var addedUserId = await ReadAsync(read => read.Users.Where(u => u.Identifier == addedIdentifier).Select(u => u.Id).SingleAsync());
        var userCreation = (await EntriesOfRootAsync(addedUserId)).Single(e => e.EntityId == addedUserId);
        userCreation.RecordId.ShouldBe(tenantUpdate.RecordId);
        userCreation.TenantId.ShouldBe(scenario.Tenant.Id);
        userCreation.RequestName.ShouldBe("UpdateTenant.Command");
    }

    [Test]
    public async Task failed_save_records_nothing()
    {
        // Arrange
        var scope = new TestingServiceScope();
        await scope.StartGraph(out _).AddUser(out var existing).BuildAsync();
        var db = scope.GetService<AppDbContext>();
        var duplicate = NewUser(TestFixture.DefaultTenant.Id, existing.Identifier);
        db.Users.Add(duplicate);

        // Act
        var act = () => db.SaveChangesAsync();

        // Assert
        await act.ShouldThrowAsync<ConstraintConflictException>();
        (await EntriesOfRootAsync(duplicate.Id)).ShouldBeEmpty();
    }

    [Test]
    public async Task retried_save_records_its_changes_once()
    {
        // Arrange
        var failFirstWrite = new FailFirstWrite();
        await using var services = TestFixture.BuildServiceProvider(s => s
            .ConfigureDbContext<AppDbContext>((_, options) => options.AddInterceptors(failFirstWrite)));
        var scope = new TestingServiceScope(services);
        var scenario = await scope.StartTenantScenarioAsync();
        failFirstWrite.Arm();

        // Act
        await scope.SendAsync(new UpdateTenant.Command(scenario.Tenant.Id,
            new TenantForUpdateDto { Name = $"renamed-{Guid.NewGuid()}", Version = scenario.Tenant.Version }));

        // Assert
        failFirstWrite.Failures.ShouldBe(1);
        (await EntriesOfRootAsync(scenario.Tenant.Id)).Count(e => e.Kind == AuditEntryKind.Updated).ShouldBe(1);
    }

    [Test]
    public async Task system_work_records_no_actor_and_no_request()
    {
        // Arrange
        var tenant = Tenant.Create(new FakeTenantForCreation().Generate());
        await using var system = TestFixture.BaseScopeFactory.CreateSystemScope();
        var db = system.ServiceProvider.GetRequiredService<AppDbContext>();

        // Act
        db.Tenants.Add(tenant);
        await db.SaveChangesAsync();

        // Assert
        var created = (await EntriesOfRootAsync(tenant.Id)).ShouldHaveSingleItem();
        (created.ActorUser, created.ActorClient, created.RequestName).ShouldBe((null, null, null));
    }

    [Test]
    public async Task save_after_a_request_of_the_same_scope_does_not_take_the_name_of_that_request()
    {
        // Arrange
        var scope = new TestingServiceScope();
        await scope.StartGraph(out _).AddTenant(out var tenant).AddUser(out var admin, u => u.Role = UserRole.Admin().Value)
            .BuildAsync();
        scope.SetUserWithDefaults(identifier: admin.Identifier);
        await scope.SendAsync(new UpdateTenant.Command(tenant.Id,
            new TenantForUpdateDto { Name = $"renamed-{Guid.NewGuid()}", Version = tenant.Version }));
        var db = scope.GetService<AppDbContext>();
        var user = await db.Users.SingleAsync(u => u.Id == admin.Id);

        // Act
        user.UpdateFromIdp($"{user.FirstName}x", user.LastName, user.Email, user.Username);
        await db.SaveChangesAsync();

        // Assert
        (await EntriesOfRootAsync(tenant.Id)).Single(e => e.Kind == AuditEntryKind.Updated)
            .RequestName.ShouldBe("UpdateTenant.Command");
        (await EntriesOfRootAsync(admin.Id)).Single(e => e.Kind == AuditEntryKind.Updated)
            .RequestName.ShouldBeNull();
    }

    [Test]
    [Arguments("UPDATE audit_entries SET kind = kind WHERE root_id = @rootId")]
    [Arguments("DELETE FROM audit_entries WHERE root_id = @rootId")]
    [Arguments("TRUNCATE audit_entries")]
    public async Task audit_entries_refuse_a_change_of_a_written_row(string sql)
    {
        // Arrange
        var tenant = Tenant.Create(new FakeTenantForCreation().Generate());
        await using (var system = TestFixture.BaseScopeFactory.CreateSystemScope())
        {
            var db = system.ServiceProvider.GetRequiredService<AppDbContext>();
            db.Tenants.Add(tenant);
            await db.SaveChangesAsync();
        }

        await using var connection = new NpgsqlConnection(
            new TestingServiceScope().GetService<IConfiguration>().GetConnectionString(DatabaseConsts.DatabaseName));
        await connection.OpenAsync();
        // The rollback undoes the statement if the table ever lets it through.
        await using var transaction = await connection.BeginTransactionAsync();
        await using var command = new NpgsqlCommand(sql, connection, transaction);
        command.Parameters.AddWithValue("rootId", tenant.Id);

        // Act
        var act = () => command.ExecuteNonQueryAsync();

        // Assert
        (await act.ShouldThrowAsync<PostgresException>()).MessageText.ShouldContain("append-only");
    }

    private static Task<List<AuditEntry>> EntriesOfRootAsync(string rootId)
        => ReadAsync(read => read.AuditEntries
            .Where(e => e.RootId == rootId)
            .OrderBy(e => e.OccurredAt).ThenBy(e => e.Ordinal)
            .ToListAsync());

    private static List<string> Paths(AuditEntry entry)
        => JsonDocument.Parse(entry.Data).RootElement.EnumerateObject().Select(p => p.Name).ToList();

    private static Change ChangeOf(AuditEntry entry, string path)
    {
        var change = JsonDocument.Parse(entry.Data).RootElement.GetProperty(path);
        var hasOld = change.TryGetProperty("old", out var old);
        return new Change(hasOld, hasOld ? old.GetString() : null, change.GetProperty("new").GetString());
    }

    private sealed record Change(bool HasOld, string? Old, string? New);
}
