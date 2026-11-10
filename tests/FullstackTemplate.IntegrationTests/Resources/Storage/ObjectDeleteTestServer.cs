namespace FullstackTemplate.IntegrationTests.Resources.Storage;

using System.Net;
using System.Net.Sockets;
using Amazon.Runtime;
using Amazon.S3;
using Amazon.S3.Model;
using FullstackTemplate.Server.Domain.ObjectDeleteRequests;
using FullstackTemplate.Server.Domain.Tenants.Features;
using FullstackTemplate.Server.Domain.Users;
using FullstackTemplate.Server.Resources;
using FullstackTemplate.SharedTestHelpers.EntityGraphs;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;

/// <summary>
/// The Server of the object delete tests, with storage up or down, and reads of the real store. A runner claims each
/// due request of the database, also the requests of other tests, so the tests that run a runner do not run in
/// parallel with each other and look only at their own requests. Each Server has its own <see cref="RunClock"/>, so a
/// test sets the time of a run.
/// </summary>
public static class ObjectDeleteTestServer
{
    public const string RunnerTests = "object-delete-runner";

    /// <summary>
    /// A Server whose S3 client calls a port that refuses each connection, as a stopped storage does. The client does
    /// not try again, so a run over the requests of other tests stays short.
    /// </summary>
    public static ServiceProvider WithStorageDown(ObjectDeleteOptions? options = null, TimeSpan? clockStep = null)
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
            UseOptions(services, options, clockStep);
        });
    }

    public static ServiceProvider WithStorageUp(ObjectDeleteOptions? options = null)
        => TestFixture.BuildServiceProvider(services => UseOptions(services, options, null));

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
            UseOptions(services, options, null);
        });
        return new HungStorage(server, listener);
    }

    /// <summary>Runs each due request with storage up, so that a test starts with no request due.</summary>
    public static async Task RunAllDueAsync()
    {
        await using var storageUp = WithStorageUp();
        await RunDueAsync(storageUp, DateTimeOffset.UtcNow);
    }

    /// <summary>
    /// The time now, cut to the microseconds that Postgres keeps, so that a test can compare it with a stored time. Linux
    /// gives the time in ticks of 100 ns.
    /// </summary>
    public static DateTimeOffset DatabaseNow()
    {
        var now = DateTimeOffset.UtcNow;
        return now.AddTicks(-(now.Ticks % (TimeSpan.TicksPerMillisecond / 1000)));
    }

    /// <summary>Runs the due requests with the clock of the Server at <paramref name="now"/>.</summary>
    public static Task<IReadOnlyList<ObjectDeleteAttempt>> RunDueAsync(IServiceProvider server, DateTimeOffset now)
    {
        ((RunClock)server.GetRequiredService<TimeProvider>()).Now = now;
        return new TestingServiceScope(server).GetService<ObjectDeleteRunner>().RunDueAsync(CancellationToken.None);
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

    /// <summary>Deletes the tenant as its admin, on <paramref name="server"/> or on the default Server.</summary>
    public static Task DeleteTenantAsync(string tenantId, string adminIdentifier, IServiceProvider? server = null)
    {
        var scope = server is null ? new TestingServiceScope() : new TestingServiceScope(server);
        scope.SetUserWithDefaults(identifier: adminIdentifier);
        return scope.SendAsync(new DeleteTenant.Command(tenantId));
    }

    public static Task<List<ObjectDeleteRequest>> RequestsOfTenantAsync(string tenantId)
        => new TestingServiceScope().ExecuteDbContextAsync(db => db.Set<ObjectDeleteRequest>()
            .IgnoreQueryFilters([QueryFilterNames.Tenant])
            .Where(r => r.TenantId == tenantId)
            .OrderBy(r => r.Key)
            .ToListAsync());

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

    private static void UseOptions(IServiceCollection services, ObjectDeleteOptions? options, TimeSpan? clockStep)
    {
        services.RemoveAll<TimeProvider>();
        services.AddSingleton<TimeProvider>(new RunClock(clockStep ?? TimeSpan.Zero));
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

    /// <summary>A clock that a test sets. It moves on by <paramref name="step"/> after each read, as time passes in a run.</summary>
    private sealed class RunClock(TimeSpan step) : TimeProvider
    {
        public DateTimeOffset Now { get; set; } = DateTimeOffset.UtcNow;

        public override DateTimeOffset GetUtcNow()
        {
            var now = Now;
            Now += step;
            return now;
        }
    }
}
