namespace FullstackTemplate.Bff.Tests.Unit;

using FullstackTemplate.Bff.Management;
using Microsoft.Extensions.Configuration;
using Shouldly;

public class SpaOriginTests
{
    [Test]
    [Arguments("http://localhost:5173")]
    [Arguments("http://localhost:5173/")]
    [Arguments("http://localhost:5173/some/path?q=1")]
    public void a_return_url_goes_to_the_origin_of_the_dev_server(string devServer)
    {
        var spaOrigin = Read(new() { ["services:webfrontend:http:0"] = devServer });

        spaOrigin.Resolve("/assets/42?tab=x").ShouldBe("http://localhost:5173/assets/42?tab=x");
    }

    [Test]
    public void the_https_endpoint_of_the_dev_server_wins()
    {
        var spaOrigin = Read(new()
        {
            ["services:webfrontend:https:0"] = "https://localhost:5174",
            ["services:webfrontend:http:0"] = "http://localhost:5173",
        });

        spaOrigin.Resolve("/").ShouldBe("https://localhost:5174/");
    }

    [Test]
    [Arguments(null)]
    [Arguments("")]
    public void without_a_dev_server_a_return_url_stays_local(string? devServer)
    {
        var spaOrigin = Read(new() { ["services:webfrontend:http:0"] = devServer });

        spaOrigin.Resolve("/assets/42?tab=x").ShouldBe("/assets/42?tab=x");
    }

    [Test]
    [Arguments("localhost:5173")]
    [Arguments("ftp://localhost:5173")]
    [Arguments("http://user:secret@localhost:5173")]
    public void a_dev_server_that_is_not_an_http_url_stops_the_startup(string devServer)
    {
        Should.Throw<InvalidOperationException>(() => Read(new() { ["services:webfrontend:http:0"] = devServer }));
    }

    [Test]
    [Arguments("@evil.example")]
    [Arguments("//evil.example")]
    [Arguments("https://evil.example")]
    public void a_value_that_is_not_a_local_url_throws(string returnUrl)
    {
        var spaOrigin = Read(new() { ["services:webfrontend:http:0"] = "http://localhost:5173" });

        Should.Throw<ArgumentException>(() => spaOrigin.Resolve(returnUrl));
    }

    private static SpaOrigin Read(Dictionary<string, string?> settings) =>
        SpaOrigin.FromConfiguration(new ConfigurationBuilder().AddInMemoryCollection(settings).Build());
}
