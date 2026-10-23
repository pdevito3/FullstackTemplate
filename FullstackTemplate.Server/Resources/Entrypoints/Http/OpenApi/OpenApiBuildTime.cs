namespace FullstackTemplate.Server.Resources.Entrypoints.Http.OpenApi;

using System.Reflection;
using Microsoft.Extensions.DependencyInjection.Extensions;

/// <summary>
/// A Debug build writes the OpenAPI document with the GetDocument.Insider tool. The tool starts
/// Program to read the endpoints, but Aspire does not run during a build, so its settings are absent.
/// Microsoft gives the entry assembly check in "Customize runtime behavior during build-time document generation":
/// https://learn.microsoft.com/aspnet/core/fundamentals/openapi/aspnetcore-openapi#customize-runtime-behavior-during-build-time-document-generation
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
            "--Auth:ClientId=openapi-build",
        ]
        : args;

    /// <summary>
    /// Removes the hosted services for the build-time run. The tool starts the host, so a hosted service
    /// otherwise runs during a build, for example a migration against the database in the connection string.
    /// </summary>
    public static void RemoveHostedServices(IServiceCollection services)
    {
        if (IsActive)
        {
            services.RemoveAll<IHostedService>();
        }
    }
}
