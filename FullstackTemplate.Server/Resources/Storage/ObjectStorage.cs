namespace FullstackTemplate.Server.Resources.Storage;

using Amazon.S3;
using Amazon.S3.Model;
using Microsoft.Extensions.Options;

public sealed class ObjectStorage(IAmazonS3 s3, IOptions<ObjectStorageOptions> options)
{
    private string BucketName => options.Value.BucketName;

    public async Task PutAsync(string key, Stream content, string contentType, CancellationToken cancellationToken)
        => await s3.PutObjectAsync(new PutObjectRequest
        {
            BucketName = BucketName,
            Key = key,
            InputStream = content,
            ContentType = contentType,
            // The caller owns the stream
            AutoCloseStream = false,
        }, cancellationToken);

    /// <summary>Deletes the object. S3 gives success for a key that does not exist, so a second delete is a no-op.</summary>
    public async Task DeleteAsync(string key, CancellationToken cancellationToken)
        => await s3.DeleteObjectAsync(new DeleteObjectRequest { BucketName = BucketName, Key = key }, cancellationToken);

    /// <summary>
    /// Deletes each object whose key starts with the prefix, one page of keys at a time, and gives the number of keys.
    /// A key that fails to delete throws a <see cref="DeleteObjectsException"/>, and the keys of the pages after it stay.
    /// </summary>
    public async Task<int> DeleteUnderAsync(string prefix, CancellationToken cancellationToken)
    {
        var list = new ListObjectsV2Request { BucketName = BucketName, Prefix = prefix };
        var deleted = 0;
        ListObjectsV2Response page;
        do
        {
            page = await s3.ListObjectsV2Async(list, cancellationToken);
            if (page.S3Objects is { Count: > 0 } objects)
            {
                await s3.DeleteObjectsAsync(new DeleteObjectsRequest
                {
                    BucketName = BucketName,
                    Objects = [.. objects.Select(o => new KeyVersion { Key = o.Key })],
                    Quiet = true,
                }, cancellationToken);
                deleted += objects.Count;
            }

            list.ContinuationToken = page.NextContinuationToken;
        } while (page.IsTruncated == true);

        return deleted;
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
