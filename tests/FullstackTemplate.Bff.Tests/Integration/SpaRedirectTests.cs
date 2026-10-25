namespace FullstackTemplate.Bff.Tests.Integration;

using System.Net;
using Shouldly;

public class SpaRedirectTests
{
    [Test]
    public async Task the_root_redirects_to_the_spa_dev_server()
    {
        using var browser = BffApp.Instance.CreateBrowser();

        var response = await browser.GetAsync("/");

        response.StatusCode.ShouldBe(HttpStatusCode.Redirect);
        response.Headers.Location!.OriginalString.ShouldBe($"{BffApp.SpaDevServer}/");
    }
}
