namespace FullstackTemplate.Server.Resources.Extensions;

using Asp.Versioning;
using Asp.Versioning.Builder;

public static class ApiVersioningExtension
{
    public static IServiceCollection AddApiVersioningExtension(this IServiceCollection services)
    {
        services.AddApiVersioning(options =>
        {
            options.DefaultApiVersion = new ApiVersion(1, 0);
            options.AssumeDefaultVersionWhenUnspecified = true;
            options.ReportApiVersions = true;
            options.ApiVersionReader = new UrlSegmentApiVersionReader();
        });

        return services;
    }

    public static ApiVersionSet GetApiVersionSet(this WebApplication app)
    {
        return app.NewApiVersionSet()
            .HasApiVersion(new ApiVersion(1, 0))
            // .HasApiVersion(new ApiVersion(2, 0)) // Add more versions as needed
            .ReportApiVersions()
            .Build();
    }
}
