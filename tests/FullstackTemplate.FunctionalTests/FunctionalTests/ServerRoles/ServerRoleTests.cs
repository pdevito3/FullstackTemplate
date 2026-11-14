namespace FullstackTemplate.FunctionalTests.FunctionalTests.ServerRoles;

using System.Net;
using Amazon.S3.Model;
using BackWave;
using BackWave.Jobs;
using BackWave.Monitor;
using BackWave.Postgres;
using BackWave.Storage;
using FullstackTemplate.Server.Databases;
using FullstackTemplate.Server.Domain;
using FullstackTemplate.Server.Domain.ObjectDeletes;
using FullstackTemplate.Server.Domain.Tenants;
using FullstackTemplate.Server.Resources.Entrypoints;
using FullstackTemplate.Server.Resources.Entrypoints.Jobs;
using FullstackTemplate.Server.Resources.Extensions;
using FullstackTemplate.SharedTestHelpers.Utilities;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;
using Shouldly;
using TestUtilities;

/// <summary>
/// <c>Server:Role</c> splits one Server image into processes that serve requests and processes that run jobs. Each
/// test runs the built Server in processes of its own, on a database of its own.
/// </summary>
public class ServerRoleTests
{
    // The worker groups poll each second, so a pump would claim a due job well inside this time
    private static readonly TimeSpan ThreePolls = TimeSpan.FromSeconds(3);

    [Test]
    public async Task a_worker_answers_the_health_probes_and_maps_no_api_no_mcp_and_no_jobs_dashboard()
    {
        // Arrange
        await using var database = await NewDatabaseAsync();
        await using var worker = await ServerProcess.StartAsync(ServerRole.Worker, database.ConnectionString);

        // Act
        using var health = await worker.Client.GetRequestAsync(ApiRoutes.Health);
        using var alive = await worker.Client.GetRequestAsync(ApiRoutes.Alive);
        using var api = await worker.Client.GetRequestAsync(ApiRoutes.Users.GetCurrent);
        using var mcp = await worker.Client.PostAsync(EntrypointRegistration.McpPath, null);
        using var dashboard = await worker.Client.GetRequestAsync(JobsDashboard.Path);

        // Assert
        health.StatusCode.ShouldBe(HttpStatusCode.OK);
        alive.StatusCode.ShouldBe(HttpStatusCode.OK);
        api.StatusCode.ShouldBe(HttpStatusCode.NotFound);
        mcp.StatusCode.ShouldBe(HttpStatusCode.NotFound);
        dashboard.StatusCode.ShouldBe(HttpStatusCode.NotFound);
        worker.Output.ShouldContain(line => LogsRole(line, ServerRole.Worker));
    }

    [Test]
    public async Task a_worker_whose_worker_group_stops_on_a_fault_of_the_job_store_is_unhealthy()
    {
        // Arrange
        await using var database = await NewDatabaseAsync();
        await using var worker = await ServerProcess.StartAsync(ServerRole.Worker, database.ConnectionString);

        // Act
        // The next poll of each worker group finds no job table, and the group stops
        await ExecuteAsync(database, $"ALTER SCHEMA {BackWaveSchema.SchemaName} RENAME TO gone");
        var health = await HealthOnceUnhealthyAsync(worker);

        // Assert
        health.ShouldBe(HttpStatusCode.ServiceUnavailable);
    }

    [Test]
    public async Task an_api_server_runs_no_job_and_a_worker_on_its_database_runs_the_job()
    {
        // Arrange
        await using var database = await NewDatabaseAsync();
        await using var api = await ServerProcess.StartAsync(ServerRole.Api, database.ConnectionString);
        var tenantId = EntityId.New(Tenant.IdPrefix);
        var key = $"tenants/{tenantId}/files/{Guid.NewGuid():N}";
        await PutAsync(key);
        await using var store = new PostgresJobStore(new PostgresStoreOptions
        {
            ConnectionString = database.ConnectionString,
            SchemaName = BackWaveSchema.SchemaName,
        });
        var registry = TestingWebApplicationFactory.Instance.Services.GetRequiredService<JobRegistry>();
        var monitor = new BackWaveMonitor(store, registry);
        // As a request of the Api server does, when its save queues the job
        var jobId = await new BackWaveClient(store, registry).EnqueueAsync(new DeleteTenantObjects(tenantId), DateTimeOffset.UtcNow);

        // Act
        using var apiRequest = await api.Client.GetRequestAsync(ApiRoutes.Users.GetCurrent);
        await Task.Delay(ThreePolls);
        var withApiOnly = await monitor.GetJobAsync(jobId);
        await using var worker = await ServerProcess.StartAsync(ServerRole.Worker, database.ConnectionString);
        var withWorker = await DoneAsync(monitor, jobId);

        // Assert
        apiRequest.StatusCode.ShouldBe(HttpStatusCode.Unauthorized, "The Api server maps the API.");
        withApiOnly.ShouldNotBeNull().ShouldSatisfyAllConditions(
            j => j.State.ShouldBe(JobState.Scheduled),
            j => j.Attempt.ShouldBe(0, "No pump of the Api server claimed the job."));
        withWorker.State.ShouldBe(JobState.Succeeded);
        (await KeysUnderAsync(key)).ShouldBeEmpty();
        api.Output.ShouldContain(line => LogsRole(line, ServerRole.Api));
    }

