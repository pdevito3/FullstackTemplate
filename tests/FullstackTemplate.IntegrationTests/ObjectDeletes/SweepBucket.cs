namespace FullstackTemplate.IntegrationTests.ObjectDeletes;

using Amazon.S3.Model;
using BackWave.Core;
using BackWave.Storage;
using FullstackTemplate.IntegrationTests.Jobs;
using FullstackTemplate.Server.Domain.Attachments;
using FullstackTemplate.Server.Domain.ObjectDeletes;
using FullstackTemplate.Server.Resources.Storage;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;
using Shouldly;

/// <summary>
/// A bucket of one test, and the Servers over it. The sweep lists the whole bucket, so in the bucket of all tests it
/// sees the objects of the other tests.
/// </summary>
public sealed class SweepBucket : IAsyncDisposable
{
    private readonly List<ServiceProvider> _servers = [];

    private SweepBucket()
    {
    }

    public string Name { get; } = $"sweep-{Guid.NewGuid():N}";

    public static TimeSpan Grace => new ObjectSweepOptions().GraceFor(new AttachmentOptions());

    public static async Task<SweepBucket> CreateAsync()
    {
        var bucket = new SweepBucket();
        using var client = TestFixture.Storage.CreateClient();
        await client.PutBucketAsync(bucket.Name);
        return bucket;
    }

    /// <summary>A Server that keeps its objects in this bucket, on the database of the tests or on another one.</summary>
    public ServiceProvider Server(string? connectionString = null) => Track(TestFixture.BuildServiceProvider(
        services => services.AddSingleton(Options.Create(new ObjectStorageOptions { BucketName = Name })),
        connectionString));

    /// <summary>Runs one sweep on a Server of this bucket whose clock reads <paramref name="now"/>.</summary>
    public async Task SweepAsync(DateTimeOffset now, string? connectionString = null)
    {
        var sweeper = Track(TestFixture.BuildServiceProvider(services =>
        {
            services.AddSingleton(Options.Create(new ObjectStorageOptions { BucketName = Name }));
            services.Replace(ServiceDescriptor.Singleton<TimeProvider>(new FixedClock(now)));
        }, connectionString));
        await using var harness = new JobHarness(sweeper, new RetryPolicy { MaxAttempts = 1 });

        var jobId = await harness.EnqueueAsync(new SweepObjects());
        await harness.RunDueAsync();

        (await harness.JobAsync(jobId)).State.ShouldBe(JobState.Succeeded, string.Join('\n', await harness.FailuresOfAsync(jobId)));
    }

    public async Task<StoredObject> PutAsync(string key)
    {
        using var client = TestFixture.Storage.CreateClient();
        await client.PutObjectAsync(new PutObjectRequest { BucketName = Name, Key = key, ContentBody = "bytes" });
        return (await ObjectsAsync()).Single(o => o.Key == key);
    }

    /// <summary>The objects of the bucket, read with a client of the test, in the order of their keys.</summary>
    public async Task<List<StoredObject>> ObjectsAsync()
    {
        using var client = TestFixture.Storage.CreateClient();
        var page = await client.ListObjectsV2Async(new ListObjectsV2Request { BucketName = Name });
        return [.. (page.S3Objects ?? []).Select(o => new StoredObject(o.Key, new DateTimeOffset(o.LastModified!.Value)))];
    }

    public async Task<List<string>> KeysAsync() => [.. (await ObjectsAsync()).Select(o => o.Key)];

    public async ValueTask DisposeAsync()
    {
        foreach (var server in _servers)
            await server.DisposeAsync();
        using var client = TestFixture.Storage.CreateClient();
        foreach (var key in await KeysAsync())
            await client.DeleteObjectAsync(Name, key);
        await client.DeleteBucketAsync(Name);
    }

    private ServiceProvider Track(ServiceProvider server)
    {
        _servers.Add(server);
        return server;
    }

    private sealed class FixedClock(DateTimeOffset now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => now;
    }
}
