using Aspire.Hosting;
using Aspire.Hosting.ApplicationModel;

namespace FullstackTemplate.AppHost.AuthProviders;

/// <summary>
/// Creates a configuration for Amazon Cognito, emulated locally by Floci in Docker.
/// The init hook in seed/ seeds the user pool, the app client, the groups, and the test users.
/// Floci keeps no data, so each start gets a new pool. The hook pins the pool ID, the client ID, and the client secret,
/// so the AppHost knows the authority before Floci starts.
/// </summary>
/// <remarks>
/// Cognito is different from the other providers in four ways:
/// <list type="bullet">
///   <item>It rejects the offline_access scope, and it returns a refresh token without it.</item>
///   <item>The access token has no aud claim. It has client_id instead.</item>
///   <item>The discovery document has no end_session_endpoint and no revocation_endpoint. The BFF sends client_id
///   and logout_uri to LogoutPath, and it does not revoke the refresh token.</item>
///   <item>The roles are in cognito:groups. The access token has no email, and its username is the sub UUID.</item>
/// </list>
/// Test users: admin@example.com (roles admin and user) and user@example.com (role user). The password is password123!.
/// </remarks>
public sealed class CognitoAuthProvider : IAuthProvider
{
    public AuthProviderConfig AddTo(IDistributedApplicationBuilder builder, AuthClientUrls clientUrls)
    {
        // The seed script pins these values with the floci:override-* tags of the user pool
        const string userPoolId = "us-east-1_aspire";
        const string clientId = "aspire-app";

        // The compat image has the AWS CLI that the seed script uses
        var floci = builder.AddContainer("cognito", "floci/floci", "2.3.0-compat")
            .WithHttpEndpoint(targetPort: 4566, name: "http");
        var flociEndpoint = floci.GetEndpoint("http");

        floci
            // The issuer of the tokens comes from FLOCI_BASE_URL. It must be the host URL that the BFF,
            // the server, and the browser use, so that the issuer check passes.
            .WithEnvironment(context =>
            {
                context.EnvironmentVariables["FLOCI_BASE_URL"] = flociEndpoint.Url;
                // Swagger UI on the server calls the token endpoint from the browser
                context.EnvironmentVariables["FLOCI_SECURITY_EXTRA_CORS_ALLOWED_ORIGINS"] = clientUrls.Server.Url;
            })
            .WithSeedFiles(this, "/etc/floci/init/ready.d")
            .WithClientUrls(clientUrls)
            // The pool exists only after the ready hook, so the discovery document is the health check
            .WithHttpHealthCheck($"/{userPoolId}/.well-known/openid-configuration");

        return new AuthProviderConfig
        {
            ClientId = clientId,
            ClientSecret = "super-secret-client-secret-change-in-production",
            Audience = clientId,
            RequireHttpsMetadata = false,
            NameClaimType = "username",
            RoleClaimType = "cognito:groups",
            UseAudienceAsScope = false, // The app client allows only the openid, profile, and email scopes
            RequestOfflineAccess = false,
            AudienceClaimType = "client_id",
            AuthResource = floci,
            AuthorityPathSuffix = $"/{userPoolId}",
            LogoutPath = "/cognito-idp/logout"
        };
    }
}