    [Test]
    public async Task each_job_handler_resolves_without_the_entrypoints_as_in_a_worker()
    {
        // Arrange
        // The Worker composition of Program: the core services, with no AddEntrypoints. The settings come from the
        // environment of the test server.
        var builder = WebApplication.CreateBuilder();
        builder.ConfigureServices(ServerRole.Worker);
        await using var services = builder.Services.BuildServiceProvider(validateScopes: true);
        var jobTypes = services.GetRequiredService<JobRegistry>().Registrations.Select(r => r.JobType).ToList();
        await using var scope = services.CreateAsyncScope();

        // Act
        var handlers = jobTypes.Select(type => scope.ServiceProvider.GetService(typeof(IJobHandler<>).MakeGenericType(type))).ToList();

        // Assert
        jobTypes.ShouldNotBeEmpty();
        handlers.ShouldAllBe(handler => handler != null);
    }

    /// <summary>True for the JSON log line in which the Server names its role at startup.</summary>
    private static bool LogsRole(string line, ServerRole role) => line.Contains($$"""
        "ServerRole":"{{role}}"
        """, StringComparison.Ordinal);

    private static async Task<PostgresTestDatabase> NewDatabaseAsync()
    {
        var database = new PostgresTestDatabase();
        await database.StartAsync();
        return database;
    }

    private static async Task ExecuteAsync(PostgresTestDatabase database, string sql)
    {
        await using var connection = new NpgsqlConnection(database.ConnectionString);
        await connection.OpenAsync();
        await using var command = new NpgsqlCommand(sql, connection);
        await command.ExecuteNonQueryAsync();
    }

    /// <summary>The status of <c>/health</c> once it is not 200, or the last status after 30 seconds.</summary>
    private static async Task<HttpStatusCode> HealthOnceUnhealthyAsync(ServerProcess server)
    {
        var deadline = DateTimeOffset.UtcNow.AddSeconds(30);
        while (true)
        {
            using var health = await server.Client.GetRequestAsync(ApiRoutes.Health);
            if (health.StatusCode != HttpStatusCode.OK || DateTimeOffset.UtcNow > deadline)
                return health.StatusCode;
            await Task.Delay(TimeSpan.FromMilliseconds(200));
        }
    }

    private static async Task<JobSnapshot> DoneAsync(BackWaveMonitor monitor, Guid jobId)
    {
        var deadline = DateTimeOffset.UtcNow.AddSeconds(30);
        while (true)
        {
            var job = (await monitor.GetJobAsync(jobId)).ShouldNotBeNull();
            if (job.State is JobState.Succeeded or JobState.DeadLettered || DateTimeOffset.UtcNow > deadline)
                return job;
            await Task.Delay(TimeSpan.FromMilliseconds(200));
        }
    }

    private static async Task PutAsync(string key)
    {
        var storage = TestingWebApplicationFactory.Instance.Storage;
        using var client = storage.CreateClient();
        await client.PutObjectAsync(new PutObjectRequest { BucketName = storage.BucketName, Key = key, ContentBody = "x" });
    }

    private static async Task<List<string>> KeysUnderAsync(string prefix)
    {
        var storage = TestingWebApplicationFactory.Instance.Storage;
        using var client = storage.CreateClient();
        var page = await client.ListObjectsV2Async(new ListObjectsV2Request { BucketName = storage.BucketName, Prefix = prefix });
        return page.S3Objects?.Select(o => o.Key).ToList() ?? [];
    }
}
