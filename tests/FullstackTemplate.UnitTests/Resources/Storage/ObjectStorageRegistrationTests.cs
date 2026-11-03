namespace FullstackTemplate.UnitTests.Resources.Storage;

using FullstackTemplate.Server.Resources.Storage;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Shouldly;

public class ObjectStorageRegistrationTests
{
    [Test]
    public async Task a_start_with_no_bucket_fails_and_names_the_variable()
    {
        // Arrange
        await using var app = BuildWithBucket("");

        // Act
        var failure = Should.Throw<OptionsValidationException>(() => app.Services.GetRequiredService<IStartupValidator>().Validate());

        // Assert
        failure.Message.ShouldContain("ObjectStorage__BucketName");
    }

    [Test]
    public async Task a_start_with_a_bucket_passes()
    {
        // Arrange
        await using var app = BuildWithBucket("files");

        // Act and assert
        Should.NotThrow(() => app.Services.GetRequiredService<IStartupValidator>().Validate());
    }

    private static WebApplication BuildWithBucket(string bucketName)
    {
        var builder = WebApplication.CreateSlimBuilder();
        builder.Configuration[$"{ObjectStorageOptions.SectionName}:BucketName"] = bucketName;
        builder.AddObjectStorage();
        return builder.Build();
    }
}
