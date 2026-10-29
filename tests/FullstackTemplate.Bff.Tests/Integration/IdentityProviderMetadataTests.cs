namespace FullstackTemplate.Bff.Tests.Integration;

using System.Net;
using FullstackTemplate.Bff.Authentication;
using FullstackTemplate.Bff.Tests.Fakes;
using Shouldly;

/// <summary>
/// The checks on the discovery document and the signing keys.
/// </summary>
public class IdentityProviderMetadataTests
{
    [Test]
    public async Task a_discovery_issuer_that_is_not_the_authority_stops_the_bff_at_startup()
    {
        // Arrange
        await using var provider = await FakeIdentityProvider.StartAsync(new FakeProviderOptions { MismatchedDiscoveryIssuer = true });

        // Act & Assert
        var exception = Should.Throw<Exception>(() => BffApp.StartAnotherInstance(identityProvider: provider));

        // IdentityModel hides the message of the check as PII
        AllMessages(exception).ShouldContain("IDX20810");
    }

    [Test]
    public async Task the_first_login_after_a_signing_key_rollover_signs_in()
    {
        // Arrange
        await using var provider = await FakeIdentityProvider.StartAsync();
        await using var bff = BffApp.StartAnotherInstance(
            new Dictionary<string, string?> { [IdentityProviderBackchannel.MetadataRefreshIntervalKey] = "00:00:01" },
            identityProvider: provider);
        using (var before = bff.CreateBrowser())
            (await BffApp.SignInAsync(before, providerQuery: $"test_sub=user-{Guid.NewGuid():N}")).StatusCode.ShouldBe(HttpStatusCode.Redirect);

        provider.RotateSigningKey();
        // The shortest time between two refreshes of the keys
        await Task.Delay(TimeSpan.FromSeconds(1.5));

        using var browser = bff.CreateBrowser();

        // Act
        var callback = await BffApp.SignInAsync(browser, providerQuery: $"test_sub=user-{Guid.NewGuid():N}");

        // Assert
        callback.StatusCode.ShouldBe(HttpStatusCode.Redirect);
        (await browser.GetApiAsync("/bff/user")).StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    private static string AllMessages(Exception exception) =>
        exception.InnerException is null ? exception.Message : $"{exception.Message} {AllMessages(exception.InnerException)}";
}
