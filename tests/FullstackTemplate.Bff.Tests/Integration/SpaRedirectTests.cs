namespace FullstackTemplate.Bff.Tests.Integration;

using System.Net;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.FileProviders;
using Shouldly;

public class SpaRedirectTests
{
    private const string IndexHtml = "<!doctype html><title>published spa</title>";

    private static readonly Lazy<string> WebRoot = new(() =>
    {
        var webRoot = Directory.CreateTempSubdirectory("bff-wwwroot-").FullName;
        File.WriteAllText(Path.Combine(webRoot, "index.html"), IndexHtml);
        return webRoot;
    });

    [Test]
    public async Task the_root_redirects_to_the_spa_dev_server()
    {
        // Arrange
        using var browser = BffApp.Instance.CreateBrowser();

        // Act
        var response = await browser.GetAsync("/");

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.Redirect);
        response.Headers.Location!.OriginalString.ShouldBe($"{BffApp.SpaDevServer}/");
    }

    [Test]
    [Arguments("/about?tab=team&x=1")]
    [Arguments("/assets/42")]
    [Arguments("/items/report.json?city=x")]
    [Arguments("/%5Cevil.example/x")]
    [Arguments("/caf%C3%A9?city=%E6%9D%B1%E4%BA%AC")]
    public async Task a_deep_link_redirects_to_the_same_path_and_query_on_the_spa_dev_server(string path)
    {
        // Arrange
        using var browser = BffApp.Instance.CreateBrowser();

        // Act
        var response = await browser.GetAsync(path);

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.Redirect);
        response.Headers.Location!.OriginalString.ShouldBe(BffApp.SpaDevServer + path);
        response.Headers.Location!.Authority.ShouldBe(new Uri(BffApp.SpaDevServer).Authority);
    }

    [Test]
    [Arguments("/bff")]
    [Arguments("/bff/nope")]
    [Arguments("/BFF/nope?x=1")]
    [Arguments("/bff/login/nope")]
    [Arguments("/health/nope")]
    [Arguments("/alive/nope")]
    [Arguments("/signin-oidc/nope")]
    [Arguments("/signout-callback-oidc/nope")]
    [Arguments("/api-docs")]
    [Arguments("/bffx")]
    [Arguments("/api%2Fx")]
    [Arguments("/signin-oidcx")]
    [Arguments("/bff%2Fx")]
    [Arguments("/BFF%2fuser")]
    public async Task an_unknown_path_that_vite_proxies_to_the_bff_keeps_its_404_on_a_dev_or_published_bff(string path)
    {
        // Arrange
        await using var published = StartPublished();

        foreach (var app in new[] { BffApp.Instance, published })
        {
            using var browser = app.CreateBrowser();

            // Act
            var response = await browser.GetAsync(path);

            // Assert
            response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
            response.Headers.Location.ShouldBeNull();
            (await response.Content.ReadAsStringAsync()).ShouldBeEmpty();
        }
    }

    [Test]
    public async Task an_unknown_api_path_goes_to_the_server_api()
    {
        // Arrange
        using var browser = BffApp.Instance.CreateBrowser();

        // Act
        var response = await browser.GetApiAsync("/api/v1/nope?x=1");

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        (await response.ReadEchoAsync()).Path.ShouldBe("/api/v1/nope");
    }

    [Test]
    public async Task the_oidc_callbacks_answer_as_on_a_published_bff()
    {
        // Arrange
        await using var published = StartPublished();
        using var devBrowser = BffApp.Instance.CreateBrowser();
        using var publishedBrowser = published.CreateBrowser();

        foreach (var path in new[] { "/signin-oidc", "/signin-oidc?code=x&state=y", "/signout-callback-oidc", "/signout-callback-oidc?state=y" })
        {
            // Act
            var dev = await devBrowser.GetAsync(path);
            var expected = await publishedBrowser.GetAsync(path);

            // Assert
            ((int)dev.StatusCode).ShouldBe((int)expected.StatusCode, path);
            dev.Headers.Location.ShouldBe(expected.Headers.Location, path);
        }
    }

    [Test]
    [Arguments("//evil.example/x")]
    [Arguments("//evil.example")]
    public async Task a_path_that_is_not_a_local_url_gets_404(string path)
    {
        // Arrange
        using var browser = BffApp.Instance.CreateBrowser();

        // Act
        var response = await browser.SendAsync(new HttpRequestMessage(HttpMethod.Get, new Uri(BffApp.Origin.GetLeftPart(UriPartial.Authority) + path)));

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
        response.Headers.Location.ShouldBeNull();
    }

    [Test]
    public async Task a_post_to_an_unknown_path_keeps_its_404()
    {
        // Arrange
        using var browser = BffApp.Instance.CreateBrowser();

        // Act
        var response = await browser.PostAsync("/about", content: null);

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
        response.Headers.Location.ShouldBeNull();
    }

    [Test]
    public async Task a_published_bff_serves_index_html_for_a_deep_link()
    {
        // Arrange
        await using var published = StartPublished();
        using var browser = published.CreateBrowser();

        // Act
        var response = await browser.GetAsync("/about?tab=team&x=1");

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        (await response.Content.ReadAsStringAsync()).ShouldBe(IndexHtml);
    }

    [Test]
    public async Task a_published_bff_keeps_the_404_of_an_unknown_bff_path()
    {
        // Arrange
        await using var published = StartPublished();
        using var browser = published.CreateBrowser();

        foreach (var path in new[] { "/bff", "/bff/nothing", "/BFF/nope?x=1", "/bff/login/nope" })
        {
            var request = new HttpRequestMessage(HttpMethod.Get, path);
            request.Headers.Add("Accept", "application/json");
            request.Headers.Add("X-CSRF", "1");

            // Act
            var response = await browser.SendAsync(request);

            // Assert
            ((int)response.StatusCode).ShouldBe(404, path);
            (await response.Content.ReadAsStringAsync()).ShouldBeEmpty(path);
        }
    }

    [Test]
    public async Task a_dev_or_published_bff_allows_no_cross_origin_request_from_the_spa_dev_server()
    {
        // Arrange
        await using var published = StartPublished();

        foreach (var app in new[] { BffApp.Instance, published })
        {
            using var browser = app.CreateBrowser();
            var preflight = new HttpRequestMessage(HttpMethod.Options, "/bff/user");
            preflight.Headers.Add("Origin", BffApp.SpaDevServer);
            preflight.Headers.Add("Access-Control-Request-Method", "GET");
            preflight.Headers.Add("Access-Control-Request-Headers", "x-csrf");
            var request = new HttpRequestMessage(HttpMethod.Get, "/bff/user");
            request.Headers.Add("Origin", BffApp.SpaDevServer);
            request.Headers.Add("X-CSRF", "1");

            // Act
            var preflightResponse = await browser.SendAsync(preflight);
            var response = await browser.SendAsync(request);

            // Assert
            preflightResponse.Headers.Contains("Access-Control-Allow-Origin").ShouldBeFalse();
            response.StatusCode.ShouldBe(HttpStatusCode.NoContent);
            response.Headers.Contains("Access-Control-Allow-Origin").ShouldBeFalse();
            response.Headers.Contains("Access-Control-Allow-Credentials").ShouldBeFalse();
        }
    }

    [Test]
    [Arguments("POST", "/bff/user", "GET")]
    [Arguments("PUT", "/bff/user", "GET")]
    [Arguments("GET", "/bff/backchannel", "POST")]
    [Arguments("POST", "/bff/logout", "GET")]
    public async Task a_wrong_method_on_a_bff_endpoint_gets_405_on_a_dev_or_published_bff(string method, string path, string allow)
    {
        // Arrange
        await using var published = StartPublished();

        foreach (var app in new[] { BffApp.Instance, published })
        {
            using var browser = app.CreateBrowser();
            var request = new HttpRequestMessage(new HttpMethod(method), path);
            request.Headers.Add("X-CSRF", "1");

            // Act
            var response = await browser.SendAsync(request);

            // Assert
            response.StatusCode.ShouldBe(HttpStatusCode.MethodNotAllowed);
            response.Content.Headers.Allow.ShouldBe([allow]);
        }
    }

    [Test]
    [Arguments("/")]
    [Arguments("/about?tab=team&x=1")]
    public async Task a_head_of_a_page_redirects_as_a_get_does(string path)
    {
        // Arrange
        using var browser = BffApp.Instance.CreateBrowser();

        var get = await browser.GetAsync(path);

        // Act
        var head = await browser.SendAsync(new HttpRequestMessage(HttpMethod.Head, path));

        // Assert
        head.StatusCode.ShouldBe(HttpStatusCode.Redirect);
        head.Headers.Location.ShouldBe(get.Headers.Location);
    }

    [Test]
    public async Task a_published_bff_answers_a_head_of_a_page_as_a_get()
    {
        // Arrange
        await using var published = StartPublished();
        using var browser = published.CreateBrowser();

        // Act
        var response = await browser.SendAsync(new HttpRequestMessage(HttpMethod.Head, "/about?tab=team&x=1"));

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        response.Content.Headers.ContentType!.MediaType.ShouldBe("text/html");
        response.Content.Headers.ContentLength.ShouldBe(IndexHtml.Length);
    }

    [Test]
    [Arguments("GET", "/assets/missing.js")]
    [Arguments("GET", "/about/logo.png")]
    [Arguments("POST", "/about")]
    [Arguments("DELETE", "/")]
    public async Task a_published_bff_keeps_the_404_of_a_missing_file_or_a_page_that_is_not_a_get(string method, string path)
    {
        // Arrange
        await using var published = StartPublished();
        using var browser = published.CreateBrowser();

        // Act
        var response = await browser.SendAsync(new HttpRequestMessage(new HttpMethod(method), path));

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
        (await response.Content.ReadAsStringAsync()).ShouldBeEmpty();
    }

    /// <summary>
    /// A BFF with no <c>services:webfrontend</c>, which serves the SPA from a web root with an <c>index.html</c>.
    /// </summary>
    private static BffApp StartPublished() =>
        BffApp.StartAnotherInstance(
            new Dictionary<string, string?> { ["services:webfrontend:http:0"] = "" },
            services => services.Configure<StaticFileOptions>(options => options.FileProvider = new PhysicalFileProvider(WebRoot.Value)));
}
