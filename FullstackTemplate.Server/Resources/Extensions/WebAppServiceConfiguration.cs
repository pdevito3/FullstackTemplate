namespace FullstackTemplate.Server.Resources.Extensions;

using Databases;
using Microsoft.EntityFrameworkCore;
using ZiggyCreatures.Caching.Fusion;

public static class WebAppServiceConfiguration
{
    public static void ConfigureServices(this WebApplicationBuilder builder)
    {
        builder.Services.AddSingleton(TimeProvider.System);
        builder.Services.AddProblemDetails();
        builder.Services.AddHttpContextAccessor();
        builder.Services.AddFusionCache()
            .WithDefaultEntryOptions(options =>
            {
                options.Duration = TimeSpan.FromMinutes(5);
                options.IsFailSafeEnabled = true;
                options.FailSafeMaxDuration = TimeSpan.FromHours(2);
                options.FailSafeThrottleDuration = TimeSpan.FromSeconds(30);
            });
        builder.Services.AddApplicationServices();
        builder.Services.AddMediatR(cfg => cfg.RegisterServicesFromAssembly(typeof(Program).Assembly));
        builder.Services.AddApiVersioningExtension();
        builder.Services.AddControllers();
        builder.Services.AddSwaggerExtension(builder.Configuration);
        builder.Services.AddJwtBearerAuthentication(builder.Configuration, builder.Environment);

        builder.Services.AddDbContext<AppDbContext>((serviceProvider, options) =>
        {
            // use this to account for malformed DATABASE_URL from Northflank. Please remove it if you don't need it
            var connectionString = ConnectionStringHelper
                .ConvertPostgresUri(builder.Configuration.GetConnectionString(DatabaseConsts.DatabaseName));
            // var connectionString = builder.Configuration.GetConnectionString(DatabaseConsts.DatabaseName);
            
            options.UseNpgsql(connectionString)
                .UseSnakeCaseNamingConvention();
        });
        builder.EnrichNpgsqlDbContext<AppDbContext>();
    }
}
