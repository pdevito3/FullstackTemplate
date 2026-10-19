namespace FullstackTemplate.UnitTests.AppHost;

using FullstackTemplate.AppHost;
using Shouldly;

public class CSharpReplTests
{
    [Test]
    public void parse_exports_reads_each_exported_variable()
    {
        // Arrange
        const string output = """
            # Run in the shell that launches your app, then start it and note its process id:
            # Do NOT set them as system-wide or user-wide environment variables; only set them in the shell.
            export DOTNET_STARTUP_HOOKS="/Users/dev/.nuget/packages/csharprepl.osx-arm64/0.9.2/tools/net10.0/osx-arm64/connector/CSharpRepl.InjectedHook.dll"
            export ASPNETCORE_HOSTINGSTARTUPASSEMBLIES="CSharpRepl.InjectedHook"
            """;

        // Act
        var variables = CSharpRepl.ParseExports(output);

        // Assert
        variables.Count.ShouldBe(2);
        variables["DOTNET_STARTUP_HOOKS"].ShouldBe("/Users/dev/.nuget/packages/csharprepl.osx-arm64/0.9.2/tools/net10.0/osx-arm64/connector/CSharpRepl.InjectedHook.dll");
        variables["ASPNETCORE_HOSTINGSTARTUPASSEMBLIES"].ShouldBe("CSharpRepl.InjectedHook");
    }

    [Test]
    public void parse_exports_keeps_an_equals_sign_in_the_value()
    {
        // Act
        var variables = CSharpRepl.ParseExports("export NAME=\"a=b\"\r\n");

        // Assert
        variables["NAME"].ShouldBe("a=b");
    }

    [Test]
    public void parse_exports_returns_nothing_for_output_with_no_exports()
    {
        // Act
        var variables = CSharpRepl.ParseExports("Cannot find a tool in the manifest file that has a command named 'csharprepl'.");

        // Assert
        variables.ShouldBeEmpty();
    }
}
