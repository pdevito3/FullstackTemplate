namespace FullstackTemplate.IntegrationTests.ObjectDeletes;

using System.Net;
using System.Net.Sockets;
using Amazon.Runtime;
using Amazon.S3;
using Amazon.S3.Model;
using FullstackTemplate.IntegrationTests.Jobs;
using FullstackTemplate.Server.Domain.ObjectDeletes;
using FullstackTemplate.Server.Domain.Tenants.Features;
using FullstackTemplate.Server.Domain.Users;
using FullstackTemplate.SharedTestHelpers.EntityGraphs;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;

/// <summary>
/// The Servers of the object delete tests, with storage up, down, or hung, and reads of the real store. A test saves
/// the delete on the default Server, reads the job that the save committed, and runs it in a
/// <see cref="JobHarness"/> over one of these Servers.
/// </summary>
public static class ObjectDeleteTestServer
{
    /// <summary>The worker group retry of the object deletes, and the harness that runs the jobs with it.</summary>
    public static JobHarness Harness(IServiceProvider server, ObjectDeleteOptions? options = null)
        => new(server, (options ?? new ObjectDeleteOptions()).WorkerGroup().RetryPolicy);

    /// <summary>
    /// A Server whose S3 client calls a port that refuses each connection, as a stopped storage does. The client does
    /// not try again, so each attempt stays short.
    /// </summary>
    public static ServiceProvider WithStorageDown(ObjectDeleteOptions? options = null)
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
                MaxErrorRetry = 0,
            }));
            UseOptions(services, options);
        });
    }

    public static ServiceProvider WithStorageUp(ObjectDeleteOptions? options = null)
        => TestFixture.BuildServiceProvider(services => UseOptions(services, options));

    /// <summary>
    /// A Server whose S3 client calls a port that takes each connection and never answers, as a paused storage does.
    /// The client waits 10 minutes, so only <see cref="ObjectDeleteOptions.AttemptTimeout"/> ends an attempt soon.
    /// </summary>
    public static HungStorage WithStorageHung(ObjectDeleteOptions options)
    {
        var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        var port = ((IPEndPoint)listener.LocalEndpoint).Port;

        var server = TestFixture.BuildServiceProvider(services =>
        {
            services.RemoveAll<IAmazonS3>();
            services.AddSingleton<IAmazonS3>(new AmazonS3Client(new BasicAWSCredentials("test", "test"), new AmazonS3Config
            {
                ServiceURL = $"http://127.0.0.1:{port}",
                ForcePathStyle = true,
                AuthenticationRegion = "us-east-1",
                MaxErrorRetry = 0,
                Timeout = TimeSpan.FromMinutes(10),
            }));
            UseOptions(services, options);
        });
        return new HungStorage(server, listener);
    }

    /// <summary>Adds a tenant and an admin of the tenant, who can delete it.</summary>
    public static async Task<(string TenantId, string AdminIdentifier)> AddTenantAsync()
    {
        await new TestingServiceScope().StartGraph(out _)
            .AddTenant(out var tenant)
            .AddUser(out var admin, u => u.Role = UserRole.Admin().Value)
            .BuildAsync();
        return (tenant.Id, admin.Identifier);
    }

    /// <summary>Deletes the tenant as its admin. The save queues the job that deletes the objects of the tenant.</summary>
    public static Task DeleteTenantAsync(string tenantId, string adminIdentifier)
    {
        var scope = new TestingServiceScope();
        scope.SetUserWithDefaults(identifier: adminIdentifier);
        return scope.SendAsync(new DeleteTenant.Command(tenantId));
    }

    public static async Task PutAsync(string key)
    {
        using var client = TestFixture.Storage.CreateClient();
        await client.PutObjectAsync(new PutObjectRequest
        {
            BucketName = TestFixture.Storage.BucketName,
            Key = key,
            ContentBody = "bytes",
        });
    }

    public static async Task<List<string>> KeysUnderAsync(string prefix)
    {
        using var client = TestFixture.Storage.CreateClient();
        var page = await client.ListObjectsV2Async(new ListObjectsV2Request
        {
            BucketName = TestFixture.Storage.BucketName,
            Prefix = prefix,
        });
        return page.S3Objects?.Select(o => o.Key).ToList() ?? [];
    }

    private static void UseOptions(IServiceCollection services, ObjectDeleteOptions? options)
    {
        if (options is null)
            return;

        services.RemoveAll<IOptions<ObjectDeleteOptions>>();
        services.AddSingleton(Options.Create(options));
    }

    public sealed class HungStorage(ServiceProvider server, TcpListener listener) : IAsyncDisposable
    {
        public ServiceProvider Server => server;

        public async ValueTask DisposeAsync()
        {
            listener.Stop();
            await server.DisposeAsync();
        }
    }
}
