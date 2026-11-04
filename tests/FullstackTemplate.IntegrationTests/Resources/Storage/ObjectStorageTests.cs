namespace FullstackTemplate.IntegrationTests.Resources.Storage;

using Amazon.S3.Model;
using FullstackTemplate.Server.Resources.Storage;
using Shouldly;

public class ObjectStorageTests : TestBase
{
    [Test]
    public async Task a_delete_under_a_prefix_deletes_each_object_on_each_page_and_keeps_the_objects_outside_it()
    {
        // Arrange - S3 lists and deletes 1,000 keys at a time, so 1,001 objects need two pages
        var prefix = $"tenants/{Guid.NewGuid():N}/";
        var outside = $"tenants/{Guid.NewGuid():N}/kept";
        var keys = Enumerable.Range(0, 1001).Select(i => $"{prefix}files/{i}").Append(outside).ToList();
        var storage = new TestingServiceScope().GetService<ObjectStorage>();
        await Parallel.ForEachAsync(keys, new ParallelOptions { MaxDegreeOfParallelism = 16 }, async (key, ct) =>
        {
            using var content = new MemoryStream([1]);
            await storage.PutAsync(key, content, "application/octet-stream", ct);
        });
        (await FirstPageUnderAsync(prefix)).IsTruncated.ShouldBe(true);

        // Act
        var deleted = await storage.DeleteUnderAsync(prefix, CancellationToken.None);

        // Assert
        deleted.ShouldBe(1001);
        (await KeysUnderAsync(prefix)).ShouldBeEmpty();
        (await KeysUnderAsync(outside)).ShouldBe([outside]);
    }

    [Test]
    public async Task a_delete_under_a_prefix_with_no_objects_deletes_none()
    {
        // Arrange
        var storage = new TestingServiceScope().GetService<ObjectStorage>();

        // Act
        var deleted = await storage.DeleteUnderAsync($"tenants/{Guid.NewGuid():N}/", CancellationToken.None);

        // Assert
        deleted.ShouldBe(0);
    }

    private static async Task<List<string>> KeysUnderAsync(string prefix)
        => (await FirstPageUnderAsync(prefix)).S3Objects?.Select(o => o.Key).ToList() ?? [];

    private static async Task<ListObjectsV2Response> FirstPageUnderAsync(string prefix)
    {
        using var client = TestFixture.Storage.CreateClient();
        return await client.ListObjectsV2Async(new ListObjectsV2Request
        {
            BucketName = TestFixture.Storage.BucketName,
            Prefix = prefix,
        });
    }
}
