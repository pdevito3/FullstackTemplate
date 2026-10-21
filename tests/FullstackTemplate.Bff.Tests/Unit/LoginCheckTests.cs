namespace FullstackTemplate.Bff.Tests.Unit;

using System.Text;
using FullstackTemplate.Bff.Features;
using FullstackTemplate.Bff.Login;
using Microsoft.Extensions.Primitives;
using Shouldly;

public class LoginCheckTests
{
    private const string Issuer = "https://idp.example/realms/a";

    [Test]
    [Arguments(IssuerParameterRule.Required)]
    [Arguments(IssuerParameterRule.IfPresent)]
    public void an_iss_that_equals_the_issuer_passes(IssuerParameterRule rule)
    {
        AuthorizationResponseIssuer.Validate(new StringValues(Issuer), rule, Issuer).ShouldBeNull();
    }

    [Test]
    [Arguments(IssuerParameterRule.Required, "https://evil.example")]
    [Arguments(IssuerParameterRule.IfPresent, "https://evil.example")]
    [Arguments(IssuerParameterRule.IfPresent, "https://idp.example/realms/a/")]
    [Arguments(IssuerParameterRule.IfPresent, "https://IDP.example/realms/a")]
    [Arguments(IssuerParameterRule.IfPresent, "")]
    public void an_iss_that_is_not_the_issuer_fails_with_an_ordinal_compare(IssuerParameterRule rule, string iss)
    {
        AuthorizationResponseIssuer.Validate(new StringValues(iss), rule, Issuer).ShouldNotBeNull();
    }

    [Test]
    public void a_missing_iss_fails_when_discovery_lists_the_parameter()
    {
        AuthorizationResponseIssuer.Validate(StringValues.Empty, IssuerParameterRule.Required, Issuer).ShouldNotBeNull();
    }

    [Test]
    public void a_missing_iss_passes_when_discovery_does_not_list_the_parameter()
    {
        AuthorizationResponseIssuer.Validate(StringValues.Empty, IssuerParameterRule.IfPresent, Issuer).ShouldBeNull();
    }

    [Test]
    public void two_iss_values_fail_even_when_one_matches()
    {
        AuthorizationResponseIssuer.Validate(new StringValues([Issuer, Issuer]), IssuerParameterRule.IfPresent, Issuer).ShouldNotBeNull();
    }

    [Test]
    public void disable_does_no_check()
    {
        AuthorizationResponseIssuer.Validate(new StringValues("https://evil.example"), IssuerParameterRule.Disabled, Issuer).ShouldBeNull();
    }

    [Test]
    public void an_id_token_with_only_the_client_id_as_audience_passes()
    {
        IdTokenChecks.CheckAudience(["client-1"], "client-1").ShouldBeNull();
    }

    [Test]
    [Arguments("client-1 other-api")]
    [Arguments("other-api client-1")]
    [Arguments("client-1 client-1")]
    [Arguments("CLIENT-1")]
    [Arguments("")]
    public void an_id_token_with_another_audience_fails(string audiences)
    {
        IdTokenChecks.CheckAudience(audiences.Split(' ', StringSplitOptions.RemoveEmptyEntries), "client-1").ShouldNotBeNull();
    }

    [Test]
    [Arguments("client-1", "secret", "client-1:secret")]
    [Arguments("client 1", "a b", "client+1:a+b")]
    [Arguments("client:1", "p%s&w=d+", "client%3A1:p%25s%26w%3Dd%2B")]
    [Arguments("kunde", "geheïm", "kunde:gehe%C3%AFm")]
    public void the_basic_header_form_encodes_the_client_id_and_the_secret_before_base64(string clientId, string secret, string expected)
    {
        var header = TokenClientAuthentication.BasicHeader(clientId, secret);

        header.Scheme.ShouldBe("Basic");
        Encoding.UTF8.GetString(Convert.FromBase64String(header.Parameter!)).ShouldBe(expected);
    }
}
