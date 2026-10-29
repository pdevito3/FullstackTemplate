namespace FullstackTemplate.Bff.Tests.Unit;

using FullstackTemplate.Bff.Management;
using Shouldly;

public class LocalUrlTests
{
    [Test]
    [Arguments("/")]
    [Arguments("/a")]
    [Arguments("/a/b?c=d#e")]
    [Arguments("/a%20b")]
    [Arguments("/weather?city=//x")]
    [Arguments("/caf%C3%A9")]
    [Arguments("/a/..//evil.example")]
    public void a_local_path_passes_unchanged(string returnUrl)
    {
        // Act & Assert
        LocalUrl.TryParse(returnUrl, out var localUrl).ShouldBeTrue();
        localUrl!.Value.ShouldBe(returnUrl);
    }

    [Test]
    [Arguments("/café", "/caf%C3%A9")]
    [Arguments("/users/José?city=東京#é", "/users/Jos%C3%A9?city=%E6%9D%B1%E4%BA%AC#%C3%A9")]
    [Arguments("/\U0001F600", "/%F0%9F%98%80")]
    [Arguments("/\u2028", "/%E2%80%A8")]
    [Arguments("/\uFF0Fevil.example", "/%EF%BC%8Fevil.example")]
    public void a_non_ascii_character_becomes_its_percent_encoded_utf8_bytes(string returnUrl, string expected)
    {
        // Act & Assert
        LocalUrl.TryParse(returnUrl, out var localUrl).ShouldBeTrue();
        localUrl!.Value.ShouldBe(expected);
    }

    [Test]
    [Arguments(null)]
    [Arguments("")]
    [Arguments("a")]
    [Arguments("@evil.example")]
    [Arguments("//evil.example")]
    [Arguments("//evil.example/a")]
    [Arguments("/\\evil.example")]
    [Arguments("\\\\evil.example")]
    [Arguments("https://evil.example")]
    [Arguments("https://localhost/a")]
    [Arguments("javascript:alert(1)")]
    [Arguments("/a\nb")]
    [Arguments("/a\r\nLocation: https://evil.example")]
    [Arguments("/a\tb")]
    [Arguments("/\t/evil.example")]
    [Arguments("/\u0000")]
    [Arguments("/a\u007f")]
    [Arguments("/a\u0085")]
    [Arguments("/a\ud800")]
    [Arguments("/a\udc00b")]
    public void a_value_that_is_not_a_local_path_fails(string? returnUrl)
    {
        // Act & Assert
        LocalUrl.TryParse(returnUrl, out var localUrl).ShouldBeFalse();
        localUrl.ShouldBeNull();
    }
}
