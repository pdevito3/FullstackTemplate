namespace FullstackTemplate.FunctionalTests.FunctionalTests.Attachments;

using FullstackTemplate.Server.Databases;
using FullstackTemplate.Server.Domain.ObjectDeleteRequests;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Shouldly;

public class ObjectDeleteWorkerStartTests : TestBase
{
    [Test]
    public void the_host_starts_the_object_delete_worker_after_the_migration()
    {
        // Act
        var started = TestingWebApplicationFactory.Instance.Services.GetServices<IHostedService>().ToList();

        // Assert
        var migration = started.FindIndex(s => s is MigrationHostedService);
        var worker = started.FindIndex(s => s is ObjectDeleteWorker);
        migration.ShouldBeGreaterThanOrEqualTo(0);
        worker.ShouldBeGreaterThan(migration,
            "The host starts its services in order, so a worker before the migration could read a missing table.");
    }
}
