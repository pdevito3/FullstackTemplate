namespace FullstackTemplate.IntegrationTests.Databases;

using FullstackTemplate.IntegrationTests.DomainEvents;
using FullstackTemplate.Server.Databases;
using FullstackTemplate.Server.Domain.DPoPProofs;
using FullstackTemplate.Server.Domain.Tenants.DomainEvents;
using FullstackTemplate.Server.Domain.Tenants.Models;
using FullstackTemplate.Server.Domain.Users;
using FullstackTemplate.Server.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Shouldly;
using static FullstackTemplate.IntegrationTests.DomainEvents.DomainEventTestServices;

public class WriteGuardTests : TestBase
{
    public delegate Task HandlerWrite(AppDbContext dbContext, string userId, CancellationToken cancellationToken);

    public delegate Task Write(AppDbContext dbContext, string userId);

    public static IEnumerable<Func<(string Write, HandlerWrite Run)>> Writes()
    {
        yield return () => ("SaveChangesAsync", (db, _, ct) => db.SaveChangesAsync(ct));
        yield return () => ("ExecuteUpdateAsync", (db, userId, ct) => db.Users.Where(u => u.Id == userId)
            .ExecuteUpdateAsync(set => set.SetProperty(u => u.FirstName, "Changed"), ct));
        yield return () => ("ExecuteDeleteAsync", (db, userId, ct) => db.UserPermissions.Where(p => p.UserId == userId)
            .ExecuteDeleteAsync(ct));
        yield return () => ("raw SQL", (db, userId, ct) => db.Database.ExecuteSqlAsync(
            $"UPDATE users SET first_name = 'Changed' WHERE id = {userId}", ct));
        yield return () => ("SqlQuery with UPDATE RETURNING", async (db, userId, ct) => await db.Database.SqlQuery<int>(
            $"UPDATE users SET first_name = 'Changed' WHERE id = {userId} RETURNING 1 AS \"Value\"").ToListAsync(ct));
        yield return () => ("FromSql with a writable CTE", async (db, userId, ct) => await db.Tenants.FromSql($"""
                WITH changed AS (UPDATE users SET first_name = 'Changed' WHERE id = {userId} RETURNING tenant_id)
                SELECT * FROM tenants WHERE id IN (SELECT tenant_id FROM changed)
                """)
            .IgnoreQueryFilters().AsNoTracking().ToListAsync(ct));
        yield return () => ("BeginTransactionAsync", async (db, _, ct) => await db.Database.BeginTransactionAsync(ct));
        yield return () => ("UseTransactionAsync", async (db, _, ct) =>
        {
            await db.Database.OpenConnectionAsync(ct);
            var transaction = await db.Database.GetDbConnection().BeginTransactionAsync(ct);
            var refusal = await Should.ThrowAsync<InvalidOperationException>(
                () => db.Database.UseTransactionAsync(transaction, ct));
            refusal.Message.ShouldStartWith("A domain event handler must not begin or use a transaction.");
            throw refusal;
        });
    }

    public static IEnumerable<Func<(string Write, string Table, Write Run)>> WritesOfAuditedTables()
    {
        yield return () => ("ExecuteUpdateAsync", "users", (db, userId) => db.Users.Where(u => u.Id == userId)
            .ExecuteUpdateAsync(set => set.SetProperty(u => u.FirstName, "Changed")));
        yield return () => ("ExecuteDeleteAsync", "user_permissions", (db, userId) => db.UserPermissions
            .Where(p => p.UserId == userId).ExecuteDeleteAsync());
        yield return () => ("raw SQL", "users", (db, userId) => db.Database.ExecuteSqlAsync(
            $"UPDATE users SET first_name = 'Changed' WHERE id = {userId}"));
        yield return () => ("raw SQL on the audit log", "audit_entries", (db, userId) => db.Database.ExecuteSqlAsync(
            $"DELETE FROM audit_entries WHERE root_id = {userId}"));
        yield return () => ("SqlQuery with UPDATE RETURNING", "users", async (db, userId) => await db.Database.SqlQuery<int>(
            $"UPDATE users SET first_name = 'Changed' WHERE id = {userId} RETURNING 1 AS \"Value\"").ToListAsync());
        yield return () => ("FromSql with a writable CTE", "users", async (db, userId) => await db.Tenants.FromSql($"""
                WITH changed AS (UPDATE users SET first_name = 'Changed' WHERE id = {userId} RETURNING tenant_id)
                SELECT * FROM tenants WHERE id IN (SELECT tenant_id FROM changed)
                """)
            .IgnoreQueryFilters().AsNoTracking().ToListAsync());
    }

