namespace FullstackTemplate.Server.Resources.Entrypoints.Http;

using Asp.Versioning;

/// <summary>
/// The versions of the HTTP API. An endpoint maps to one of these with <c>MapToApiVersion</c>, so that each version
/// has one definition. To add a version, add it here and to <see cref="ApiVersioningExtension.SupportedVersions"/>.
/// </summary>
public static class ApiVersions
{
    public static readonly ApiVersion V1 = new(1, 0);
}
