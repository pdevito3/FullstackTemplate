namespace FullstackTemplate.IntegrationTests.Jobs;

using BackWave.Storage;
using FullstackTemplate.IntegrationTests.DomainEvents;
using FullstackTemplate.Server.Databases;
using FullstackTemplate.Server.Domain.AuditEntries;
using FullstackTemplate.Server.Domain.DPoPProofs;
using FullstackTemplate.Server.Domain.Tenants.DomainEvents;
using FullstackTemplate.Server.Domain.Tenants.Models;
using FullstackTemplate.Server.Resources.Jobs;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using Shouldly;
using static FullstackTemplate.IntegrationTests.DomainEvents.DomainEventTestServices;

public class QueueJobTests : TestBase
{
    [Test]
    public async Task job_queued_with_a_change_commits_with_the_change_and_its_audit_entry()
    {
        // Arrange
        var scope = new TestingServiceScope();
        var scenario = await scope.StartTenantScenarioAsync();
        var db = scope.GetService<AppDbContext>();
        var user = await db.Users.SingleAsync(u => u.Id == scenario.FirstUser.Id);

        // Act
        user.UpdateFromIdp($"{user.FirstName}x", user.LastName, user.Email, user.Username);
        db.QueueJob(new TestJob(scenario.Tenant.Id, "with a change"));
        var saved = await db.SaveChangesAsync();

        // Assert
        saved.ShouldBe(1);
        var job = (await TestJob.OfTenantAsync(scenario.Tenant.Id)).ShouldHaveSingleItem();
        job.State.ShouldBe(JobState.Scheduled);
        job.Tags.ShouldContain(JobTag.Keyed(PendingJob.TenantTag, scenario.Tenant.Id));
        (await ReadAsync(read => read.Users.Where(u => u.Id == user.Id).Select(u => u.FirstName).SingleAsync()))
            .ShouldBe(user.FirstName);
        (await ReadAsync(read => read.AuditEntries
            .CountAsync(e => e.RootId == user.Id && e.Kind == AuditEntryKind.Updated)))
            .ShouldBe(1);
    }

    [Test]
    public async Task job_queued_with_no_change_commits_alone()
    {
        // Arrange
        var scope = new TestingServiceScope();
        var scenario = await scope.StartTenantScenarioAsync();
        var db = scope.GetService<AppDbContext>();

        // Act
        db.QueueJob(new TestJob(scenario.Tenant.Id, "first"));
        db.QueueJob(new TestJob(scenario.Tenant.Id, "second"));
        var saved = await db.SaveChangesAsync();

        // Assert
        saved.ShouldBe(0);
        (await TestJob.OfTenantAsync(scenario.Tenant.Id)).Count.ShouldBe(2);
    }

    [Test]
    public async Task job_queued_with_a_change_that_has_no_audit_commits_with_the_change()
    {
        // Arrange
        var scope = new TestingServiceScope();
        var scenario = await scope.StartTenantScenarioAsync();
        var db = scope.GetService<AppDbContext>();
        var proofId = $"probe-{Guid.NewGuid():N}";

        // Act
        db.Set<UsedDPoPProof>().Add(new UsedDPoPProof(proofId, DateTimeOffset.UtcNow.AddMinutes(5)));
        db.QueueJob(new TestJob(scenario.Tenant.Id, "with a proof"));
        var saved = await db.SaveChangesAsync();

        // Assert
        saved.ShouldBe(1);
        (await TestJob.OfTenantAsync(scenario.Tenant.Id)).Count.ShouldBe(1);
        (await ReadAsync(read => read.Set<UsedDPoPProof>().CountAsync(p => p.Id == proofId))).ShouldBe(1);
    }

    [Test]
    public async Task job_queued_by_a_domain_event_handler_commits_with_the_save()
    {
        // Arrange
        await using var services = TestFixture.BuildServiceProvider(s => s.AddTestHandler<TenantUpdated>((e, db, _) =>
        {
            db.QueueJob(new TestJob(e.Id, "from a handler"));
            return Task.CompletedTask;
        }));
        var scope = new TestingServiceScope(services);
        var scenario = await scope.StartTenantScenarioAsync();
        var db = scope.GetService<AppDbContext>();
        var tenant = await db.Tenants.SingleAsync(t => t.Id == scenario.Tenant.Id);

        // Act
        tenant.Update(new TenantForUpdate { Name = $"renamed-{Guid.NewGuid()}", Version = tenant.Version });
        await db.SaveChangesAsync();

        // Assert
        (await TestJob.OfTenantAsync(scenario.Tenant.Id)).Count.ShouldBe(1);
    }

