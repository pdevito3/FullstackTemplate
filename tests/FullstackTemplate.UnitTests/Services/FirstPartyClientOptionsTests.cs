namespace FullstackTemplate.UnitTests.Services;

using FullstackTemplate.Server.Services;
using Microsoft.Extensions.Configuration;
using Shouldly;

public sealed class FirstPartyClientOptionsTests
{
    [Test]
    public void swagger_client_is_first_party_when_it_is_set()
    {
        // Act
        var options = FromConfiguration(swaggerClientId: "swagger");

        // Assert
        options.Contains("app").ShouldBeTrue();
        options.Contains("swagger").ShouldBeTrue();
    }

    [Test]
    [Arguments("")]
    [Arguments("   ")]
    public void empty_swagger_client_id_is_not_first_party(string swaggerClientId)
    {
        // Act
        var options = FromConfiguration(swaggerClientId);

        // Assert
        options.Contains(swaggerClientId).ShouldBeFalse();
        options.ClientIds.ShouldBe(["app"]);
    }

    [Test]
    public void missing_auth_client_id_stops_startup()
    {
        // Arrange
        var configuration = new ConfigurationBuilder().Build();

        // Act
        var act = () => FirstPartyClientOptions.FromConfiguration(configuration);

        // Assert
        act.ShouldThrow<InvalidOperationException>().Message.ShouldBe("Auth:ClientId configuration is required.");
    }

    private static FirstPartyClientOptions FromConfiguration(string swaggerClientId)
        => FirstPartyClientOptions.FromConfiguration(new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Auth:ClientId"] = "app",
                ["Swagger:ClientId"] = swaggerClientId,
            })
            .Build());
}
