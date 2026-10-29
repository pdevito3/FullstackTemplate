namespace FullstackTemplate.Bff.Tests.Unit;

using System.Text.Json;
using FullstackTemplate.Bff.Features;
using Microsoft.AspNetCore.Authentication.OpenIdConnect;
using Microsoft.IdentityModel.Protocols.OpenIdConnect;
using Shouldly;

public class ProviderFeatureDecisionTests
{
    private const string Issuer = "https://idp.example";

    /// <summary>
    /// The discovery fields of each feature, with the value that lists it.
    /// </summary>
    private static readonly Dictionary<string, object> AllFeatures = new()
    {
        ["revocation_endpoint"] = $"{Issuer}/revoke",
        ["authorization_response_iss_parameter_supported"] = true,
        ["backchannel_logout_supported"] = true,
        ["end_session_endpoint"] = $"{Issuer}/endsession",
        ["pushed_authorization_request_endpoint"] = $"{Issuer}/par",
    };

    [Test]
    public void auto_uses_each_feature_that_discovery_lists_with_no_warning()
    {
        // Act
        var (features, warnings) = Decide(Discovery(AllFeatures), new ProviderFeatureOptions());

        // Assert
        features.ShouldBe(new ActiveFeatures(Issuer, Revocation: true, IssuerParameterRule.Required, BackChannelLogout: true,
            EndSession: true, PushedAuthorization: true, ClientAuthenticationMethod.Basic));
        warnings.ShouldBeEmpty();
    }

    [Test]
    public void auto_falls_back_for_each_missing_feature_with_one_warning_each()
    {
        // Act
        var (features, warnings) = Decide(Discovery([]), new ProviderFeatureOptions());

        // Assert
        // The back-channel endpoint stays mapped, and a present iss must still match
        features.ShouldBe(new ActiveFeatures(Issuer, Revocation: false, IssuerParameterRule.IfPresent, BackChannelLogout: true,
            EndSession: false, PushedAuthorization: false, ClientAuthenticationMethod.Basic));
        warnings.Select(w => (w.Feature, w.DiscoveryField)).ShouldBe(
        [
            ("Revocation", "revocation_endpoint"),
            ("IssuerParameter", "authorization_response_iss_parameter_supported"),
            ("BackChannelLogout", "backchannel_logout_supported"),
            ("EndSession", "end_session_endpoint"),
            ("PushedAuthorization", "pushed_authorization_request_endpoint"),
        ]);
    }

    [Test]
    public void a_logout_endpoint_setting_turns_on_end_session_when_discovery_lists_none()
    {
        // Act
        var (features, warnings) = ProviderFeatureDecision.Decide(
            Discovery([]), new ProviderFeatureOptions(), ClientAuthenticationMethod.Auto, $"{Issuer}/logout");

        // Assert
        features.EndSession.ShouldBeTrue();
        warnings.ShouldNotContain(w => w.Feature == "EndSession");
    }

    [Test]
    [Arguments("authorization_response_iss_parameter_supported", false)]
    [Arguments("backchannel_logout_supported", false)]
    public void a_false_flag_does_not_list_the_feature(string field, bool value)
    {
        // Act
        var (_, warnings) = Decide(Discovery(new() { [field] = value }), new ProviderFeatureOptions());

        // Assert
        warnings.ShouldContain(w => w.DiscoveryField == field);
    }

    [Test]
    [MethodDataSource(nameof(RequireSettings))]
    public void require_stops_the_bff_when_discovery_does_not_list_the_feature(string feature, Action<ProviderFeatureOptions> require)
    {
        // Arrange
        var options = new ProviderFeatureOptions();
        require(options);

        // Act & Assert
        var exception = Should.Throw<ProviderFeatureException>(() => Decide(Discovery([]), options));

        exception.Message.ShouldContain($"Auth:Features:{feature} is Require");
    }

    [Test]
    [MethodDataSource(nameof(RequireSettings))]
    public void require_uses_the_feature_that_discovery_lists_with_no_warning(string feature, Action<ProviderFeatureOptions> require)
    {
        // Arrange
        var options = new ProviderFeatureOptions();
        require(options);

        // Act
        var (_, warnings) = Decide(Discovery(AllFeatures), options);

        // Assert
        warnings.ShouldNotContain(w => w.Feature == feature);
    }

