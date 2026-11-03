namespace FullstackTemplate.Bff.Tests.Integration;

using Microsoft.AspNetCore.Server.Kestrel.Core;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Shouldly;

public class UploadRequestLimitTests
{
    [Test]
    public void the_bff_keeps_the_default_body_limit_of_kestrel_that_the_largest_upload_rules_fit_in()
    {
        // Act
        var limit = BffApp.Instance.Services.GetRequiredService<IOptions<KestrelServerOptions>>().Value.Limits.MaxRequestBodySize;

        // Assert
        // UploadRules.LargestMaxSizeMegabytes of the Server fits in this limit. Change the two together.
        limit.ShouldBe(new KestrelServerLimits().MaxRequestBodySize);
    }
}
