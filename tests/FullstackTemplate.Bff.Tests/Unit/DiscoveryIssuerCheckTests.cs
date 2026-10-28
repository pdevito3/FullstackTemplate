namespace FullstackTemplate.Bff.Tests.Unit;

using FullstackTemplate.Bff.Authentication;
using Shouldly;

public class DiscoveryIssuerCheckTests
{
    [Test]
    [Arguments("https://idp.example/realms/a", "https://idp.example/realms/a")]
    [Arguments("https://idp.example/application/o/app/", "https://idp.example/application/o/app/")]
    [Arguments("https://idp.example/application/o/app/", "https://idp.example/application/o/app")]
    [Arguments("https://idp.example/realms/a", "https://idp.example/realms/a/")]
    public void the_same_issuer_passes(string issuer, string authority)
    {
        DiscoveryIssuerCheck.Matches(issuer, authority).ShouldBeTrue();
    }

    [Test]
    [Arguments("https://idp.example/realms/aINVALID", "https://idp.example/realms/a")]
    [Arguments("https://IDP.example/realms/a", "https://idp.example/realms/a")]
    [Arguments("http://idp.example/realms/a", "https://idp.example/realms/a")]
    [Arguments("acme.com", "http://localhost:24750")]
    [Arguments("", "https://idp.example")]
    [Arguments(null, "https://idp.example")]
    public void another_issuer_fails(string? issuer, string authority)
    {
        DiscoveryIssuerCheck.Matches(issuer, authority).ShouldBeFalse();
    }
}
