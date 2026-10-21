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
    public void a_local_path_passes(string returnUrl)
    {
        LocalUrl.IsLocal(returnUrl).ShouldBeTrue();
    }

    [Test]
    [Arguments("")]
    [Arguments("a")]
    [Arguments("//evil.example")]
    [Arguments("//evil.example/a")]
    [Arguments("/\\evil.example")]
    [Arguments("\\\\evil.example")]
    [Arguments("https://evil.example")]
    [Arguments("https://localhost/a")]
    [Arguments("javascript:alert(1)")]
    [Arguments("/a\nb")]
    [Arguments("/a\tb")]
    [Arguments("/\u0000")]
    [Arguments("/a\u007f")]
    public void a_value_that_is_not_a_local_path_fails(string returnUrl)
    {
        LocalUrl.IsLocal(returnUrl).ShouldBeFalse();
    }
}
