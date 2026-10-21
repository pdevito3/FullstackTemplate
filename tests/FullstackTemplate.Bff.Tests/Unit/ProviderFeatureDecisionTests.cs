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
        var (features, warnings) = Decide(Discovery(AllFeatures), new ProviderFeatureOptions());

        features.ShouldBe(new ActiveFeatures(Issuer, Revocation: true, IssuerParameterRule.Required, BackChannelLogout: true,
            EndSession: true, PushedAuthorization: true, ClientAuthenticationMethod.Basic));
        warnings.ShouldBeEmpty();
    }

    [Test]
    public void auto_falls_back_for_each_missing_feature_with_one_warning_each()
    {
        var (features, warnings) = Decide(Discovery([]), new ProviderFeatureOptions());

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
        var (features, warnings) = ProviderFeatureDecision.Decide(
            Discovery([]), new ProviderFeatureOptions(), ClientAuthenticationMethod.Auto, $"{Issuer}/logout");

        features.EndSession.ShouldBeTrue();
        warnings.ShouldNotContain(w => w.Feature == "EndSession");
    }

    [Test]
    [Arguments("authorization_response_iss_parameter_supported", false)]
    [Arguments("backchannel_logout_supported", false)]
    public void a_false_flag_does_not_list_the_feature(string field, bool value)
    {
        var (_, warnings) = Decide(Discovery(new() { [field] = value }), new ProviderFeatureOptions());

        warnings.ShouldContain(w => w.DiscoveryField == field);
    }

    [Test]
    [MethodDataSource(nameof(RequireSettings))]
    public void require_stops_the_bff_when_discovery_does_not_list_the_feature(string feature, Action<ProviderFeatureOptions> require)
    {
        var options = new ProviderFeatureOptions();
        require(options);

        var exception = Should.Throw<ProviderFeatureException>(() => Decide(Discovery([]), options));

        exception.Message.ShouldContain($"Auth:Features:{feature} is Require");
    }

    [Test]
    [MethodDataSource(nameof(RequireSettings))]
    public void require_uses_the_feature_that_discovery_lists_with_no_warning(string feature, Action<ProviderFeatureOptions> require)
    {
        var options = new ProviderFeatureOptions();
        require(options);

        var (_, warnings) = Decide(Discovery(AllFeatures), options);

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
        var options = new ProviderFeatureOptions
        {
            Revocation = FeatureMode.Disable,
            IssuerParameter = FeatureMode.Disable,
            BackChannelLogout = FeatureMode.Disable,
            EndSession = FeatureMode.Disable,
            PushedAuthorization = FeatureMode.Disable,
        };

        var (features, warnings) = Decide(Discovery(AllFeatures), options);

        features.ShouldBe(new ActiveFeatures(Issuer, Revocation: false, IssuerParameterRule.Disabled, BackChannelLogout: false,
            EndSession: false, PushedAuthorization: false, ClientAuthenticationMethod.Basic));
        warnings.ShouldBeEmpty();
    }

    [Test]
    [Arguments(FeatureMode.Auto)]
    [Arguments(FeatureMode.Require)]
    public void dpop_other_than_disable_stops_the_bff_in_phase_1(FeatureMode mode)
    {
        Should.Throw<ProviderFeatureException>(() =>
            Decide(Discovery(AllFeatures), new ProviderFeatureOptions { DPoP = mode }));
    }

    [Test]
    [Arguments(FeatureMode.Auto, PushedAuthorizationBehavior.UseIfAvailable)]
    [Arguments(FeatureMode.Require, PushedAuthorizationBehavior.Require)]
    [Arguments(FeatureMode.Disable, PushedAuthorizationBehavior.Disable)]
    public void each_pushed_authorization_value_maps_to_the_handler_behavior(FeatureMode mode, PushedAuthorizationBehavior expected)
    {
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
        var fields = new Dictionary<string, object>(AllFeatures);
        if (supported.Length > 0)
            fields["token_endpoint_auth_methods_supported"] = supported;

        var (features, warnings) = ProviderFeatureDecision.Decide(Discovery(fields), new ProviderFeatureOptions(), setting);

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
