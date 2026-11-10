namespace FullstackTemplate.IntegrationTests.Resources.Storage;

using System.Net;
using System.Net.Sockets;
using Amazon.Runtime;
using Amazon.S3;
using Amazon.S3.Model;
using FullstackTemplate.Server.Domain.ObjectDeleteRequests;
using FullstackTemplate.Server.Resources;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;

/// <summary>
/// The Server of the object delete tests, with storage up or down, and reads of the real store. A runner claims each
/// due request of the database, also the requests of other tests, so the tests that run a runner do not run in
/// parallel with each other and look only at their own requests.
/// </summary>
public static class ObjectDeleteTestServer
{
    public const string RunnerTests = "object-delete-runner";

    /// <summary>
    /// A Server whose S3 client calls a port that refuses each connection, as a stopped storage does. The client does
    /// not try again, so a run over the requests of other tests stays short.
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

    public static Task<IReadOnlyList<ObjectDeleteAttempt>> RunDueAsync(IServiceProvider server, DateTimeOffset now)
        => new TestingServiceScope(server).GetService<ObjectDeleteRunner>().RunDueAsync(now, CancellationToken.None);

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

    private static void UseOptions(IServiceCollection services, ObjectDeleteOptions? options)
    {
        if (options is null)
            return;

        services.RemoveAll<IOptions<ObjectDeleteOptions>>();
        services.AddSingleton(Options.Create(options));
    }
}
