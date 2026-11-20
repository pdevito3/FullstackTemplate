namespace FullstackTemplate.Server.Resources.Storage;

using System.Diagnostics.CodeAnalysis;
using Amazon.S3;

public static class ObjectStorageRegistration
{
    // The variable that the AWS SDKs read for a custom endpoint. The Floci integration of the AppHost sets it.
    private const string EndpointKey = "AWS_ENDPOINT_URL";
    private const string RegionKey = "AWS_DEFAULT_REGION";

    /// <summary>
    /// Adds the S3 client, <see cref="ObjectStorage"/>, and <see cref="ObjectWriter"/>. With no endpoint, the client
    /// calls S3 of AWS, and the SDK finds the region and the credentials in its default chain. An endpoint, such as Floci in a local run, gets path-style URLs, so
    /// that the host of a URL is the endpoint host and not a subdomain with the bucket name.
    /// </summary>
    [SuppressMessage("ApiDesign", "RS0030:Do not use banned APIs",
        Justification = "The registration makes the one S3 client of ObjectStorage and ObjectWriter.")]
    public static void AddObjectStorage(this WebApplicationBuilder builder)
    {
        builder.Services.AddOptions<ObjectStorageOptions>()
            .BindConfiguration(ObjectStorageOptions.SectionName)
            .Validate(o => !string.IsNullOrWhiteSpace(o.BucketName),
                $"{ObjectStorageOptions.SectionName}:BucketName is not set. Set the variable {ObjectStorageOptions.SectionName}__BucketName to the S3 bucket that holds the files.")
            .ValidateOnStart();
        builder.Services.AddSingleton<IAmazonS3>(_ =>
        {
            // The SDK signs with the clock of S3 when the clock of this Server is off. Without that correction S3
            // refuses a request more than 15 minutes off, and the sweep of the bucket needs that bound on its clock.
            var config = new AmazonS3Config { DisableClockSkewCorrection = true };
            if (builder.Configuration[EndpointKey] is { Length: > 0 } endpoint)
            {
                config.ServiceURL = endpoint;
                config.ForcePathStyle = true;
                config.AuthenticationRegion = builder.Configuration[RegionKey];
            }

            return new AmazonS3Client(config);
        });
        builder.Services.AddSingleton<ObjectStorage>();
        builder.Services.AddSingleton<ObjectWriter>();
    }
}
