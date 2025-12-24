using Duende.Bff;
using Duende.Bff.AccessTokenManagement;
using Duende.Bff.Yarp;
using HelloAspireReact.Bff;

var builder = WebApplication.CreateBuilder(args);

// Add service defaults & Aspire client integrations.
builder.AddServiceDefaults();

// Add services to the container.
builder.Services.AddProblemDetails();

// CORS for development (Vite runs on different port)
builder.Services.AddCors(options =>
{
    options.AddDefaultPolicy(policy =>
    {
        var frontendUrl = builder.Configuration["services:webfrontend:https:0"]
            ?? builder.Configuration["services:webfrontend:http:0"]
            ?? "http://localhost:5173";

        policy.WithOrigins(frontendUrl.TrimEnd('/'))
              .AllowAnyHeader()
              .AllowAnyMethod()
              .AllowCredentials();
    });
});

// Configure Duende BFF with OIDC authentication
// Auth provider is configured via environment variables from AppHost
builder.Services.AddBffAuthentication(builder.Configuration, builder.Environment);

var app = builder.Build();

// Configure the HTTP request pipeline.
app.UseExceptionHandler();

app.UseCors();
app.UseAuthentication();
app.UseRouting();
app.UseBff();
app.UseAuthorization();

// BFF management endpoints: /bff/login, /bff/logout, /bff/user, etc.
app.MapBffManagementEndpoints();

// Local BFF API endpoint (protected with anti-forgery)
app.MapGet("/local-api/hello", () => new { message = "Hello from BFF!", timestamp = DateTime.UtcNow })
    .AsBffApiEndpoint();

// Remote API proxy to backend server using Aspire service discovery
// Proxies all /api/* requests to the server with access token forwarding
var serverUrl = builder.Configuration["services:server:https:0"]
    ?? builder.Configuration["services:server:http:0"]
    ?? "https://localhost:5001";

app.MapRemoteBffApiEndpoint("/api", new Uri(serverUrl))
    .WithAccessToken(RequiredTokenType.UserOrNone);

app.MapDefaultEndpoints();

// Fallback: redirect root to the React frontend (for post-login redirect)
var frontendUrl = builder.Configuration["services:webfrontend:https:0"]
    ?? builder.Configuration["services:webfrontend:http:0"]
    ?? Environment.GetEnvironmentVariable("FRONTEND_URL")
    ?? "http://localhost:5173";

app.MapGet("/", () => Results.Redirect(frontendUrl));

app.Run();
