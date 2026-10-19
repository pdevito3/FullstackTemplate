namespace FullstackTemplate.Server.Resources.Extensions;

public static class WebAppServiceConfiguration
{
    public static void ConfigureServices(this WebApplicationBuilder builder)
    {
        builder.Services.AddProblemDetails();
    }
}
