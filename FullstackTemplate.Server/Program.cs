using FullstackTemplate.Server.Resources.Extensions;

var builder = WebApplication.CreateBuilder(args);

builder.AddServiceDefaults(); // OpenTelemetry, health checks, resilience, service discovery
builder.ConfigureServices();

var app = builder.Build();

app.UseExceptionHandler();

app.MapDefaultEndpoints();

app.Run();
