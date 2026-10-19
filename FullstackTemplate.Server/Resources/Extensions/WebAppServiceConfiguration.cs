namespace FullstackTemplate.Server.Resources.Extensions;

using Microsoft.AspNetCore.Mvc.ModelBinding.Metadata;

public static class WebAppServiceConfiguration
{
    public static void ConfigureServices(this WebApplicationBuilder builder)
    {
        builder.Services.AddProblemDetails();
        builder.Services.AddApiVersioningExtension();
        builder.Services.AddControllers(options =>
        {
            // The keys of the validation errors use the JSON names of the request, for example "name", not "Name"
            options.ModelMetadataDetailsProviders.Add(new SystemTextJsonValidationMetadataProvider());
        });
        builder.Services.AddSwaggerExtension(builder.Configuration);
        builder.Services.AddJwtBearerAuthentication(builder.Configuration, builder.Environment);
    }
}
