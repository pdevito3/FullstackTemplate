namespace FullstackTemplate.IntegrationTests.Resources.Storage;

using System.Net;
using System.Net.Sockets;
using Amazon.Runtime;
using Amazon.S3;
using Amazon.S3.Model;
using FullstackTemplate.Server.Domain.Tenants.Features;
using FullstackTemplate.Server.Domain.Users;
using FullstackTemplate.Server.Examples.Workshops;
using FullstackTemplate.Server.Examples.Workshops.Features;
using FullstackTemplate.Server.Exceptions;
using FullstackTemplate.Server.Resources.Storage;
using FullstackTemplate.SharedTestHelpers.EntityGraphs;
using FullstackTemplate.SharedTestHelpers.Examples.EntityGraphs;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Shouldly;

/// <summary>
/// Issue 422: the state after an object delete that fails after the commit. The objects are on the real store, and
/// the Server under test has an S3 client whose endpoint refuses each connection, as a stopped storage does.
/// </summary>
public class FailedObjectDeleteReproTests : TestBase
{
    [Test]
    public async Task an_attachment_delete_with_storage_down_leaves_the_object_and_a_second_delete_cannot_reach_it()
    {
        // Arrange
        var scope = new TestingServiceScope();
        await scope.StartGraph(out _)
            .AddTenant(out var tenant)
            .AddUser(out var admin, u => u.Role = UserRole.Admin().Value)
            .Under(tenant).AddVenue(out _).AddWorkshop(out var workshop).AddWorkshopAttachment(out var attachment)
            .BuildAsync();
        var key = attachment.File.StorageKey;
        await PutAsync(key);
        await using var storageDown = BuildServerWithStorageDown();
        var underTest = new TestingServiceScope(storageDown);
        underTest.SetUserWithDefaults(identifier: admin.Identifier);

        // Act
        await underTest.SendAsync(new DeleteWorkshopAttachment.Command(workshop.Id, attachment.Id));

        // Assert
        (await KeysUnderAsync(key)).ShouldBe([key], "The delete returned success, and the object stayed.");
        var row = await new TestingServiceScope().ExecuteDbContextAsync(db => db.Set<WorkshopAttachment>()
            .IgnoreQueryFilters().SingleAsync(a => a.Id == attachment.Id));
        row.IsDeleted.ShouldBeTrue("The row delete committed.");
        row.File.StorageKey.ShouldBe(key, "The soft-deleted row still names the object.");

        var storageBack = new TestingServiceScope();
        storageBack.SetUserWithDefaults(identifier: admin.Identifier);
        await Should.ThrowAsync<NotFoundException>(() =>
            storageBack.SendAsync(new DeleteWorkshopAttachment.Command(workshop.Id, attachment.Id)));
        (await KeysUnderAsync(key)).ShouldBe([key], "With storage back, a second delete gets 404, and the object stays.");
    }

    [Test]
    public async Task a_tenant_delete_with_storage_down_leaves_the_objects_and_a_second_delete_cannot_reach_them()
    {
        // Arrange
        var scope = new TestingServiceScope();
        await scope.StartGraph(out _)
            .AddTenant(out var tenant)
            .AddUser(out var admin, u => u.Role = UserRole.Admin().Value)
            .Under(tenant).AddVenue(out _).AddWorkshop(out _).AddWorkshopAttachment(out var attachment)
            .BuildAsync();
        var key = attachment.File.StorageKey;
        var orphan = $"tenants/{tenant.Id}/no-row/{Guid.NewGuid():N}";
        var prefix = $"tenants/{tenant.Id}/";
        await PutAsync(key);
        await PutAsync(orphan);
        await using var storageDown = BuildServerWithStorageDown();
        var underTest = new TestingServiceScope(storageDown);
        underTest.SetUserWithDefaults(identifier: admin.Identifier);

        // Act
        await underTest.SendAsync(new DeleteTenant.Command(tenant.Id));

        // Assert
        (await KeysUnderAsync(prefix)).Count.ShouldBe(2, "The tenant delete returned success, and the objects stayed.");
        var storageBack = new TestingServiceScope();
        storageBack.SetMachineClient(TestFixture.TrustedMachineClientId);
        await Should.ThrowAsync<NotFoundException>(() => storageBack.SendAsync(new DeleteTenant.Command(tenant.Id)));
        (await KeysUnderAsync(prefix)).Count.ShouldBe(2, "With storage back, a second delete gets 404, and the objects stay.");
    }