    [Test]
    [MethodDataSource(nameof(WritesOfAuditedTables))]
    public async Task write_of_an_audited_table_that_skips_the_save_is_refused(string write, string table, Write run)
    {
        // Arrange
        var scope = new TestingServiceScope();
        var scenario = await scope.StartTenantScenarioAsync();
        var permissionsBefore = await ReadAsync(read => read.UserPermissions.CountAsync(p => p.UserId == scenario.FirstUser.Id));
        var auditEntriesBefore = await ReadAsync(read => read.AuditEntries.CountAsync(e => e.RootId == scenario.FirstUser.Id));

        // Act
        var act = () => run(scope.GetService<AppDbContext>(), scenario.FirstUser.Id);

        // Assert
        (await act.ShouldThrowAsync<InvalidOperationException>()).Message
            .ShouldStartWith($"{table} is audited, so write it through SaveChangesAsync or BulkInsertAsync.", customMessage: write);
        await ShouldBeUnchangedAsync(scenario, permissionsBefore);
        (await ReadAsync(read => read.AuditEntries.CountAsync(e => e.RootId == scenario.FirstUser.Id)))
            .ShouldBe(auditEntriesBefore);
    }

    public static IEnumerable<Func<(string Write, Write Run)>> HiddenCode()
    {
        yield return () => ("DO block", (db, userId) =>
        {
            var sql = $"DO $$ BEGIN UPDATE users SET first_name = 'Changed' WHERE id = '{userId}'; END $$";
            return db.Database.ExecuteSqlRawAsync(sql);
        });
        yield return () => ("DO block in quotes", (db, userId) =>
        {
            var sql = $"DO 'BEGIN UPDATE users SET first_name = ''Changed'' WHERE id = ''{userId}''; END'";
            return db.Database.ExecuteSqlRawAsync(sql);
        });
        yield return () => ("CALL", (db, _) => db.Database.ExecuteSqlRawAsync("CALL rename_users()"));
        yield return () => ("SqlQuery with dollar-quoted text", async (db, _) => await db.Database.SqlQueryRaw<string>(
            "SELECT $q$x$q$ AS \"Value\"").ToListAsync());
    }

    [Test]
    [MethodDataSource(nameof(HiddenCode))]
    public async Task raw_sql_that_hides_the_tables_it_writes_is_refused(string write, Write run)
    {
        // Arrange
        var scope = new TestingServiceScope();
        var scenario = await scope.StartTenantScenarioAsync();
        var permissionsBefore = await ReadAsync(read => read.UserPermissions.CountAsync(p => p.UserId == scenario.FirstUser.Id));

        // Act
        var act = () => run(scope.GetService<AppDbContext>(), scenario.FirstUser.Id);

        // Assert
        (await act.ShouldThrowAsync<InvalidOperationException>()).Message
            .ShouldStartWith("Raw SQL must not run a DO block, a CALL, or dollar-quoted text", customMessage: write);
        await ShouldBeUnchangedAsync(scenario, permissionsBefore);
    }

    [Test]
    public async Task raw_read_of_an_audited_table_and_raw_write_of_a_table_with_no_audit_run()
    {
        // Arrange
        var scope = new TestingServiceScope();
        var scenario = await scope.StartTenantScenarioAsync();
        var db = scope.GetService<AppDbContext>();
        var proofId = $"probe-{Guid.NewGuid():N}";

        // Act
        var firstNames = await db.Database.SqlQuery<string>(
            $"SELECT first_name AS \"Value\" FROM users WHERE id = {scenario.FirstUser.Id}").ToListAsync();
        var inserted = await db.Database.ExecuteSqlAsync(
            $"INSERT INTO used_dpop_proofs (id, expires) VALUES ({proofId}, {DateTimeOffset.UtcNow}) ON CONFLICT (id) DO NOTHING");
        var deleted = await db.Set<UsedDPoPProof>().Where(p => p.Id == proofId).ExecuteDeleteAsync();

        // Assert
        firstNames.ShouldBe([scenario.FirstUser.FirstName]);
        (inserted, deleted).ShouldBe((1, 1));
    }

    [Test]
    [MethodDataSource(nameof(Writes))]
    public async Task handler_write_that_bypasses_the_save_is_refused_and_nothing_is_saved(string write, HandlerWrite run)
    {
        // Arrange
        string? userId = null;
        await using var services = TestFixture.BuildServiceProvider(s => s.AddTestHandler<TenantUpdated>(
            (_, db, ct) => run(db, userId!, ct)));
        var scope = new TestingServiceScope(services);
        var scenario = await scope.StartTenantScenarioAsync();
        userId = scenario.FirstUser.Id;
        var permissionsBefore = await ReadAsync(read => read.UserPermissions.CountAsync(p => p.UserId == userId));
        var db = scope.GetService<AppDbContext>();
        var tenant = await db.Tenants.SingleAsync(t => t.Id == scenario.Tenant.Id);

        // Act
        tenant.Update(new TenantForUpdate { Name = $"renamed-{Guid.NewGuid()}", Version = tenant.Version });
        var act = () => db.SaveChangesAsync();

        // Assert
        (await act.ShouldThrowAsync<InvalidOperationException>()).Message
            .ShouldStartWith("A domain event handler must not", customMessage: write);
        await ShouldBeUnchangedAsync(scenario, permissionsBefore);
    }

