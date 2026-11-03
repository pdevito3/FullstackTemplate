namespace FullstackTemplate.Server.Resources.Storage;

public sealed record ObjectStorageOptions
{
    public const string SectionName = "ObjectStorage";

    /// <summary>The S3 bucket that holds the files of all tenants. The AppHost sets it for a local run.</summary>
    public string BucketName { get; init; } = default!;
}
