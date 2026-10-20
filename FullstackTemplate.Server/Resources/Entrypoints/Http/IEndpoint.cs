namespace FullstackTemplate.Server.Resources.Entrypoints.Http;

/// <summary>
/// One HTTP endpoint of a feature. <see cref="EndpointDiscovery"/> finds each implementation at startup and calls
/// <see cref="Map"/> once. The class must have a public parameterless constructor.
/// </summary>
public interface IEndpoint
{
    /// <summary>
    /// Maps the endpoint on the versioned group <c>api/v{version:apiVersion}</c>. The route is relative to the group,
    /// for example <c>/tenants/{id}</c>. Declare the API version with <c>MapToApiVersion</c>.
    /// </summary>
    void Map(IEndpointRouteBuilder versionedGroup);
}