    [Test]
    public async Task handler_reads_with_linq_and_the_save_commits()
    {
        // Arrange
        string? userId = null;
        var reads = new List<object?>();
        await using var services = TestFixture.BuildServiceProvider(s => s.AddTestHandler<TenantUpdated>(async (e, db, ct) =>
        {
            reads.Add((await db.Users.FindAsync([userId], ct))?.Id);
            var user = await db.Users.FirstOrDefaultAsync(u => u.Id == userId, ct);
            reads.Add(user?.Id);
            reads.Add(await db.Users.CountAsync(u => u.TenantId == e.Id, ct));
            reads.Add(await db.Users.AnyAsync(u => u.Id == userId, ct));
            reads.Add((await db.Users.AsNoTracking().Include(u => u.UserPermissions).SingleAsync(u => u.Id == userId, ct)).Id);
            await db.Entry(user!).Collection(u => u.UserPermissions).LoadAsync(ct);
            reads.Add(db.Entry(user!).Collection(u => u.UserPermissions).IsLoaded);
            user!.UpdateRole(UserRole.Admin());
        }));
        var scope = new TestingServiceScope(services);
        var scenario = await scope.StartTenantScenarioAsync();
        userId = scenario.FirstUser.Id;
        var db = scope.GetService<AppDbContext>();
        var tenant = await db.Tenants.SingleAsync(t => t.Id == scenario.Tenant.Id);

        // Act
        tenant.Update(new TenantForUpdate { Name = $"renamed-{Guid.NewGuid()}", Version = tenant.Version });
        await db.SaveChangesAsync();

        // Assert
        reads.ShouldBe(new object?[] { userId, userId, 3, true, userId, true });
        (await ReadAsync(read => read.Users.SingleAsync(u => u.Id == userId))).Role.ShouldBe(UserRole.Admin());
    }

    [Test]
    public async Task context_built_by_hand_also_refuses_a_handler_write()
    {
        // Arrange
        string? userId = null;
        await using var services = TestFixture.BuildServiceProvider(s => s.AddTestHandler<TenantUpdated>(
            (_, db, ct) => db.Users.Where(u => u.Id == userId)
                .ExecuteUpdateAsync(set => set.SetProperty(u => u.FirstName, "Changed"), ct)));
        var scope = new TestingServiceScope(services);
        var scenario = await scope.StartTenantScenarioAsync();
        userId = scenario.FirstUser.Id;
        var permissionsBefore = await ReadAsync(read => read.UserPermissions.CountAsync(p => p.UserId == userId));
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseNpgsql(scope.GetService<IConfiguration>().GetConnectionString(DatabaseConsts.DatabaseName))
            .UseSnakeCaseNamingConvention()
            .Options;
        await using var db = new AppDbContext(options, TimeProvider.System, scope.GetService<ICurrentUserService>(),
            scope.GetService<ICallerIdentityResolver>(), scope.GetService<ITenantScopeProvider>(), scope.GetService<DomainEventDispatcher>());
        var tenant = await db.Tenants.SingleAsync(t => t.Id == scenario.Tenant.Id);

        // Act
        tenant.Update(new TenantForUpdate { Name = $"renamed-{Guid.NewGuid()}", Version = tenant.Version });
        var act = () => db.SaveChangesAsync();

        // Assert
        (await act.ShouldThrowAsync<InvalidOperationException>()).Message.ShouldStartWith("A domain event handler must not");
        await ShouldBeUnchangedAsync(scenario, permissionsBefore);
    }

    private static async Task ShouldBeUnchangedAsync(TenantScenario scenario, int permissionsBefore)
    {
        (await ReadAsync(read => read.Tenants.Where(t => t.Id == scenario.Tenant.Id).Select(t => t.Name).SingleAsync()))
            .ShouldBe(scenario.Tenant.Name);
        (await ReadAsync(read => read.Users.Where(u => u.Id == scenario.FirstUser.Id).Select(u => u.FirstName).SingleAsync()))
            .ShouldBe(scenario.FirstUser.FirstName);
        (await ReadAsync(read => read.UserPermissions.CountAsync(p => p.UserId == scenario.FirstUser.Id)))
            .ShouldBe(permissionsBefore);
    }
}
