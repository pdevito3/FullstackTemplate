namespace FullstackTemplate.IntegrationTests.Databases;

using System.Data.Common;
using System.Text.Json;
using FullstackTemplate.IntegrationTests.DomainEvents;
using FullstackTemplate.Server.Databases;
using FullstackTemplate.Server.Domain.AuditEntries;
using FullstackTemplate.Server.Domain.Tenants;
using FullstackTemplate.Server.Domain.Users;
using FullstackTemplate.Server.Domain.Users.DomainEvents;
using FullstackTemplate.Server.Services;
using FullstackTemplate.SharedTestHelpers.Fakes.Tenant;
using FullstackTemplate.SharedTestHelpers.Fakes.User;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;
using Shouldly;
using static FullstackTemplate.IntegrationTests.DomainEvents.DomainEventTestServices;

public class BulkInsertTests : TestBase
{
    [Test]
    public async Task bulk_insert_writes_each_user_with_its_permissions_and_a_created_entry_for_each_row()
    {
        // Arrange
        var scope = new TestingServiceScope();
        var users = Enumerable.Range(0, 3).Select(_ => NewAdmin()).ToList();
        var allPermissions = Permission.GetAll().Select(p => p.Value).ToList();

        // Act
        var written = await scope.GetService<AppDbContext>().BulkInsertAsync(users);

        // Assert
        written.ShouldBe(users.Count * (1 + allPermissions.Count));
        var ids = users.Select(u => u.Id).ToList();
        var stored = await ReadAsync(read => read.Users.Include(u => u.UserPermissions).Where(u => ids.Contains(u.Id)).ToListAsync());
        var entries = await ReadAsync(read => read.AuditEntries.Where(e => ids.Contains(e.RootId)).ToListAsync());
        var record = entries.Select(e => (e.RecordId, e.OccurredAt)).Distinct().ShouldHaveSingleItem();
        foreach (var user in users)
        {
            var row = stored.Single(u => u.Id == user.Id);
            (row.FirstName, row.Email.Value, row.Role.Value).ShouldBe((user.FirstName, user.Email.Value, user.Role.Value));
            (row.CreatedOn, row.CreatedBy, row.CreatedByClient)
                .ShouldBe((record.OccurredAt, TestFixture.DefaultUser.Identifier, TestFixture.FirstPartyClientId));
            row.UserPermissions.Select(p => p.Permission.Value).ShouldBe(allPermissions, ignoreOrder: true);

            NewValueOf(entries.Single(e => e.EntityId == user.Id), "Email").ShouldBe(user.Email.Value);
            entries.Where(e => e.RootId == user.Id && e.EntityType == nameof(UserPermission)).Select(e => e.EntityId)
                .ShouldBe(user.UserPermissions.Select(p => p.Id), ignoreOrder: true);
        }

        entries.ShouldAllBe(e => e.Kind == AuditEntryKind.Created && e.ActorSubject == TestFixture.DefaultUser.Identifier
                                 && e.ActorUserId == TestFixture.DefaultUser.Id);
        users.ShouldAllBe(u => u.DomainEvents.Count == 0);
    }

    [Test]
    public async Task bulk_inserted_user_is_tracked_with_its_version_so_a_later_save_changes_it()
    {
        // Arrange
        var db = new TestingServiceScope().GetService<AppDbContext>();
        var user = NewAdmin();
        await db.BulkInsertAsync([user]);

        // Act
        user.UpdateFromIdp("Renamed", user.LastName, user.Email.Value, user.Username);
        await db.SaveChangesAsync();

        // Assert
        var stored = await ReadAsync(read => read.Users.SingleAsync(u => u.Id == user.Id));
        stored.FirstName.ShouldBe("Renamed");
        stored.Version.ShouldBe(user.Version);
    }

    [Test]
    public async Task bulk_inserted_user_ends_a_cached_miss_of_the_tenant_lookup()
    {
        // Arrange
        var scope = new TestingServiceScope();
        var lookup = scope.GetService<ITenantIdProvider>();
        var user = NewAdmin();
        (await lookup.FindLiveUserAsync(user.Identifier)).ShouldBeNull();

        // Act
        await scope.GetService<AppDbContext>().BulkInsertAsync([user]);

        // Assert
        (await lookup.FindLiveUserAsync(user.Identifier)).ShouldBe(new LiveUser(user.Id, user.TenantId));
    }

    [Test]
    [Arguments(false)]
    [Arguments(true)]
    public async Task bulk_insert_that_loses_the_connection_at_the_commit_writes_each_row_once(bool afterCommit)
    {
        // Arrange
        var failCommit = new FailFirstCommit(afterCommit);
        await using var services = TestFixture.BuildServiceProvider(s => s
            .ConfigureDbContext<AppDbContext>((_, options) => options.AddInterceptors(failCommit)));
        var db = new TestingServiceScope(services).GetService<AppDbContext>();
        var user = NewAdmin();
        failCommit.Arm();

        // Act
        var written = await db.BulkInsertAsync([user]);

        // Assert
        failCommit.Failures.ShouldBe(1);
        (await ReadAsync(read => read.Users.CountAsync(u => u.Id == user.Id))).ShouldBe(1);
        (await ReadAsync(read => read.AuditEntries.CountAsync(e => e.RootId == user.Id))).ShouldBe(written);
    }

