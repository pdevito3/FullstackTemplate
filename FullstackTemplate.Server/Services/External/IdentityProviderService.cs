namespace FullstackTemplate.Server.Services.External;

using Resources.Extensions;

/// <summary>
/// Stub implementation of the Identity Provider service.
/// Replace with actual IDP integration (Keycloak Admin API, FusionAuth API, etc.).
/// </summary>
public sealed class IdentityProviderService : IIdentityProviderService
{
    public Task<IdpUser?> GetUserAsync(string identifier, CancellationToken cancellationToken = default)
    {
        throw new NotImplementedException("IDP integration not yet implemented. " +
            "Implement this method to call your IDP's user management API.");
    }

    public Task<IdpUser> CreateUserAsync(IdpUserForCreation user, CancellationToken cancellationToken = default)
    {
        throw new NotImplementedException("IDP integration not yet implemented. " +
            "Implement this method to call your IDP's user creation API.");
    }

    public Task<IdpUser> UpdateUserAsync(string identifier, IdpUserForUpdate user, CancellationToken cancellationToken = default)
    {
        throw new NotImplementedException("IDP integration not yet implemented. " +
            "Implement this method to call your IDP's user update API.");
    }

    public Task DeleteUserAsync(string identifier, CancellationToken cancellationToken = default)
    {
        throw new NotImplementedException("IDP integration not yet implemented. " +
            "Implement this method to call your IDP's user deletion API.");
    }

    public Task<IdpUser?> SyncUserAsync(string identifier, CancellationToken cancellationToken = default)
    {
        throw new NotImplementedException("IDP integration not yet implemented. " +
            "Implement this method to sync user data from your IDP to the local database.");
    }
}