    [Test]
    public async Task next_save_does_not_write_the_jobs_of_an_earlier_save_again()
    {
        // Arrange
        var scope = new TestingServiceScope();
        var scenario = await scope.StartTenantScenarioAsync();
        var db = scope.GetService<AppDbContext>();
        db.QueueJob(new TestJob(scenario.Tenant.Id, "once"));
        await db.SaveChangesAsync();
        var user = await db.Users.SingleAsync(u => u.Id == scenario.FirstUser.Id);

        // Act
        user.UpdateFromIdp($"{user.FirstName}x", user.LastName, user.Email, user.Username);
        await db.SaveChangesAsync();

        // Assert
        (await TestJob.OfTenantAsync(scenario.Tenant.Id)).Count.ShouldBe(1);
    }

    [Test]
    public async Task save_that_fails_after_it_writes_the_jobs_commits_no_job_and_no_change()
    {
        // Arrange
        await using var services = FailAfterJobWrite.Around(new InvalidOperationException("The job write failed."));
        var writer = (FailAfterJobWrite)services.GetRequiredService<IJobWriter>();
        var scope = new TestingServiceScope(services);
        var scenario = await scope.StartTenantScenarioAsync();
        var db = scope.GetService<AppDbContext>();
        var user = await db.Users.SingleAsync(u => u.Id == scenario.FirstUser.Id);
        writer.Arm();

        // Act
        user.UpdateFromIdp($"{user.FirstName}x", user.LastName, user.Email, user.Username);
        db.QueueJob(new TestJob(scenario.Tenant.Id, "rolled back"));
        var act = () => db.SaveChangesAsync();

        // Assert
        (await act.ShouldThrowAsync<InvalidOperationException>()).Message.ShouldBe("The job write failed.");
        (await TestJob.OfTenantAsync(scenario.Tenant.Id)).ShouldBeEmpty();
        (await ReadAsync(read => read.Users.Where(u => u.Id == user.Id).Select(u => u.FirstName).SingleAsync()))
            .ShouldBe(scenario.FirstUser.FirstName);
    }

    [Test]
    public async Task job_for_a_tenant_that_the_caller_cannot_see_is_refused()
    {
        // Arrange
        var scope = new TestingServiceScope();
        var other = await scope.SetupTenantAsync();
        await scope.StartTenantScenarioAsync();
        var db = scope.GetService<AppDbContext>();

        // Act
        var act = () => db.QueueJob(new TestJob(other.Id, "other tenant"));

        // Assert
        act.ShouldThrow<InvalidOperationException>().Message.ShouldBe(
            $"The caller cannot see tenant {other.Id}, so it cannot queue TestJob for that tenant.");
        await db.SaveChangesAsync();
        (await TestJob.OfTenantAsync(other.Id)).ShouldBeEmpty();
    }

    [Test]
    public async Task job_for_any_tenant_from_a_machine_client_that_manages_all_tenants_commits()
    {
        // Arrange
        var scope = new TestingServiceScope();
        var tenant = await scope.SetupTenantAsync();
        scope.SetMachineClient(TestFixture.TrustedMachineClientId);
        var db = scope.GetService<AppDbContext>();

        // Act
        db.QueueJob(new TestJob(tenant.Id, "from a trusted machine"));
        await db.SaveChangesAsync();

        // Assert
        (await TestJob.OfTenantAsync(tenant.Id)).Count.ShouldBe(1);
    }

    [Test]
    public void job_with_a_tenant_id_that_is_not_a_tenant_id_is_refused()
    {
        // Arrange
        var scope = new TestingServiceScope();
        var db = scope.GetService<AppDbContext>();

        // Act
        var act = () => db.QueueJob(new TestJob("usr_01JZ8QAQ0Q7S0D5Y2N3V4W5X6Y", "not a tenant"));

        // Assert
        act.ShouldThrow<ArgumentException>().Message.ShouldStartWith(
            "TestJob has the tenant ID 'usr_01JZ8QAQ0Q7S0D5Y2N3V4W5X6Y', which is not a tenant ID.");
    }

    [Test]
    public async Task job_queued_while_the_save_writes_is_refused_and_the_save_fails()
    {
        // Arrange
        var lateQueue = new QueueWhileSaving();
        await using var services = TestFixture.BuildServiceProvider(s => s
            .ConfigureDbContext<AppDbContext>((_, options) => options.AddInterceptors(lateQueue)));
        var scope = new TestingServiceScope(services);
        var scenario = await scope.StartTenantScenarioAsync();
        var db = scope.GetService<AppDbContext>();
        var user = await db.Users.SingleAsync(u => u.Id == scenario.FirstUser.Id);
        lateQueue.TenantId = scenario.Tenant.Id;

        // Act
        user.UpdateFromIdp($"{user.FirstName}x", user.LastName, user.Email, user.Username);
        var act = () => db.SaveChangesAsync();

        // Assert
        (await act.ShouldThrowAsync<InvalidOperationException>()).Message.ShouldBe(
            "Queue a job before the save writes: in a handler, or in a domain event handler.");
        (await TestJob.OfTenantAsync(scenario.Tenant.Id)).ShouldBeEmpty();
    }

