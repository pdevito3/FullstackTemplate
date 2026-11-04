namespace FullstackTemplate.UnitTests.SourceGenerators;

using System.Collections.Immutable;
using Microsoft.CodeAnalysis;

/// <summary>
/// The references of each test compilation. One shared list lets Roslyn read the metadata of each assembly one time.
/// A new list for each test made each compilation read all of it again, and the analyzer tests then took more than
/// 20 GB, which stopped the CI runner.
/// </summary>
internal static class PlatformReferences
{
    public static ImmutableArray<MetadataReference> All { get; } =
    [
        .. ((string)AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES")!)
            .Split(Path.PathSeparator)
            .Select(path => MetadataReference.CreateFromFile(path)),
    ];
}
