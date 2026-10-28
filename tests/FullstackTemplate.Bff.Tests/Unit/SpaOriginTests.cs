namespace FullstackTemplate.Bff.Tests.Unit;

using FullstackTemplate.Bff.Management;
using Microsoft.Extensions.Configuration;
using Shouldly;

public class SpaOriginTests
{
    [Test]
    [Arguments("http://localhost:24710")]
    [Arguments("http://localhost:24710/")]
    [Arguments("http://localhost:24710/some/path?q=1")]
    public void a_return_url_goes_to_the_origin_of_the_dev_server(string devServer)
    {
        var spaOrigin = Read(new() { ["services:webfrontend:http:0"] = devServer });

        spaOrigin.Resolve(Local("/assets/42?tab=x")).ShouldBe("http://localhost:24710/assets/42?tab=x");
    }

    [Test]
    public void the_https_endpoint_of_the_dev_server_wins()
    {
        var spaOrigin = Read(new()
        {
            ["services:webfrontend:https:0"] = "https://localhost:5174",
            ["services:webfrontend:http:0"] = "http://localhost:24710",
        });

        spaOrigin.Resolve(LocalUrl.Root).ShouldBe("https://localhost:5174/");
    }

    [Test]
    [Arguments(null)]
    [Arguments("")]
    public void without_a_dev_server_a_return_url_stays_local(string? devServer)
    {
        var spaOrigin = Read(new() { ["services:webfrontend:http:0"] = devServer });

        spaOrigin.Resolve(Local("/assets/42?tab=x")).ShouldBe("/assets/42?tab=x");
    }

    [Test]
    [Arguments("localhost:24710")]
    [Arguments("ftp://localhost:24710")]
    [Arguments("http://user:secret@localhost:24710")]
    public void a_dev_server_that_is_not_an_http_url_stops_the_startup(string devServer)
    {
        Should.Throw<InvalidOperationException>(() => Read(new() { ["services:webfrontend:http:0"] = devServer }));
    }

    private static LocalUrl Local(string value) =>
        LocalUrl.TryParse(value, out var localUrl) ? localUrl : throw new ArgumentException($"'{value}' is not a local URL.");

    private static SpaOrigin Read(Dictionary<string, string?> settings) =>
        SpaOrigin.FromConfiguration(new ConfigurationBuilder().AddInMemoryCollection(settings).Build());
}
