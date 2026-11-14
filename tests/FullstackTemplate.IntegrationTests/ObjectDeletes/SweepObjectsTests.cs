namespace FullstackTemplate.IntegrationTests.ObjectDeletes;

using BackWave.Monitor;
using BackWave.Storage;
using FullstackTemplate.IntegrationTests.Jobs;
using FullstackTemplate.Server.Databases;
using FullstackTemplate.Server.Domain;
using FullstackTemplate.Server.Domain.Attachments;
using FullstackTemplate.Server.Domain.ObjectDeletes;
using FullstackTemplate.Server.Domain.Tenants;
using FullstackTemplate.SharedTestHelpers.Fakes.Tenant;
using FullstackTemplate.SharedTestHelpers.Utilities;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;
using Shouldly;
using static ObjectDeleteTestServer;

/// <summary>
/// The sweep of the bucket on the real store, on a bucket of its own for each test. An add that stops between its put
/// and its save leaves an object that no row names, and so does a put that lands after the delete of its tenant.
/// </summary>
public class SweepObjectsTests : TestBase
{
    private static readonly TimeSpan Grace = SweepBucket.Grace;

    [Test]
    public async Task an_object_that_no_row_names_goes_after_the_grace()
    {
        // Arrange
        await using var bucket = await SweepBucket.CreateAsync();
        var orphan = await bucket.PutAsync($"{StoredFile.TenantPrefix(TestFixture.DefaultTenant.Id)}files/{Guid.NewGuid():N}");

        // Act
        await bucket.SweepAsync(orphan.LastModified + Grace + TimeSpan.FromSeconds(1));

        // Assert
        (await bucket.KeysAsync()).ShouldBeEmpty();
    }

    [Test]
    public async Task an_object_that_no_row_names_stays_up_to_the_grace_while_an_older_one_goes()
    {
        // Arrange
        await using var bucket = await SweepBucket.CreateAsync();
        var prefix = StoredFile.TenantPrefix(TestFixture.DefaultTenant.Id);
        var older = await bucket.PutAsync($"{prefix}files/older");
        // S3 keeps the LastModified of an object to the second
        await Task.Delay(TimeSpan.FromSeconds(1.5));
        var younger = await bucket.PutAsync($"{prefix}files/younger");

        // Act
        await bucket.SweepAsync(younger.LastModified + Grace);

        // Assert
        older.LastModified.ShouldBeLessThan(younger.LastModified);
        (await bucket.KeysAsync()).ShouldBe([younger.Key]);
    }

    [Test]
    public async Task a_put_that_lands_after_the_delete_of_the_objects_of_its_tenant_goes()
    {
        // Arrange
        await using var bucket = await SweepBucket.CreateAsync();
        var (tenantId, admin) = await AddTenantAsync();
        await DeleteTenantAsync(tenantId, admin);
        await using (var harness = Harness(bucket.Server()))
        {
            var jobId = await harness.EnqueueAsync((await StoredJobs.QueuedAsync<DeleteTenantObjects>(tenantId)).Single());
            await harness.RunDueAsync();
            (await harness.JobAsync(jobId)).State.ShouldBe(JobState.Succeeded);
        }

        var latePut = await bucket.PutAsync($"{StoredFile.TenantPrefix(tenantId)}files/{Guid.NewGuid():N}");

        // Act
        await bucket.SweepAsync(latePut.LastModified + Grace + TimeSpan.FromSeconds(1));

        // Assert
        (await bucket.KeysAsync()).ShouldBeEmpty();
    }

    [Test]
    public async Task an_object_whose_key_names_no_tenant_of_the_database_stays_while_an_orphan_of_a_tenant_goes()
    {
        // Arrange
        await using var bucket = await SweepBucket.CreateAsync();
        var ofAnotherDatabase = await bucket.PutAsync(
            $"{StoredFile.TenantPrefix(EntityId.New(Tenant.IdPrefix))}files/{Guid.NewGuid():N}");
        var underNoTenant = await bucket.PutAsync($"{StoredFile.TenantsPrefix}not-a-tenant/{Guid.NewGuid():N}");
        var orphan = await bucket.PutAsync($"{StoredFile.TenantPrefix(TestFixture.DefaultTenant.Id)}files/{Guid.NewGuid():N}");

        // Act
        await bucket.SweepAsync(orphan.LastModified + Grace + TimeSpan.FromSeconds(1));

        // Assert
        (await bucket.KeysAsync()).ShouldBe([ofAnotherDatabase.Key, underNoTenant.Key], ignoreOrder: true);
    }

    [Test]
    public async Task a_database_with_a_migration_that_the_server_does_not_have_skips_the_sweep()
    {
        // Arrange
        await using var bucket = await SweepBucket.CreateAsync();
        await using var database = new PostgresTestDatabase();
        await database.StartAsync();
        var server = bucket.Server(database.ConnectionString);
        await new MigrationHostedService(server.GetRequiredService<IServiceScopeFactory>(),
            server.GetRequiredService<IConfiguration>()).StartingAsync(CancellationToken.None);
        var prefix = StoredFile.TenantPrefix((await SaveTenantAsync(server)).Id);
        var orphanBefore = await bucket.PutAsync($"{prefix}files/before");
        await bucket.SweepAsync(orphanBefore.LastModified + Grace + TimeSpan.FromSeconds(1), database.ConnectionString);
        var keysBefore = await bucket.KeysAsync();
        await ExecuteAsync(database.ConnectionString, """
            INSERT INTO "__EFMigrationsHistory" (migration_id, product_version) VALUES ('29991231000000_OfANewerServer', '10.0.14')
            """);
        var orphanAfter = await bucket.PutAsync($"{prefix}files/after");

        // Act
        await bucket.SweepAsync(orphanAfter.LastModified + Grace + TimeSpan.FromSeconds(1), database.ConnectionString);

        // Assert
        keysBefore.ShouldBeEmpty("With only the migrations of this Server, the sweep runs.");
        (await bucket.KeysAsync()).ShouldBe([orphanAfter.Key], "A table of the newer Server can name the object.");
    }

    [Test]
    public async Task the_start_of_the_server_schedules_the_sweep_each_day_with_one_run_for_missed_ticks_and_no_overlap()
    {
        // Arrange
        await using var scope = TestFixture.BaseScopeFactory.CreateAsyncScope();

        // Act
        var schedules = await scope.ServiceProvider.GetRequiredService<BackWaveMonitor>().ListSchedulesAsync();

        // Assert
        schedules.Select(s => (s.ScheduleId, s.Cron, s.Queue, s.CatchUp, s.NoOverlap, s.Error)).ShouldBe(
            [("sweep-objects", "0 0 3 * * *", ObjectSweepOptions.Queue, CatchUpPolicy.Coalesce, true, (string?)null)]);
    }

    private static async Task<Tenant> SaveTenantAsync(IServiceProvider server)
    {
        var tenant = Tenant.Create(new FakeTenantForCreation().Generate());
        await using var scope = server.GetRequiredService<IServiceScopeFactory>().CreateSystemScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        dbContext.Tenants.Add(tenant);
        await dbContext.SaveChangesAsync();
        return tenant;
    }

    private static async Task ExecuteAsync(string connectionString, string sql)
    {
        await using var connection = new NpgsqlConnection(connectionString);
        await connection.OpenAsync();
        await using var command = new NpgsqlCommand(sql, connection);
        await command.ExecuteNonQueryAsync();
    }
}
