namespace FullstackTemplate.Server.Resources.Entrypoints.Http;

using System.Diagnostics.CodeAnalysis;
using System.Reflection;

/// <summary>
/// Finds each <see cref="IEndpoint"/> in the Server assembly and maps it on the versioned API group.
/// </summary>
public static class EndpointDiscovery
{
    /// <summary>The route of the versioned group. Each endpoint route is relative to it.</summary>
    public const string VersionedRoutePrefix = "api/v{version:apiVersion}";

    /// <summary>
    /// Maps the versioned group with the API version set and the filters that apply to all endpoints, then maps each
    /// <see cref="IEndpoint"/> on it. Each versioned endpoint is an <see cref="IEndpoint"/>, so no other code maps on the group.
    /// </summary>
    [SuppressMessage("ApiDesign", "RS0030:Do not use banned APIs",
        Justification = "Startup scan. It runs one time, when the app maps its routes.")]
    public static void MapVersionedEndpoints(this WebApplication app)
    {
        var versionedGroup = app.MapGroup(VersionedRoutePrefix)
            .WithApiVersionSet(app.GetApiVersionSet())
            // On the group, not on each list endpoint, so that a new list endpoint cannot forget the header
            .AddEndpointFilter<PaginationHeaderFilter>();

        foreach (var endpointType in EndpointTypes(typeof(EndpointDiscovery).Assembly))
            ((IEndpoint)Activator.CreateInstance(endpointType)!).Map(versionedGroup);
    }

    /// <summary>
    /// Each concrete <see cref="IEndpoint"/> in the assembly, in a fixed order (by full name), so that the
    /// OpenAPI document does not change between builds.
    /// </summary>
    public static IReadOnlyList<Type> EndpointTypes(Assembly assembly) => assembly.GetTypes()
        .Where(type => type is { IsClass: true, IsAbstract: false } && typeof(IEndpoint).IsAssignableFrom(type))
        .OrderBy(type => type.FullName, StringComparer.Ordinal)
        .ToList();
}