    [Test]
    public async Task bulk_insert_of_a_duplicate_is_a_conflict_that_writes_nothing_and_ends_the_context()
    {
        // Arrange
        var takenName = $"Tenant {Guid.NewGuid():N}";
        await new TestingServiceScope().SetupTenantAsync(takenName);
        var db = new TestingServiceScope().GetService<AppDbContext>();
        Tenant[] tenants = [NewTenant($"Tenant {Guid.NewGuid():N}"), NewTenant(takenName)];
        var ids = tenants.Select(t => t.Id).ToList();

        // Act
        var act = () => db.BulkInsertAsync(tenants);

        // Assert
        (await act.ShouldThrowAsync<ConstraintConflictException>()).Property.ShouldBe("name");
        (await ReadAsync(read => read.Tenants.AnyAsync(t => ids.Contains(t.Id)))).ShouldBeFalse();
        (await ReadAsync(read => read.AuditEntries.AnyAsync(e => ids.Contains(e.RootId)))).ShouldBeFalse();
        (await Should.ThrowAsync<InvalidOperationException>(() => db.SaveChangesAsync())).Message
            .ShouldStartWith("An earlier save on this AppDbContext failed");
    }

    [Test]
    public async Task bulk_insert_refuses_pending_changes_and_leaves_them_to_the_save()
    {
        // Arrange
        var db = new TestingServiceScope().GetService<AppDbContext>();
        var pending = NewAdmin();
        db.Users.Add(pending);
        var user = NewAdmin();

        // Act
        var act = () => db.BulkInsertAsync([user]);

        // Assert
        (await act.ShouldThrowAsync<InvalidOperationException>()).Message.ShouldContain("has pending changes");
        await db.SaveChangesAsync();
        (await ReadAsync(read => read.Users.Where(u => u.Id == pending.Id || u.Id == user.Id).Select(u => u.Id).ToListAsync()))
            .ShouldBe([pending.Id]);
    }

    [Test]
    public async Task bulk_insert_refuses_a_part_of_an_aggregate()
    {
        // Arrange
        var db = new TestingServiceScope().GetService<AppDbContext>();
        var permission = NewAdmin().UserPermissions.First();

        // Act
        var act = () => db.BulkInsertAsync([permission]);

        // Assert
        (await act.ShouldThrowAsync<InvalidOperationException>()).Message
            .ShouldStartWith("BulkInsertAsync inserts aggregate roots, and a UserPermission is a part of its User.");
    }

    [Test]
    public async Task bulk_insert_refuses_a_domain_event_that_has_a_handler_and_writes_nothing()
    {
        // Arrange
        await using var services = TestFixture.BuildServiceProvider(s => s
            .AddTestHandler<UserCreated>((_, _, _) => Task.CompletedTask));
        var db = new TestingServiceScope(services).GetService<AppDbContext>();
        var user = NewAdmin();

        // Act
        var act = () => db.BulkInsertAsync([user]);

        // Assert
        (await act.ShouldThrowAsync<InvalidOperationException>()).Message
            .ShouldStartWith("BulkInsertAsync runs no domain event handlers, but UserCreated has a handler.");
        (await ReadAsync(read => read.Users.AnyAsync(u => u.Id == user.Id))).ShouldBeFalse();
    }

    private static User NewAdmin() => User.Create(new FakeUserForCreation()
        .RuleFor(u => u.TenantId, _ => TestFixture.DefaultTenant.Id)
        .RuleFor(u => u.Identifier, _ => $"bulk-{Guid.NewGuid()}")
        .RuleFor(u => u.Role, _ => UserRole.Admin().Value)
        .Generate());

    private static Tenant NewTenant(string name)
        => Tenant.Create(new FakeTenantForCreation().RuleFor(t => t.Name, _ => name).Generate());

    private static string? NewValueOf(AuditEntry entry, string path)
        => JsonDocument.Parse(entry.Data).RootElement.GetProperty(path).GetProperty("new").GetString();

    /// <summary>
    /// Breaks the connection at the first commit after <see cref="Arm"/>, before the commit or after it. After it, the
    /// rows are in, but the caller sees a failure, as when the acknowledgment of the commit is lost.
    /// </summary>
    private sealed class FailFirstCommit(bool afterCommit) : DbTransactionInterceptor
    {
        private int _armed;

        public int Failures { get; private set; }

        public void Arm() => Interlocked.Exchange(ref _armed, 1);

        public override ValueTask<InterceptionResult> TransactionCommittingAsync(DbTransaction transaction,
            TransactionEventData eventData, InterceptionResult result, CancellationToken cancellationToken = default)
        {
            if (!afterCommit)
                FailWhenArmed();
            return ValueTask.FromResult(result);
        }

        public override Task TransactionCommittedAsync(DbTransaction transaction, TransactionEndEventData eventData,
            CancellationToken cancellationToken = default)
        {
            if (afterCommit)
                FailWhenArmed();
            return Task.CompletedTask;
        }

        private void FailWhenArmed()
        {
            if (Interlocked.Exchange(ref _armed, 0) == 0)
                return;

            Failures++;
            throw new NpgsqlException("Injected loss of the connection.", new IOException());
        }
    }
}
