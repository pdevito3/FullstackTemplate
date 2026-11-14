namespace FullstackTemplate.IntegrationTests.Jobs;

using FullstackTemplate.IntegrationTests.Databases;
using FullstackTemplate.IntegrationTests.DomainEvents;
using FullstackTemplate.Server.Databases;
using FullstackTemplate.Server.Domain.AuditEntries;
using FullstackTemplate.Server.Domain.DPoPProofs;
using FullstackTemplate.Server.Resources.Jobs;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;
using Shouldly;
using static FullstackTemplate.IntegrationTests.DomainEvents.DomainEventTestServices;

/// <summary>
/// A save with queued jobs that loses the connection at the commit. When the commit did not occur, the save runs again
/// and writes the jobs one time. When it occurred, the save finds its commit and does not run again.
/// </summary>
public class QueueJobLostCommitReplyTests : TestBase
{
    [Test]
    [Arguments(false)]
    [Arguments(true)]
    public async Task save_of_a_change_with_audit_and_a_job_commits_one_job(bool afterCommit)
    {
        // Arrange
        var failCommit = new FailFirstCommit(afterCommit);
        await using var services = FailingFirstCommit(failCommit);
        var scope = new TestingServiceScope(services);
        var scenario = await scope.StartTenantScenarioAsync();
        var db = scope.GetService<AppDbContext>();
        var user = await db.Users.SingleAsync(u => u.Id == scenario.FirstUser.Id);
        user.UpdateFromIdp($"{user.FirstName}x", user.LastName, user.Email, user.Username);
        db.QueueJob(new TestJob(scenario.Tenant.Id, "audited"));
        failCommit.Arm();

        // Act
        var saved = await db.SaveChangesAsync();

        // Assert
        failCommit.Failures.ShouldBe(1);
        saved.ShouldBe(1);
        (await TestJob.OfTenantAsync(scenario.Tenant.Id)).Count.ShouldBe(1);
        (await ReadAsync(read => read.Users.Where(u => u.Id == user.Id).Select(u => u.Version).SingleAsync()))
            .ShouldBe(user.Version);
    }

    [Test]
    [Arguments(false)]
    [Arguments(true)]
    public async Task save_of_a_change_with_no_audit_and_a_job_commits_one_job(bool afterCommit)
    {
        // Arrange
        var failCommit = new FailFirstCommit(afterCommit);
        await using var services = FailingFirstCommit(failCommit);
        var scope = new TestingServiceScope(services);
        var scenario = await scope.StartTenantScenarioAsync();
        var db = scope.GetService<AppDbContext>();
        var proofId = $"probe-{Guid.NewGuid():N}";
        db.Set<UsedDPoPProof>().Add(new UsedDPoPProof(proofId, DateTimeOffset.UtcNow.AddMinutes(5)));
        db.QueueJob(new TestJob(scenario.Tenant.Id, "not audited"));
        failCommit.Arm();

        // Act
        var saved = await db.SaveChangesAsync();

        // Assert
        failCommit.Failures.ShouldBe(1);
        saved.ShouldBe(1);
        (await TestJob.OfTenantAsync(scenario.Tenant.Id)).Count.ShouldBe(1);
        (await ReadAsync(read => read.Set<UsedDPoPProof>().CountAsync(p => p.Id == proofId))).ShouldBe(1);
        db.ChangeTracker.HasChanges().ShouldBeFalse();
    }

    [Test]
    [Arguments(false)]
    [Arguments(true)]
    public async Task next_save_of_a_row_that_a_save_with_no_audit_and_a_job_wrote_does_not_conflict(bool afterCommit)
    {
        // Arrange
        var failCommit = new FailFirstCommit(afterCommit);
        await using var services = FailingFirstCommit(failCommit);
        var scope = new TestingServiceScope(services);
        var scenario = await scope.StartTenantScenarioAsync();
        var db = scope.GetService<AppDbContext>();
        var user = await db.Users.SingleAsync(u => u.Id == scenario.FirstUser.Id);
        // The save writes the row, so its xmin changes, but no recorded value changes, so it has no audit entry
        db.Entry(user).State = EntityState.Modified;
        db.QueueJob(new TestJob(scenario.Tenant.Id, "no audit entry"));
        failCommit.Arm();
        await db.SaveChangesAsync();
        user.UpdateFromIdp($"{user.FirstName}x", user.LastName, user.Email, user.Username);

        // Act
        await db.SaveChangesAsync();

        // Assert
        failCommit.Failures.ShouldBe(1);
        var stored = await ReadAsync(read => read.Users.Where(u => u.Id == user.Id)
            .Select(u => new { u.FirstName, u.Version }).SingleAsync());
        stored.FirstName.ShouldBe($"{scenario.FirstUser.FirstName}x");
        stored.Version.ShouldBe(user.Version);
    }

