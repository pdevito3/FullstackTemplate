namespace FullstackTemplate.UnitTests.SourceGenerators;

using System.Text.RegularExpressions;
using FullstackTemplate.SourceGenerators;
using Microsoft.CodeAnalysis.Diagnostics;
using Shouldly;

/// <summary>
/// Directory.Build.props gives each C# project the analyzers. ServerRulesOff.globalconfig turns off each rule that
/// checks only the code of the Server, and TestRulesOff.globalconfig turns off each rule of the app code in the tests.
/// The custom tags of RuleScope say which file a rule goes in, so a new rule that is not in its file fails this test.
/// </summary>
public class ServerRulesOffTests
{
    [Test]
    [Arguments("ServerRulesOff.globalconfig", null)]
    [Arguments("TestRulesOff.globalconfig", "AppCode")]
    public void the_globalconfig_turns_off_each_rule_of_its_scope(string file, string? scope)
    {
        // Arrange
        var ids = typeof(RawStringAnalyzer).Assembly.GetTypes()
            .Where(type => type.IsSubclassOf(typeof(DiagnosticAnalyzer)) && !type.IsAbstract)
            .SelectMany(type => ((DiagnosticAnalyzer)Activator.CreateInstance(type)!).SupportedDiagnostics)
            .Where(descriptor => scope is null
                ? !descriptor.CustomTags.Any(tag => tag is "AllCode" or "AppCode")
                : descriptor.CustomTags.Contains(scope))
            .Select(descriptor => descriptor.Id)
            .Distinct()
            .Order(StringComparer.Ordinal);
        var path = Path.Combine(FindRepositoryRoot(), "FullstackTemplate.SourceGenerators", file);

        // Act
        var turnedOff = File.ReadLines(path)
            .Select(line => Regex.Match(line, @"^dotnet_diagnostic\.(?<id>\w+)\.severity = none$"))
            .Where(match => match.Success)
            .Select(match => match.Groups["id"].Value)
            .Order(StringComparer.Ordinal);

        // Assert
        turnedOff.ShouldBe(ids);
    }

    private static string FindRepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "FullstackTemplate.slnx")))
            directory = directory.Parent;

        return directory?.FullName ?? throw new InvalidOperationException("Could not find FullstackTemplate.slnx above the test output directory.");
    }
}
