namespace FullstackTemplate.UnitTests.Architecture;

using Shouldly;

/// <summary>
/// <c>FullstackTemplate.Server/BannedSymbols.txt</c> bans the APIs that write without the save, so without an audit
/// entry (RS0030). It also bans the two-argument <c>Like</c> and <c>ILike</c>, which send <c>ESCAPE ''</c>. Code that writes a <c>[NotAudited]</c> table with one of them suppresses RS0030. It also bans
/// the run-time regex and reflection APIs that a source generator replaces. A startup scan or a per-type cache suppresses
/// RS0030 for them (see <c>docs/backend/source-generators.md</c>). This test pins the files that can suppress it, so each
/// new suppression changes this list and gets a review.
/// </summary>
public class BannedApiSuppressionTests
{
    private static readonly string[] FilesThatSuppressTheBan =
    [
        "FullstackTemplate.Server/Databases/PostgresCopy.cs",
        "FullstackTemplate.Server/Domain/DPoPProofs/Features/DeleteExpiredDPoPProofs/DeleteExpiredDPoPProofs.cs",
        "FullstackTemplate.Server/Domain/DPoPProofs/Features/RegisterDPoPProof/RegisterDPoPProof.cs",
        "FullstackTemplate.Server/Databases/Auditing/AuditVocabulary.cs",
        "FullstackTemplate.Server/Resources/Entrypoints/Http/EndpointDiscovery.cs",
        "FullstackTemplate.Server/Resources/RequestValidationBehavior.cs",
    ];

    private static readonly string[] SettingsExtensions = [".cs", ".csproj", ".props", ".editorconfig", ".globalconfig"];

    [Test]
    public void only_the_reviewed_files_suppress_a_banned_api()
    {
        // Arrange
        var root = FindRepositoryRoot();
        var files = Directory.EnumerateFiles(Path.Combine(root, "FullstackTemplate.Server"), "*", SearchOption.AllDirectories)
            .Concat([Path.Combine(root, ".editorconfig"), Path.Combine(root, "Directory.Build.props")])
            .Select(file => Path.GetRelativePath(root, file).Replace('\\', '/'))
            .Where(file => SettingsExtensions.Contains(Path.GetExtension(file)))
            .Where(file => !file.Split('/').Intersect(["bin", "obj"]).Any());

        // Act
        var suppressing = files
            .Where(file => File.ReadAllText(Path.Combine(root, file)).Contains("RS0030", StringComparison.Ordinal))
            .ToList();

        // Assert
        suppressing.ShouldBe(FilesThatSuppressTheBan, ignoreOrder: true);
    }

    private static string FindRepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "FullstackTemplate.slnx")))
            directory = directory.Parent;

        return directory?.FullName ?? throw new InvalidOperationException("Could not find FullstackTemplate.slnx above the test output directory.");
    }
}
