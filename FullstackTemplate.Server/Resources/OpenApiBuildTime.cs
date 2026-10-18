namespace FullstackTemplate.Server.Resources;

using System.Reflection;

/// <summary>
/// A Debug build writes the OpenAPI document with the GetDocument.Insider tool. The tool starts
/// Program to read the endpoints, but Aspire does not run during a build, so its settings are absent.
/// </summary>
public static class OpenApiBuildTime
{
    public static bool IsActive { get; } = Assembly.GetEntryAssembly()?.GetName().Name == "GetDocument.Insider";

    /// <summary>
    /// Adds the host settings for the build-time run: no config file watchers, which can block a build,
    /// and placeholder values for the settings that Aspire gives at run time.
    /// </summary>
    public static string[] WithHostArgs(string[] args) => IsActive
        ?
        [
            ..args,
            "--hostBuilder:reloadConfigOnChange=false",
            "--Auth:Authority=https://openapi-build.invalid",
            "--Auth:Audience=openapi-build",
        ]
        : args;
}
