namespace FullstackTemplate.Server.Resources.Storage;

using System.Diagnostics.CodeAnalysis;
using Amazon.S3;
using Amazon.S3.Model;
using Microsoft.Extensions.Options;

[SuppressMessage("ApiDesign", "RS0030:Do not use banned APIs",
    Justification = "ObjectStorage owns the lists, the deletes, and the signed URLs of the S3 client.")]
public sealed class ObjectStorage(IAmazonS3 s3, IOptions<ObjectStorageOptions> options)
{
    private string BucketName => options.Value.BucketName;

    /// <summary>Deletes the object. S3 gives success for a key that does not exist, so a second delete is a no-op.</summary>
    public async Task DeleteAsync(string key, CancellationToken cancellationToken)
        => await s3.DeleteObjectAsync(new DeleteObjectRequest { BucketName = BucketName, Key = key }, cancellationToken);

    /// <summary>
    /// Deletes the objects, at most 1000. S3 gives success for a key that does not exist, so a second delete is a no-op.
    /// </summary>
    /// <exception cref="DeleteObjectsException">A key failed to delete.</exception>
    public async Task DeleteAsync(IReadOnlyCollection<string> keys, CancellationToken cancellationToken)
        => await s3.DeleteObjectsAsync(new DeleteObjectsRequest
        {
            BucketName = BucketName,
            Objects = [.. keys.Select(key => new KeyVersion { Key = key })],
            Quiet = true,
        }, cancellationToken);

    /// <summary>
    /// Deletes each object whose key starts with the prefix, one page of keys at a time, and gives the number of keys.
    /// A key that fails to delete throws a <see cref="DeleteObjectsException"/>, and the keys of the pages after it stay.
    /// </summary>
    public async Task<int> DeleteUnderAsync(string prefix, CancellationToken cancellationToken)
    {
        var deleted = 0;
        string? continuationToken = null;
        do
        {
            var page = await ListPageAsync(prefix, continuationToken, cancellationToken);
            if (page.Objects.Count > 0)
                await DeleteAsync([.. page.Objects.Select(o => o.Key)], cancellationToken);
            deleted += page.Objects.Count;
            continuationToken = page.NextToken;
        } while (continuationToken is not null);

        return deleted;
    }

    /// <summary>
    /// One page of the objects whose key starts with the prefix, at most 1000, in the order of their keys. Give the
    /// <see cref="ObjectPage.NextToken"/> of a page to get the page after it.
    /// </summary>
    public async Task<ObjectPage> ListPageAsync(string prefix, string? continuationToken, CancellationToken cancellationToken)
    {
        var page = await s3.ListObjectsV2Async(new ListObjectsV2Request
        {
            BucketName = BucketName,
            Prefix = prefix,
            ContinuationToken = continuationToken,
        }, cancellationToken);
        return new ObjectPage(
            [.. (page.S3Objects ?? []).Select(o => new StoredObject(o.Key, new DateTimeOffset(o.LastModified
                ?? throw new InvalidOperationException($"S3 listed the object {o.Key} with no LastModified."))))],
            page.IsTruncated == true ? page.NextContinuationToken : null);
    }

    /// <summary>
    /// Signs a GET URL. The signature is local work with no call to S3, so the URL does not prove that the object
    /// exists. Anyone with the URL can read the object until it expires, so sign only after an access check.
    /// </summary>
    public Task<string> GetPresignedUrlAsync(PresignedGet get)
    {
        var request = new GetPreSignedUrlRequest
        {
            BucketName = BucketName,
            Key = get.Key,
            Verb = HttpVerb.GET,
            Expires = get.ExpiresAt.UtcDateTime,
            // The SDK signs an https URL unless it is told otherwise. A local emulator serves plain http.
            Protocol = s3.Config.ServiceURL is { } serviceUrl && serviceUrl.StartsWith("http://", StringComparison.OrdinalIgnoreCase)
                ? Protocol.HTTP
                : Protocol.HTTPS,
        };
        request.ResponseHeaderOverrides.ContentType = get.ContentType;
        request.ResponseHeaderOverrides.ContentDisposition = get.ContentDisposition;
        return s3.GetPreSignedURLAsync(request);
    }
}

/// <summary>An object in the bucket, and the time that S3 stored it, by the clock of S3.</summary>
public sealed record StoredObject(string Key, DateTimeOffset LastModified);

/// <summary>One page of a list of the bucket. <see cref="NextToken"/> is null on the last page.</summary>
public sealed record ObjectPage(IReadOnlyList<StoredObject> Objects, string? NextToken);
