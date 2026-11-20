namespace FullstackTemplate.Server.Resources.Storage;

using System.Diagnostics.CodeAnalysis;
using Amazon.S3;
using Amazon.S3.Model;
using Microsoft.Extensions.Options;

[SuppressMessage("ApiDesign", "RS0030:Do not use banned APIs",
    Justification = "ObjectWriter owns the put of the S3 client.")]
public sealed class ObjectWriter(IAmazonS3 s3, IOptions<ObjectStorageOptions> options)
{
    public async Task PutAsync(string key, Stream content, string contentType, CancellationToken cancellationToken)
        => await s3.PutObjectAsync(new PutObjectRequest
        {
            BucketName = options.Value.BucketName,
            Key = key,
            InputStream = content,
            ContentType = contentType,
            AutoCloseStream = false,
        }, cancellationToken);
}