    [Test]
    public async Task job_queued_after_a_failed_save_is_refused()
    {
        // Arrange
        await using var services = TestFixture.BuildServiceProvider(s => s.AddTestHandler<TenantUpdated>(
            (_, _, _) => throw new InvalidOperationException("The handler failed.")));
        var scope = new TestingServiceScope(services);
        var scenario = await scope.StartTenantScenarioAsync();
        var db = scope.GetService<AppDbContext>();
        var tenant = await db.Tenants.SingleAsync(t => t.Id == scenario.Tenant.Id);
        tenant.Update(new TenantForUpdate { Name = $"renamed-{Guid.NewGuid()}", Version = tenant.Version });
        await Should.ThrowAsync<InvalidOperationException>(() => db.SaveChangesAsync());

        // Act
        var act = () => db.QueueJob(new TestJob(scenario.Tenant.Id, "after a failure"));

        // Assert
        act.ShouldThrow<InvalidOperationException>().Message.ShouldBe(
            "An earlier save on this AppDbContext failed, so it cannot queue a job. Use a new DI scope.");
    }

    [Test]
    public async Task request_that_queues_a_job_and_saves_commits_the_job()
    {
        // Arrange
        await using var services = TestFixture.BuildServiceProvider(AddQueueRequest);
        var scope = new TestingServiceScope(services);
        var scenario = await scope.StartTenantScenarioAsync();

        // Act
        await scope.SendAsync(new QueueTestJob.Command(scenario.Tenant.Id, Save: true));

        // Assert
        (await TestJob.OfTenantAsync(scenario.Tenant.Id)).Count.ShouldBe(1);
    }

    [Test]
    public async Task request_that_queues_a_job_and_does_not_save_it_fails()
    {
        // Arrange
        await using var services = TestFixture.BuildServiceProvider(AddQueueRequest);
        var scope = new TestingServiceScope(services);
        var scenario = await scope.StartTenantScenarioAsync();

        // Act
        var act = () => scope.SendAsync(new QueueTestJob.Command(scenario.Tenant.Id, Save: false));

        // Assert
        (await act.ShouldThrowAsync<InvalidOperationException>()).Message.ShouldBe(
            "QueueTestJob.Command queued a job and did not call SaveChangesAsync. Save it, also when the handler changes no data.");
        (await TestJob.OfTenantAsync(scenario.Tenant.Id)).ShouldBeEmpty();
    }

    [Test]
    public async Task lock_refuses_work_that_starts_after_a_job_is_queued()
    {
        // Arrange
        var scope = new TestingServiceScope();
        var scenario = await scope.StartTenantScenarioAsync();
        var db = scope.GetService<AppDbContext>();
        db.QueueJob(new TestJob(scenario.Tenant.Id, "before the lock"));

        // Act
        var act = () => RootLock.Tenant.RunSharedAsync(db, scenario.Tenant.Id, ct => db.SaveChangesAsync(ct),
            CancellationToken.None);

        // Assert
        (await act.ShouldThrowAsync<InvalidOperationException>()).Message.ShouldStartWith(
            "Take the lock before the first change.");
    }

    [Test]
    public async Task bulk_insert_refuses_a_queued_job()
    {
        // Arrange
        var scope = new TestingServiceScope();
        var scenario = await scope.StartTenantScenarioAsync();
        var db = scope.GetService<AppDbContext>();
        db.QueueJob(new TestJob(scenario.Tenant.Id, "before a bulk insert"));

        // Act
        var act = () => db.BulkInsertAsync([NewUser(scenario.Tenant.Id, $"bulk-{Guid.NewGuid():N}")]);

        // Assert
        (await act.ShouldThrowAsync<InvalidOperationException>()).Message.ShouldBe(
            "BulkInsertAsync does not write queued jobs. Save them with SaveChangesAsync first.");
    }

    private static void AddQueueRequest(IServiceCollection services)
        => services.AddTransient<IRequestHandler<QueueTestJob.Command>, QueueTestJob.Handler>();

    public static class QueueTestJob
    {
        public sealed record Command(string TenantId, bool Save) : IRequest;

        public sealed class Handler(AppDbContext dbContext) : IRequestHandler<Command>
        {
            public async Task Handle(Command request, CancellationToken cancellationToken)
            {
                dbContext.QueueJob(new TestJob(request.TenantId, "from a request"));
                if (request.Save)
                    await dbContext.SaveChangesAsync(cancellationToken);
            }
        }
    }

    /// <summary>Queues a job from a SaveChanges interceptor, after the save starts to write.</summary>
    private sealed class QueueWhileSaving : SaveChangesInterceptor
    {
        public string? TenantId { get; set; }

        public override ValueTask<InterceptionResult<int>> SavingChangesAsync(DbContextEventData eventData,
            InterceptionResult<int> result, CancellationToken cancellationToken = default)
        {
            if (TenantId is { } tenantId)
                ((AppDbContext)eventData.Context!).QueueJob(new TestJob(tenantId, "while the save writes"));
            return ValueTask.FromResult(result);
        }
    }
}
