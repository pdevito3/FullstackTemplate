namespace FullstackTemplate.AppHost.AuthProviders;

/// <summary>
/// Creates a configuration for the Duende Demo identity server.
/// This is a public demo server useful for testing. It adds no resources to the stack.
/// Test users: bob and alice. The password is the user name.
/// </summary>
public sealed class DuendeDemoAuthProvider : IAuthProvider
{
    public AuthProviderConfig AddTo(IDistributedApplicationBuilder builder, AuthClientUrls clientUrls) => new()
    {
        Authority = "https://demo.duendesoftware.com",
        ClientId = "interactive.confidential",
        ClientSecret = "secret",
        Audience = "api",
        RequireHttpsMetadata = true,
        NameClaimType = "name",
        RoleClaimType = "role",
        AuthResource = null
    };
}