    [Test]
    [Arguments(false)]
    [Arguments(true)]
    public async Task save_of_jobs_with_no_change_commits_each_job_one_time(bool afterCommit)
    {
        // Arrange
        var failCommit = new FailFirstCommit(afterCommit);
        await using var services = FailingFirstCommit(failCommit);
        var scope = new TestingServiceScope(services);
        var scenario = await scope.StartTenantScenarioAsync();
        var db = scope.GetService<AppDbContext>();
        db.QueueJob(new TestJob(scenario.Tenant.Id, "first"));
        db.QueueJob(new TestJob(scenario.Tenant.Id, "second"));
        failCommit.Arm();

        // Act
        var saved = await db.SaveChangesAsync();

        // Assert
        failCommit.Failures.ShouldBe(1);
        saved.ShouldBe(0);
        (await TestJob.OfTenantAsync(scenario.Tenant.Id)).Count.ShouldBe(2);
    }

    [Test]
    [Arguments(false)]
    [Arguments(true)]
    public async Task save_in_the_tenant_lock_of_a_change_and_a_job_commits_one_job(bool afterCommit)
    {
        // Arrange
        var failCommit = new FailFirstCommit(afterCommit);
        await using var services = FailingFirstCommit(failCommit);
        var scope = new TestingServiceScope(services);
        var scenario = await scope.StartTenantScenarioAsync();
        var db = scope.GetService<AppDbContext>();
        var runs = 0;
        failCommit.Arm();

        // Act
        await RootLock.Tenant.RunSharedAsync(db, scenario.Tenant.Id, async ct =>
        {
            runs++;
            var user = await db.Users.SingleAsync(u => u.Id == scenario.FirstUser.Id, ct);
            user.UpdateFromIdp($"{scenario.FirstUser.FirstName}x", user.LastName, user.Email, user.Username);
            db.QueueJob(new TestJob(scenario.Tenant.Id, $"run {runs}"));
            return await db.SaveChangesAsync(ct);
        }, CancellationToken.None);

        // Assert
        failCommit.Failures.ShouldBe(1);
        runs.ShouldBe(afterCommit ? 1 : 2);
        (await TestJob.OfTenantAsync(scenario.Tenant.Id)).Count.ShouldBe(1);
    }

    [Test]
    public async Task save_of_a_change_that_loses_the_connection_after_the_job_write_commits_each_one_time()
    {
        // Arrange
        await using var services = FailAfterJobWrite.Around(LostConnection());
        var writer = (FailAfterJobWrite)services.GetRequiredService<IJobWriter>();
        var scope = new TestingServiceScope(services);
        var scenario = await scope.StartTenantScenarioAsync();
        var db = scope.GetService<AppDbContext>();
        var user = await db.Users.SingleAsync(u => u.Id == scenario.FirstUser.Id);
        user.UpdateFromIdp($"{user.FirstName}x", user.LastName, user.Email, user.Username);
        db.QueueJob(new TestJob(scenario.Tenant.Id, "after the job write"));

        // Act
        await db.SaveChangesAsync();

        // Assert
        writer.Failures.ShouldBe(1);
        (await TestJob.OfTenantAsync(scenario.Tenant.Id)).Count.ShouldBe(1);
        (await ReadAsync(read => read.AuditEntries
            .CountAsync(e => e.RootId == user.Id && e.Kind == AuditEntryKind.Updated)))
            .ShouldBe(1);
    }

    [Test]
    public async Task save_of_jobs_that_loses_the_connection_after_the_job_write_commits_each_job_one_time()
    {
        // Arrange
        await using var services = FailAfterJobWrite.Around(LostConnection());
        var writer = (FailAfterJobWrite)services.GetRequiredService<IJobWriter>();
        var scope = new TestingServiceScope(services);
        var scenario = await scope.StartTenantScenarioAsync();
        var db = scope.GetService<AppDbContext>();
        db.QueueJob(new TestJob(scenario.Tenant.Id, "first"));
        db.QueueJob(new TestJob(scenario.Tenant.Id, "second"));

        // Act
        await db.SaveChangesAsync();

        // Assert
        writer.Failures.ShouldBe(1);
        (await TestJob.OfTenantAsync(scenario.Tenant.Id)).Count.ShouldBe(2);
    }

    private static NpgsqlException LostConnection() => new("Injected loss of the connection.", new IOException());

    private static ServiceProvider FailingFirstCommit(FailFirstCommit failCommit)
        => TestFixture.BuildServiceProvider(s => s
            .ConfigureDbContext<AppDbContext>((_, options) => options.AddInterceptors(failCommit)));
}
