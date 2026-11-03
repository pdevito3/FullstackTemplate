namespace FullstackTemplate.FunctionalTests.FunctionalTests.Attachments;

using FullstackTemplate.Server.Domain.Attachments;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.AspNetCore.Server.Kestrel.Core;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Shouldly;

public class UploadRequestLimitTests : TestBase
{
    // The boundaries and the part headers of a form with one file. A file name of 255 characters, percent-encoded,
    // takes less than 3 KB.
    private const long FormBytesAroundTheFile = 64 * 1024;

    [Test]
    public void a_form_with_a_file_of_the_largest_size_that_rules_can_allow_fits_in_the_request_limits()
    {
        // Arrange
        var services = TestingWebApplicationFactory.Instance.Services;
        var serverBodyLimit = services.GetRequiredService<IOptions<KestrelServerOptions>>().Value.Limits.MaxRequestBodySize;
        var formLimit = services.GetRequiredService<IOptions<FormOptions>>().Value.MultipartBodyLengthLimit;
        // The BFF keeps the default limit of Kestrel. UploadRequestLimitTests in FullstackTemplate.Bff.Tests checks it.
        var bffBodyLimit = new KestrelServerLimits().MaxRequestBodySize;

        // Act
        var form = new UploadRules(UploadRules.LargestMaxSizeMegabytes, FileType.Pdf).MaxSizeBytes + FormBytesAroundTheFile;

        // Assert
        form.ShouldBeLessThanOrEqualTo(serverBodyLimit.ShouldNotBeNull());
        form.ShouldBeLessThanOrEqualTo(bffBodyLimit.ShouldNotBeNull());
        form.ShouldBeLessThanOrEqualTo(formLimit);
    }
}