    public static IEnumerable<Func<(string, Action<ProviderFeatureOptions>)>> RequireSettings()
    {
        yield return () => ("Revocation", o => o.Revocation = FeatureMode.Require);
        yield return () => ("IssuerParameter", o => o.IssuerParameter = FeatureMode.Require);
        yield return () => ("BackChannelLogout", o => o.BackChannelLogout = FeatureMode.Require);
        yield return () => ("EndSession", o => o.EndSession = FeatureMode.Require);
        yield return () => ("PushedAuthorization", o => o.PushedAuthorization = FeatureMode.Require);
    }

    [Test]
    public void disable_never_uses_a_feature_that_discovery_lists_and_gives_no_warning()
    {
        // Arrange
        var options = new ProviderFeatureOptions
        {
            Revocation = FeatureMode.Disable,
            IssuerParameter = FeatureMode.Disable,
            BackChannelLogout = FeatureMode.Disable,
            EndSession = FeatureMode.Disable,
            PushedAuthorization = FeatureMode.Disable,
        };

        // Act
        var (features, warnings) = Decide(Discovery(AllFeatures), options);

        // Assert
        features.ShouldBe(new ActiveFeatures(Issuer, Revocation: false, IssuerParameterRule.Disabled, BackChannelLogout: false,
            EndSession: false, PushedAuthorization: false, ClientAuthenticationMethod.Basic));
        warnings.ShouldBeEmpty();
    }

    [Test]
    [Arguments(FeatureMode.Auto)]
    [Arguments(FeatureMode.Require)]
    public void dpop_is_active_when_the_key_is_set_and_discovery_lists_its_algorithm(FeatureMode mode)
    {
        // Arrange
        var fields = new Dictionary<string, object>(AllFeatures) { ["dpop_signing_alg_values_supported"] = new[] { "RS256", "ES256" } };

        // Act
        var (features, warnings) = ProviderFeatureDecision.Decide(Discovery(fields), new ProviderFeatureOptions { DPoP = mode },
            ClientAuthenticationMethod.Auto, dpopKeyAlgorithm: "ES256");

        // Assert
        features.DPoP.ShouldBeTrue();
        warnings.ShouldBeEmpty();
    }

    [Test]
    public void dpop_disable_is_inactive_even_with_a_key_and_a_listing_provider()
    {
        // Arrange
        var fields = new Dictionary<string, object>(AllFeatures) { ["dpop_signing_alg_values_supported"] = new[] { "ES256" } };

        // Act
        var (features, warnings) = ProviderFeatureDecision.Decide(Discovery(fields), new ProviderFeatureOptions(),
            ClientAuthenticationMethod.Auto, dpopKeyAlgorithm: "ES256");

        // Assert
        features.DPoP.ShouldBeFalse();
        warnings.ShouldBeEmpty();
    }

    [Test]
    [Arguments(null)]
    [Arguments("RS256")]
    public void dpop_auto_falls_back_to_bearer_with_one_warning_when_the_key_or_the_listing_is_missing(string? keyAlgorithm)
    {
        // Arrange
        var fields = new Dictionary<string, object>(AllFeatures) { ["dpop_signing_alg_values_supported"] = new[] { "ES256" } };

        // Act
        var (features, warnings) = ProviderFeatureDecision.Decide(Discovery(fields), new ProviderFeatureOptions { DPoP = FeatureMode.Auto },
            ClientAuthenticationMethod.Auto, dpopKeyAlgorithm: keyAlgorithm);

        // Assert
        features.DPoP.ShouldBeFalse();
        warnings.Single().Feature.ShouldBe("DPoP");
    }

    [Test]
    public void dpop_auto_falls_back_to_bearer_when_discovery_does_not_list_dpop()
    {
        // Act
        var (features, warnings) = ProviderFeatureDecision.Decide(Discovery(AllFeatures), new ProviderFeatureOptions { DPoP = FeatureMode.Auto },
            ClientAuthenticationMethod.Auto, dpopKeyAlgorithm: "ES256");

        // Assert
        features.DPoP.ShouldBeFalse();
        warnings.Single().DiscoveryField.ShouldStartWith("dpop_signing_alg_values_supported");
    }

