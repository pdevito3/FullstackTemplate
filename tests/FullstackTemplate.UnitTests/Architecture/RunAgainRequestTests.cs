namespace FullstackTemplate.UnitTests.Architecture;

using System.Collections;
using System.Reflection;
using FullstackTemplate.ArchitectureFixtures;
using FullstackTemplate.Server.Resources;
using Shouldly;

/// <summary>
/// An <see cref="IRunsAgainWhenRaced"/> request has no <c>Version</c>. With a version, a lost race is a stale version
/// on each run, so the behavior runs the command <c>MaxRuns</c> times, and the caller gets a concurrent change, not the
/// stale version that tells it to load the record again. The rule reads the request and each DTO in it, at any depth.
/// A DTO is a class or a struct of the assembly of the request, or a list of these types.
/// </summary>
public class RunAgainRequestTests
{
    [Test]
    public void no_request_that_runs_again_when_raced_has_a_version()
    {
        VersionsInRunAgainRequests(typeof(PagedList).Assembly).ShouldBeEmpty(
            "A command that sends a version gets a stale version when it loses a race. Remove IRunsAgainWhenRaced from " +
            "it, or remove the version. See docs/backend/concurrency.md.");
    }

    [Test]
    public void rule_finds_a_version_on_the_request_in_a_dto_in_a_list_of_dtos_and_on_a_struct()
    {
        VersionsInRunAgainRequests(typeof(FixtureMarker).Assembly).ShouldBe(
        [
            "FullstackTemplate.ArchitectureFixtures.Core.RunAgainCommandWithVersion.Version",
            "FullstackTemplate.ArchitectureFixtures.Core.RunAgainCommandWithVersionInDto.Body.Version",
            "FullstackTemplate.ArchitectureFixtures.Core.RunAgainCommandWithVersionInDtoList.Lines.Version",
            "FullstackTemplate.ArchitectureFixtures.Core.RunAgainStructCommandWithVersion.Version",
            "FullstackTemplate.ArchitectureFixtures.Core.RunAgainStructCommandWithVersionInDto.Body.Version",
        ]);
    }

    private static string[] VersionsInRunAgainRequests(Assembly assembly)
        => assembly.GetTypes()
            .Where(type => (type.IsValueType || type is { IsClass: true, IsAbstract: false }) && typeof(IRunsAgainWhenRaced).IsAssignableFrom(type))
            .SelectMany(type => VersionPaths(type, type.FullName!, assembly, []))
            .Order(StringComparer.Ordinal)
            .ToArray();

    private static IEnumerable<string> VersionPaths(Type type, string path, Assembly owner, HashSet<Type> visited)
    {
        if (!visited.Add(type))
            yield break;

        foreach (var property in type.GetProperties(BindingFlags.Public | BindingFlags.Instance))
        {
            var propertyPath = $"{path}.{property.Name}";
            if (string.Equals(property.Name, "Version", StringComparison.OrdinalIgnoreCase))
            {
                yield return propertyPath;
                continue;
            }

            var dto = ElementOf(property.PropertyType) ?? property.PropertyType;
            if ((dto.IsClass || dto.IsValueType) && dto != typeof(string) && dto.Assembly == owner)
                foreach (var nested in VersionPaths(dto, propertyPath, owner, visited))
                    yield return nested;
        }
    }

    private static Type? ElementOf(Type type)
        => type == typeof(string) || !typeof(IEnumerable).IsAssignableFrom(type)
            ? null
            : type.GetInterfaces().Append(type)
                .FirstOrDefault(i => i.IsGenericType && i.GetGenericTypeDefinition() == typeof(IEnumerable<>))
                ?.GetGenericArguments()[0];
}
