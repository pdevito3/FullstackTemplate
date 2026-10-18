namespace FullstackTemplate.Server.Resources.Extensions;

using System.Globalization;
using Asp.Versioning;
using Asp.Versioning.Builder;

public static class ApiVersioningExtension
{
    // Add a version here to serve it. Each version gets its own OpenAPI document and Swagger UI entry.
    public static readonly ApiVersion[] SupportedVersions = [new ApiVersion(1, 0)];

    // The group name format gives "v1" for 1.0 and "v2" for 2.0, and the OpenAPI document of a version has this name
    private const string GroupNameFormat = "'v'VVV";

    public static string DocumentName(ApiVersion version) => version.ToString(GroupNameFormat, CultureInfo.InvariantCulture);

    public static IApiVersioningBuilder AddApiVersioningExtension(this IServiceCollection services)
    {
        return services.AddApiVersioning(options =>
        {
            options.DefaultApiVersion = SupportedVersions[0];
            options.ReportApiVersions = true;
            options.ApiVersionReader = new UrlSegmentApiVersionReader();
        })
        // AddMvc applies the [ApiVersion] attributes of the controllers, so a version that is not declared gets an error
        .AddMvc()
        // Each version is its own group, so each OpenAPI document contains only the operations of its version
        .AddApiExplorer(options =>
        {
            options.GroupNameFormat = GroupNameFormat;
            options.SubstituteApiVersionInUrl = true;
        });
    }

    public static ApiVersionSet GetApiVersionSet(this WebApplication app)
    {
        var versionSet = app.NewApiVersionSet();
        foreach (var version in SupportedVersions)
            versionSet.HasApiVersion(version);

        return versionSet
            .ReportApiVersions()
            .Build();
    }
}