    [Test]
    [Arguments(null, true)]
    [Arguments("ES256", false)]
    [Arguments("RS256", true)]
    public void dpop_require_stops_the_bff_without_a_key_or_a_listed_algorithm(string? keyAlgorithm, bool listsDPoP)
    {
        // Arrange
        var fields = new Dictionary<string, object>(AllFeatures);
        if (listsDPoP)
            fields["dpop_signing_alg_values_supported"] = new[] { "ES256" };
        if (keyAlgorithm == "ES256")
            fields["dpop_signing_alg_values_supported"] = new[] { "RS256" };

        // Act & Assert
        Should.Throw<ProviderFeatureException>(() => ProviderFeatureDecision.Decide(Discovery(fields),
            new ProviderFeatureOptions { DPoP = FeatureMode.Require }, ClientAuthenticationMethod.Auto, dpopKeyAlgorithm: keyAlgorithm));
    }

    [Test]
    [Arguments(FeatureMode.Auto, PushedAuthorizationBehavior.UseIfAvailable)]
    [Arguments(FeatureMode.Require, PushedAuthorizationBehavior.Require)]
    [Arguments(FeatureMode.Disable, PushedAuthorizationBehavior.Disable)]
    public void each_pushed_authorization_value_maps_to_the_handler_behavior(FeatureMode mode, PushedAuthorizationBehavior expected)
    {
        // Act & Assert
        ProviderFeatureOptions.ToPushedAuthorizationBehavior(mode).ShouldBe(expected);
    }

    [Test]
    [Arguments(ClientAuthenticationMethod.Auto, new[] { "client_secret_basic", "client_secret_post" }, ClientAuthenticationMethod.Basic, false)]
    [Arguments(ClientAuthenticationMethod.Auto, new[] { "client_secret_basic" }, ClientAuthenticationMethod.Basic, false)]
    [Arguments(ClientAuthenticationMethod.Auto, new string[0], ClientAuthenticationMethod.Basic, false)]
    [Arguments(ClientAuthenticationMethod.Auto, new[] { "client_secret_post" }, ClientAuthenticationMethod.Post, true)]
    [Arguments(ClientAuthenticationMethod.Auto, new[] { "private_key_jwt" }, ClientAuthenticationMethod.Basic, true)]
    [Arguments(ClientAuthenticationMethod.Basic, new[] { "client_secret_post" }, ClientAuthenticationMethod.Basic, false)]
    [Arguments(ClientAuthenticationMethod.Post, new[] { "client_secret_basic", "client_secret_post" }, ClientAuthenticationMethod.Post, false)]
    public void each_client_authentication_case(ClientAuthenticationMethod setting, string[] supported,
        ClientAuthenticationMethod expected, bool warns)
    {
        // Arrange
        var fields = new Dictionary<string, object>(AllFeatures);
        if (supported.Length > 0)
            fields["token_endpoint_auth_methods_supported"] = supported;

        // Act
        var (features, warnings) = ProviderFeatureDecision.Decide(Discovery(fields), new ProviderFeatureOptions(), setting);

        // Assert
        features.ClientAuthentication.ShouldBe(expected);
        warnings.Any(w => w.Feature == "ClientAuthentication").ShouldBe(warns);
    }

    private static (ActiveFeatures, IReadOnlyList<FeatureWarning>) Decide(OpenIdConnectConfiguration discovery, ProviderFeatureOptions options) =>
        ProviderFeatureDecision.Decide(discovery, options, ClientAuthenticationMethod.Auto);

    /// <summary>
    /// A discovery document parsed from JSON, as the handler parses it, with the required fields and
    /// <paramref name="fields"/>.
    /// </summary>
    private static OpenIdConnectConfiguration Discovery(Dictionary<string, object> fields)
    {
        var document = new Dictionary<string, object>(fields)
        {
            ["issuer"] = Issuer,
            ["authorization_endpoint"] = $"{Issuer}/authorize",
            ["token_endpoint"] = $"{Issuer}/token",
            ["jwks_uri"] = $"{Issuer}/jwks",
        };
        return new OpenIdConnectConfiguration(JsonSerializer.Serialize(document));
    }
}
