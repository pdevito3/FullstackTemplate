namespace FullstackTemplate.IntegrationTests.Jobs;

using System.Diagnostics;
using BackWave.Jobs;
using FullstackTemplate.IntegrationTests.DomainEvents;
using FullstackTemplate.Server.Databases;
using FullstackTemplate.Server.Domain.AuditEntries;
using FullstackTemplate.Server.Domain.Users;
using FullstackTemplate.Server.Domain.Users.Features;
using FullstackTemplate.Server.Exceptions;
using FullstackTemplate.Server.Resources.Jobs;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Shouldly;
using static FullstackTemplate.IntegrationTests.DomainEvents.DomainEventTestServices;

/// <summary>
/// A job runs in a DI scope with no request, as BackWave gives it for each attempt. <see cref="JobHandler{TJob}"/>
/// makes the scope the caller of the tenant of the job.
/// </summary>
public class JobHandlerTests : TestBase
{
    [Test]
    public async Task job_sees_only_the_rows_of_its_tenant()
    {
        // Arrange
        var scenario = await new TestingServiceScope().StartTenantScenarioAsync();
        await new TestingServiceScope().SetupTenantAsync();
        List<string> tenantsOfUsers = [];

        // Act
        await RunJobAsync(scenario.Tenant.Id, async services => tenantsOfUsers = await services
            .GetRequiredService<AppDbContext>().Users.Select(u => u.TenantId).Distinct().ToListAsync());

        // Assert
        tenantsOfUsers.ShouldBe([scenario.Tenant.Id]);
    }

    [Test]
    public async Task job_holds_each_permission_in_its_tenant_and_cannot_manage_all_tenants()
    {
        // Arrange
        var scenario = await new TestingServiceScope().StartTenantScenarioAsync();
        Caller? caller = null;

        // Act
        await RunJobAsync(scenario.Tenant.Id, async services => caller = await services
            .GetRequiredService<IPermissionGuard>().GetCallerAsync(CancellationToken.None));

        // Assert
        var loaded = caller.ShouldNotBeNull();
        loaded.ShouldBe(new Caller.TenantJob(scenario.Tenant.Id));
        loaded.Permissions.ShouldBe(Permission.GetAll(), ignoreOrder: true);
        loaded.ManagesAllTenants.ShouldBeFalse();
    }

    [Test]
    public async Task request_that_a_job_sends_saves_under_the_name_of_the_job()
    {
        // Arrange
        var scenario = await new TestingServiceScope().StartTenantScenarioAsync();

        // Act
        await RunJobAsync(scenario.Tenant.Id, services => services.GetRequiredService<ISender>()
            .Send(new DeleteUser.Command(scenario.FirstUser.Id)));

        // Assert
        var deleted = await ReadAsync(read => read.AuditEntries
            .SingleAsync(e => e.RootId == scenario.FirstUser.Id && e.Kind == AuditEntryKind.Deleted));
        (deleted.RequestName, deleted.ActorSubject, deleted.ActorUserId, deleted.TenantId)
            .ShouldBe(("TestJob", null, null, scenario.Tenant.Id));
    }

    [Test]
    public async Task job_cannot_find_a_row_of_another_tenant()
    {
        // Arrange
        var scenario = await new TestingServiceScope().StartTenantScenarioAsync();
        var other = await new TestingServiceScope().StartTenantScenarioAsync();

        // Act
        var act = () => RunJobAsync(scenario.Tenant.Id, services => services.GetRequiredService<ISender>()
            .Send(new DeleteUser.Command(other.FirstUser.Id)));

        // Assert
        await act.ShouldThrowAsync<NotFoundException>();
        (await ReadAsync(read => read.Users.AnyAsync(u => u.Id == other.FirstUser.Id))).ShouldBeTrue();
    }

    [Test]
    public async Task job_that_changes_data_and_does_not_save_it_fails()
    {
        // Arrange
        var scenario = await new TestingServiceScope().StartTenantScenarioAsync();

        // Act
        var act = () => RunJobAsync(scenario.Tenant.Id, async services =>
        {
            var user = await services.GetRequiredService<AppDbContext>().Users
                .SingleAsync(u => u.Id == scenario.FirstUser.Id);
            user.UpdateFromIdp($"{user.FirstName}x", user.LastName, user.Email, user.Username);
        });

        // Assert
        (await act.ShouldThrowAsync<InvalidOperationException>()).Message.ShouldBe(
            "TestJob changed data, recorded an audit action, or queued a job, and did not call SaveChangesAsync. Save it.");
    }

    [Test]
    public async Task job_that_queues_a_job_of_its_tenant_and_saves_commits_it()
    {
        // Arrange
        var scenario = await new TestingServiceScope().StartTenantScenarioAsync();

        // Act
        await RunJobAsync(scenario.Tenant.Id, async services =>
        {
            var dbContext = services.GetRequiredService<AppDbContext>();
            dbContext.QueueJob(new TestJob(scenario.Tenant.Id, "from a job"));
            await dbContext.SaveChangesAsync();
        });

        // Assert
        (await TestJob.OfTenantAsync(scenario.Tenant.Id)).Count.ShouldBe(1);
    }

    [Test]
    public async Task job_cannot_queue_a_job_of_another_tenant()
    {
        // Arrange
        var scenario = await new TestingServiceScope().StartTenantScenarioAsync();
        var other = await new TestingServiceScope().SetupTenantAsync();

        // Act
        var act = () => RunJobAsync(scenario.Tenant.Id, services =>
        {
            services.GetRequiredService<AppDbContext>().QueueJob(new TestJob(other.Id, "other tenant"));
            return Task.CompletedTask;
        });

        // Assert
        (await act.ShouldThrowAsync<InvalidOperationException>()).Message.ShouldBe(
            $"The caller cannot see tenant {other.Id}, so it cannot queue TestJob for that tenant.");
    }

    [Test]
    public async Task job_puts_its_caller_on_the_current_span()
    {
        // Arrange
        var scenario = await new TestingServiceScope().StartTenantScenarioAsync();
        using var process = new Activity("process default").Start();

        // Act
        await RunJobAsync(scenario.Tenant.Id, _ => Task.CompletedTask);

        // Assert
        process.GetTagItem("app.caller.kind").ShouldBe("tenant_job");
        process.GetTagItem("app.caller.tenant_id").ShouldBe(scenario.Tenant.Id);
    }

    /// <summary>Runs <paramref name="work"/> as a job of the tenant, in a new DI scope with no request.</summary>
    private static async Task RunJobAsync(string tenantId, Func<IServiceProvider, Task> work)
    {
        await using var scope = TestFixture.BaseScopeFactory.CreateAsyncScope();
        var handler = new WorkHandler(scope.ServiceProvider.GetRequiredService<JobScope>(),
            () => work(scope.ServiceProvider));
        await handler.HandleAsync(new TestJob(tenantId, "work"), new JobContext { JobId = Guid.NewGuid(), Attempt = 1 },
            CancellationToken.None);
    }

    private sealed class WorkHandler(JobScope scope, Func<Task> work) : JobHandler<TestJob>(scope)
    {
        protected override Task RunAsync(TestJob job, JobContext context, CancellationToken cancellationToken)
            => work();
    }
}
