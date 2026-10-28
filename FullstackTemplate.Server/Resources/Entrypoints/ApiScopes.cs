namespace FullstackTemplate.Server.Resources.Entrypoints;

public static class ApiScopes
{
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
