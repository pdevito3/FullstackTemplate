using System.Security.Claims;
using HelloAspireReact.Server;

var builder = WebApplication.CreateBuilder(args);

// Add service defaults & Aspire client integrations.
builder.AddServiceDefaults();

// Add services to the container.
builder.Services.AddProblemDetails();

// Learn more about configuring OpenAPI at https://aka.ms/aspnet/openapi
builder.Services.AddOpenApi();

// Configure JWT Bearer authentication
// Auth provider is configured via environment variables from AppHost
builder.Services.AddJwtBearerAuthentication(builder.Configuration, builder.Environment);

var app = builder.Build();

// Configure the HTTP request pipeline.
app.UseExceptionHandler();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.UseAuthentication();
app.UseAuthorization();

string[] summaries = ["Freezing", "Bracing", "Chilly", "Cool", "Mild", "Warm", "Balmy", "Hot", "Sweltering", "Scorching"];

// Public weather endpoint - no authentication required
// BFF proxies /api/weatherforecast → /weatherforecast
app.MapGet("/weatherforecast", () =>
{
    var forecast = Enumerable.Range(1, 5).Select(index =>
        new WeatherForecast
        (
            DateOnly.FromDateTime(DateTime.Now.AddDays(index)),
            Random.Shared.Next(-20, 55),
            summaries[Random.Shared.Next(summaries.Length)]
        ))
        .ToArray();
    return forecast;
})
.WithName("GetWeatherForecast");

// Secure weather endpoint - requires authentication
// BFF proxies /api/weatherforecast/secure → /weatherforecast/secure
app.MapGet("/weatherforecast/secure", (ClaimsPrincipal user) =>
{
    var userName = user.Identity?.Name ?? user.FindFirstValue("sub") ?? "Unknown";
    var forecast = Enumerable.Range(1, 5).Select(index =>
        new SecureWeatherForecast
        (
            DateOnly.FromDateTime(DateTime.Now.AddDays(index)),
            Random.Shared.Next(-20, 55),
            summaries[Random.Shared.Next(summaries.Length)],
            userName
        ))
        .ToArray();
    return forecast;
})
.RequireAuthorization()
.WithName("GetSecureWeatherForecast");

app.MapDefaultEndpoints();

app.UseFileServer();

app.Run();

record WeatherForecast(DateOnly Date, int TemperatureC, string? Summary)
{
    public int TemperatureF => 32 + (int)(TemperatureC / 0.5556);
}

record SecureWeatherForecast(DateOnly Date, int TemperatureC, string? Summary, string RequestedBy)
{
    public int TemperatureF => 32 + (int)(TemperatureC / 0.5556);
}
