namespace FullstackTemplate.Server.Resources.Entrypoints;

/// <summary>
/// The scopes that an OAuth client of this API asks for. Swagger UI shows them, and the MCP resource metadata lists
/// them in <c>scopes_supported</c>.
/// </summary>
public static class ApiScopes
{
    /// <summary>
    /// The scopes of <c>Auth:Scopes</c>, with their descriptions. If <c>Auth:Scopes</c> is not set, openid and
    /// profile, and also <c>Auth:Audience</c> when <c>Auth:UseAudienceAsScope</c> is true (the default).
    /// </summary>
    public static Dictionary<string, string> FromConfiguration(IConfiguration configuration)
    {
        var scopes = configuration.GetSection("Auth:Scopes").Get<Dictionary<string, string>>();
        if (scopes is { Count: > 0 })
            return scopes;

        scopes = new Dictionary<string, string>
        {
            { "openid", "OpenID Connect scope" },
            { "profile", "User profile information" }
        };

        // Some OAuth providers (like Keycloak) require the audience as a scope to get API access tokens
        // Others (like FusionAuth) don't support custom scopes
        var audience = configuration["Auth:Audience"];
        if (configuration.GetValue("Auth:UseAudienceAsScope", true) && !string.IsNullOrEmpty(audience))
            scopes[audience] = "API access scope";

        return scopes;
    }
}
