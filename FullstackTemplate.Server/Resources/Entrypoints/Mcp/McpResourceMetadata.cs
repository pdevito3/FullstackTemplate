namespace FullstackTemplate.Server.Resources.Entrypoints.Mcp;

using System.Text.Json;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.Extensions.Primitives;
using Microsoft.Net.Http.Headers;
using ModelContextProtocol;
using ModelContextProtocol.Authentication;

/// <summary>
/// OAuth 2.0 Protected Resource Metadata (RFC 9728) for the MCP endpoint.
/// </summary>
/// <remarks>
/// The MCP SDK has an authentication scheme that does this, but it writes its own Bearer challenge with no error, and
/// its document endpoint takes the resource from the request. Thus the JWT bearer handler keeps the challenge, and
/// this code adds the parameter to it.
/// </remarks>
public static class McpResourceMetadata
{
    public const string PublicOriginKey = "Mcp:PublicOrigin";

    /// <summary>The path of the document. RFC 9728 section 3.1 puts the well-known segment before the path of the resource.</summary>
    public const string Path = "/.well-known/oauth-protected-resource" + EntrypointRegistration.McpPath;

    public static IServiceCollection AddMcpResourceMetadata(this IServiceCollection services, IConfiguration configuration)
    {
        var origin = ParsePublicOrigin(configuration[PublicOriginKey]);
        var authority = configuration["Auth:Authority"]
            ?? throw new InvalidOperationException("Auth:Authority configuration is required.");

        services.AddSingleton(Document(origin, authority, ApiScopes.FromConfiguration(configuration).Keys));
        var metadataUrl = origin + Path;
        services.PostConfigure<JwtBearerOptions>(JwtBearerDefaults.AuthenticationScheme,
            options => AddResourceMetadataToChallenge(options, metadataUrl));
        return services;
    }

    public static void MapMcpResourceMetadata(this IEndpointRouteBuilder endpoints)
    {
        var json = JsonSerializer.Serialize(
            endpoints.ServiceProvider.GetRequiredService<ProtectedResourceMetadata>(), McpJsonUtilities.DefaultOptions);
        endpoints.MapGet(Path, () => Results.Text(json, "application/json")).AllowAnonymous().ExcludeFromDescription();
    }

    public static string ParsePublicOrigin(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            throw new InvalidOperationException($"{PublicOriginKey} configuration is required.");

        if (!Uri.TryCreate(value, UriKind.Absolute, out var uri)
            // RFC 9728 section 1.2: the resource uses https. Plain http is only for a loopback host in development.
            || (uri.Scheme != Uri.UriSchemeHttps && !(uri.Scheme == Uri.UriSchemeHttp && uri.IsLoopback))
            || uri.AbsolutePath != "/"
            || uri.Query.Length > 0
            || uri.Fragment.Length > 0
            || uri.UserInfo.Length > 0)
        {
            throw new InvalidOperationException(
                $"{PublicOriginKey} must be an https origin (http only for a loopback host) with no path, query, fragment, or user info, for example https://api.example.com. The value is '{value}'.");
        }

        return uri.GetLeftPart(UriPartial.Authority);
    }

    private static ProtectedResourceMetadata Document(string origin, string issuer, IEnumerable<string> scopes) => new()
    {
        Resource = origin + EntrypointRegistration.McpPath,
        AuthorizationServers = [issuer],
        ScopesSupported = [.. scopes],
    };

    public static string Challenge(string metadataUrl, string? error, string? errorDescription, string? errorUri)
    {
        var challenge = $"{JwtBearerDefaults.AuthenticationScheme} resource_metadata=\"{metadataUrl}\"";
        if (!string.IsNullOrEmpty(error))
            challenge += $", error=\"{error}\"";
        if (!string.IsNullOrEmpty(errorDescription))
            challenge += $", error_description=\"{errorDescription.Replace("\"", "'")}\"";
        if (!string.IsNullOrEmpty(errorUri))
            challenge += $", error_uri=\"{errorUri}\"";
        return challenge;
    }

    private static void AddResourceMetadataToChallenge(JwtBearerOptions options, string metadataUrl)
    {
        var challenge = options.Events.OnChallenge;
        options.Events.OnChallenge = async context =>
        {
            await challenge(context);
            if (context.Handled || !context.Request.Path.StartsWithSegments(EntrypointRegistration.McpPath))
                return;

            context.HandleResponse();
            context.Response.StatusCode = StatusCodes.Status401Unauthorized;
            // Some MCP clients, such as the TypeScript SDK, read only the first challenge, so it goes before the DPoP one
            var headers = context.Response.Headers;
            headers[HeaderNames.WWWAuthenticate] = StringValues.Concat(
                Challenge(metadataUrl, context.Error, context.ErrorDescription, context.ErrorUri),
                headers[HeaderNames.WWWAuthenticate]);
        };
    }
}
