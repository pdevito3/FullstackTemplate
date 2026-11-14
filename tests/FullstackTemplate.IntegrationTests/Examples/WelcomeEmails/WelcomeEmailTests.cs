namespace FullstackTemplate.IntegrationTests.Examples.WelcomeEmails;

using BackWave.Core;
using BackWave.Storage;
using FullstackTemplate.IntegrationTests.Databases;
using FullstackTemplate.IntegrationTests.Jobs;
using FullstackTemplate.IntegrationTests.Resources.Email;
using FullstackTemplate.Server.Domain.Users;
using FullstackTemplate.Server.Domain.Users.Features;
using FullstackTemplate.Server.Examples.WelcomeEmails;
using FullstackTemplate.Server.Exceptions;
using FullstackTemplate.Server.Resources.Email;
using FullstackTemplate.SharedTestHelpers.EntityGraphs;
using FullstackTemplate.SharedTestHelpers.Fakes.User;
using FullstackTemplate.SharedTestHelpers.Utilities;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Shouldly;

/// <summary>
/// The save that adds a user commits the welcome email job. The test reads the job from the database and runs it
/// through BackWave with a sender that keeps each email.
/// </summary>
public class WelcomeEmailTests : TestBase
{
    [Test]
    public async Task each_save_that_adds_a_user_commits_one_welcome_email_job_for_the_user()
    {
        // Arrange
        var (tenantId, admin) = await AddTenantAsync();

        // Act
        var added = await AsUser(admin.Identifier).SendAsync(new AddUser.Command(new FakeUserForCreationDto().Generate()));

        // Assert
        (await StoredJobs.QueuedAsync<SendWelcomeEmail>(tenantId))
            .ShouldBe([new SendWelcomeEmail(tenantId, admin.Id), new SendWelcomeEmail(tenantId, added.Id)]);
    }

    [Test]
    public async Task the_failed_save_of_the_loser_of_a_user_add_race_commits_no_welcome_email_job()
    {
        // Arrange
        var (tenantId, admin) = await AddTenantAsync();
        var pause = PauseAtQuery.After("users.add.identifier_in_use");
        await using var services = pause.BuildServices();
        var underTest = AsUser(admin.Identifier, services);
        var rival = AsUser(admin.Identifier);
        var dto = new FakeUserForCreationDto().Generate();

        // Act
        var add = underTest.SendAsync(new AddUser.Command(dto));
        await pause.ReachedAsync();
        var winner = await rival.SendAsync(new AddUser.Command(dto));
        pause.Release();

        // Assert
        await Should.ThrowAsync<ConflictException>(add);
        (await StoredJobs.QueuedAsync<SendWelcomeEmail>(tenantId)).Select(j => j.UserId).ShouldBe([admin.Id, winner.Id]);
    }

    [Test]
    public async Task the_job_sends_one_welcome_email_to_the_user_with_a_key_from_the_user()
    {
        // Arrange
        var (tenantId, admin) = await AddTenantAsync();
        var job = (await StoredJobs.QueuedAsync<SendWelcomeEmail>(tenantId)).ShouldHaveSingleItem();
        var emails = new RecordingEmailSender();
        await using var server = ServerWith(emails);
        await using var harness = new JobHarness(server, RetryPolicy.Default);

        // Act
        var jobId = await harness.EnqueueAsync(job);
        await harness.RunDueAsync();

        // Assert
        (await harness.JobAsync(jobId)).State.ShouldBe(JobState.Succeeded);
        emails.Sent.ShouldHaveSingleItem().ShouldSatisfyAllConditions(
            e => e.To.ShouldBe(admin.Email),
            e => e.Subject.ShouldBe("Welcome"),
            e => e.TextBody.ShouldContain(admin.FirstName),
            e => e.IdempotencyKey.ShouldBe($"welcome-email/{admin.Id}"));
    }

    [Test]
    public async Task a_run_again_after_a_lost_reply_gives_the_provider_the_same_key()
    {
        // Arrange
        var (tenantId, admin) = await AddTenantAsync();
        var job = (await StoredJobs.QueuedAsync<SendWelcomeEmail>(tenantId)).ShouldHaveSingleItem();
        var emails = new RecordingEmailSender(lostReplies: 1);
        await using var server = ServerWith(emails);
        await using var harness = new JobHarness(server, RetryPolicy.Default);

        // Act
        var jobId = await harness.EnqueueAsync(job);
        await harness.RunDueAsync();
        await harness.AdvanceAsync(RetryPolicy.Default.Backoff(1));

        // Assert
        (await harness.JobAsync(jobId)).ShouldSatisfyAllConditions(
            j => j.State.ShouldBe(JobState.Succeeded),
            j => j.Attempt.ShouldBe(2));
        emails.Sent.Select(e => e.IdempotencyKey).ShouldBe([$"welcome-email/{admin.Id}", $"welcome-email/{admin.Id}"],
            "The provider sends each key one time, so the user gets one email.");
    }

    [Test]
    public async Task a_user_deleted_before_the_job_runs_gets_no_email_and_the_job_succeeds()
    {
        // Arrange
        var (tenantId, admin) = await AddTenantAsync();
        var scope = AsUser(admin.Identifier);
        var added = await scope.SendAsync(new AddUser.Command(new FakeUserForCreationDto().Generate()));
        await scope.SendAsync(new DeleteUser.Command(added.Id));
        var job = (await StoredJobs.QueuedAsync<SendWelcomeEmail>(tenantId)).Single(j => j.UserId == added.Id);
        var emails = new RecordingEmailSender();
        await using var server = ServerWith(emails);
        await using var harness = new JobHarness(server, RetryPolicy.Default);

        // Act
        var jobId = await harness.EnqueueAsync(job);
        await harness.RunDueAsync();

        // Assert
        (await harness.JobAsync(jobId)).State.ShouldBe(JobState.Succeeded);
        emails.Sent.ShouldBeEmpty();
    }

    /// <summary>Adds a tenant and an admin of the tenant. The save also commits the welcome email job of the admin.</summary>
    private static async Task<(string TenantId, User Admin)> AddTenantAsync()
    {
        await new TestingServiceScope().StartGraph(out _)
            .AddTenant(out var tenant)
            .AddUser(out var admin, u => u.Role = UserRole.Admin().Value)
            .BuildAsync();
        return (tenant.Id, admin);
    }

    private static TestingServiceScope AsUser(string identifier, IServiceProvider? services = null)
    {
        var scope = services is null ? new TestingServiceScope() : new TestingServiceScope(services);
        scope.SetUserWithDefaults(identifier: identifier);
        return scope;
    }

    private static ServiceProvider ServerWith(IEmailSender emails) => TestFixture.BuildServiceProvider(services =>
    {
        services.RemoveAll<IEmailSender>();
        services.AddSingleton(emails);
    });
}