    [Test]
    public async Task control_with_storage_up_the_same_deletes_remove_the_objects()
    {
        // Arrange
        var scope = new TestingServiceScope();
        await scope.StartGraph(out _)
            .AddTenant(out var tenant)
            .AddUser(out var admin, u => u.Role = UserRole.Admin().Value)
            .Under(tenant).AddVenue(out _).AddWorkshop(out var workshop)
            .AddWorkshopAttachment(out var first)
            .Under(workshop).AddWorkshopAttachment(out var second)
            .BuildAsync();
        var firstKey = first.File.StorageKey;
        var prefix = $"tenants/{tenant.Id}/";
        await PutAsync(firstKey);
        await PutAsync(second.File.StorageKey);
        var underTest = new TestingServiceScope();
        underTest.SetUserWithDefaults(identifier: admin.Identifier);

        // Act and assert
        await underTest.SendAsync(new DeleteWorkshopAttachment.Command(workshop.Id, first.Id));
        (await KeysUnderAsync(firstKey)).ShouldBeEmpty();
        (await KeysUnderAsync(prefix)).Count.ShouldBe(1);

        await underTest.SendAsync(new DeleteTenant.Command(tenant.Id));
        (await KeysUnderAsync(prefix)).ShouldBeEmpty();
    }

    [Test]
    public async Task a_repeat_delete_of_objects_that_are_gone_is_harmless_on_the_real_store()
    {
        // Arrange
        var key = $"tenants/{Guid.NewGuid():N}/files/one";
        var prefix = $"tenants/{Guid.NewGuid():N}/";
        await PutAsync(key);
        await PutAsync($"{prefix}files/two");
        var storage = new TestingServiceScope().GetService<ObjectStorage>();

        // Act
        await storage.DeleteAsync(key, CancellationToken.None);
        await storage.DeleteAsync(key, CancellationToken.None);
        var first = await storage.DeleteUnderAsync(prefix, CancellationToken.None);
        var second = await storage.DeleteUnderAsync(prefix, CancellationToken.None);

        // Assert
        first.ShouldBe(1);
        second.ShouldBe(0);
        (await KeysUnderAsync(key)).ShouldBeEmpty();
        (await KeysUnderAsync(prefix)).ShouldBeEmpty();
    }

    private static ServiceProvider BuildServerWithStorageDown()
    {
        var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        var closedPort = ((IPEndPoint)listener.LocalEndpoint).Port;
        listener.Stop();

        return TestFixture.BuildServiceProvider(services =>
        {
            services.RemoveAll<IAmazonS3>();
            services.AddSingleton<IAmazonS3>(new AmazonS3Client(new BasicAWSCredentials("test", "test"), new AmazonS3Config
            {
                ServiceURL = $"http://127.0.0.1:{closedPort}",
                ForcePathStyle = true,
                AuthenticationRegion = "us-east-1",
            }));
        });
    }

    private static async Task PutAsync(string key)
    {
        using var client = TestFixture.Storage.CreateClient();
        await client.PutObjectAsync(new PutObjectRequest
        {
            BucketName = TestFixture.Storage.BucketName,
            Key = key,
            ContentBody = "bytes",
        });
    }

    private static async Task<List<string>> KeysUnderAsync(string prefix)
    {
        using var client = TestFixture.Storage.CreateClient();
        var page = await client.ListObjectsV2Async(new ListObjectsV2Request
        {
            BucketName = TestFixture.Storage.BucketName,
            Prefix = prefix,
        });
        return page.S3Objects?.Select(o => o.Key).ToList() ?? [];
    }
}
