namespace FullstackTemplate.SharedTestHelpers.Utilities;

using Amazon.Runtime;
using Amazon.S3;
using Amazon.S3.Model;
using DotNet.Testcontainers.Builders;
using DotNet.Testcontainers.Containers;
using FullstackTemplate.AppHost;

/// <summary>
/// Gives a test run its own empty bucket on a shared Floci container, the S3 emulator of the AppHost.
/// </summary>
/// <remarks>
/// Local runs reuse one container, as <see cref="PostgresTestDatabase"/> does. Each run creates a new bucket, so
/// the objects of earlier runs and other test projects never collide with this run. Floci checks each signature,
/// so a presigned URL that the server signs wrong fails the test.
/// </remarks>
public sealed class FlociTestStorage : IAsyncDisposable
{
    private const int S3Port = 4566;
    private const string Region = "us-east-1";
    private const string AccessKey = "test";
    private const string SecretKey = "test";

    private static readonly bool ReuseContainer = Environment.GetEnvironmentVariable("CI") is null;

    private readonly IContainer _container = new ContainerBuilder($"floci/floci:{Config.FlociImageTag}")
        .WithReuse(ReuseContainer)
        .WithLabel("fullstacktemplate.reuse", "test-floci")
        .WithEnvironment(Config.FlociValidateSignaturesVariable, "true")
        .WithPortBinding(S3Port, assignRandomHostPort: true)
        .WithWaitStrategy(Wait.ForUnixContainer().UntilHttpRequestIsSucceeded(request => request
            .ForPort(S3Port)
            .ForPath("/_floci/health")))
        .Build();

    /// <summary>The bucket of this run.</summary>
    public string BucketName { get; } = $"test-{Guid.NewGuid():N}";

    /// <summary>The URL of the emulator, as the browser and the server see it.</summary>
    public string ServiceUrl { get; private set; } = null!;

    public async Task StartAsync()
    {
        await using (await AcquireStartLockAsync())
        {
            await _container.StartAsync();
        }
        ServiceUrl = $"http://{_container.Hostname}:{_container.GetMappedPublicPort(S3Port)}";

        using var s3 = CreateClient();
        await s3.PutBucketAsync(BucketName);
    }

    /// <summary>
    /// Sets the variables that the AppHost gives the server: the endpoint and the region that
    /// <c>AddObjectStorage</c> reads, the credentials of the default chain of the SDK, and the bucket.
    /// </summary>
    public void SetEnvironmentVariables()
    {
        Environment.SetEnvironmentVariable("AWS_ENDPOINT_URL", ServiceUrl);
        Environment.SetEnvironmentVariable("AWS_DEFAULT_REGION", Region);
        Environment.SetEnvironmentVariable("AWS_ACCESS_KEY_ID", AccessKey);
        Environment.SetEnvironmentVariable("AWS_SECRET_ACCESS_KEY", SecretKey);
        Environment.SetEnvironmentVariable("ObjectStorage__BucketName", BucketName);
    }

    /// <summary>A client of the emulator, for a test that reads or writes the bucket directly.</summary>
    public AmazonS3Client CreateClient()
        => new(new BasicAWSCredentials(AccessKey, SecretKey), new AmazonS3Config
        {
            ServiceURL = ServiceUrl,
            ForcePathStyle = true,
            AuthenticationRegion = Region,
        });

    public async ValueTask DisposeAsync()
    {
        if (!ReuseContainer)
        {
            await _container.DisposeAsync();
            return;
        }

        // The container stays up for the next run. Delete only the bucket of this run.
        using var s3 = CreateClient();
        ListObjectsV2Response page;
        do
        {
            page = await s3.ListObjectsV2Async(new ListObjectsV2Request { BucketName = BucketName });
            foreach (var item in page.S3Objects ?? [])
                await s3.DeleteObjectAsync(BucketName, item.Key);
        } while (page.IsTruncated == true);
        await s3.DeleteBucketAsync(BucketName);
    }

    // The test projects start at the same time. See PostgresTestDatabase.AcquireStartLockAsync.
    private static async Task<FileStream> AcquireStartLockAsync()
    {
        var path = Path.Combine(Path.GetTempPath(), "fullstacktemplate-test-floci.lock");
        var deadline = DateTime.UtcNow.AddMinutes(5);
        while (true)
        {
            try
            {
                return new FileStream(path, FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None);
            }
            catch (IOException) when (DateTime.UtcNow < deadline)
            {
                await Task.Delay(TimeSpan.FromMilliseconds(200));
            }
        }
    }
}
