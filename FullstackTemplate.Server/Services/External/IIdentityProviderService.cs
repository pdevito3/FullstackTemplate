namespace FullstackTemplate.Server.Services.External;

/// <summary>
/// Service for interacting with the Identity Provider (IDP) for user management.
/// </summary>
public interface IIdentityProviderService
{
    /// <summary>
    /// Gets a user from the IDP by their identifier (sub claim).
    /// </summary>
    Task<IdpUser?> GetUserAsync(string identifier, CancellationToken cancellationToken = default);

    /// <summary>
    /// Creates a new user in the IDP.
    /// </summary>
    Task<IdpUser> CreateUserAsync(IdpUserForCreation user, CancellationToken cancellationToken = default);

    /// <summary>
    /// Updates an existing user in the IDP.
    /// </summary>
    Task<IdpUser> UpdateUserAsync(string identifier, IdpUserForUpdate user, CancellationToken cancellationToken = default);

    /// <summary>
    /// Deletes a user from the IDP.
    /// </summary>
    Task DeleteUserAsync(string identifier, CancellationToken cancellationToken = default);

    /// <summary>
    /// Syncs a user from the IDP to the local database.
    /// Returns the local user if found, or creates one if not.
    /// </summary>
    Task<IdpUser?> SyncUserAsync(string identifier, CancellationToken cancellationToken = default);
}

/// <summary>
/// Represents a user from the Identity Provider.
/// </summary>
public sealed record IdpUser
{
    public string Identifier { get; init; } = default!;
    public string Email { get; init; } = default!;
    public string Username { get; init; } = default!;
    public string FirstName { get; init; } = default!;
    public string LastName { get; init; } = default!;
    public IReadOnlyList<string> Roles { get; init; } = [];
}

/// <summary>
/// Data for creating a user in the Identity Provider.
/// </summary>
public sealed record IdpUserForCreation
{
    public string Email { get; init; } = default!;
    public string Username { get; init; } = default!;
    public string FirstName { get; init; } = default!;
    public string LastName { get; init; } = default!;
    public string Password { get; init; } = default!;
    public IReadOnlyList<string> Roles { get; init; } = [];
}

/// <summary>
/// Data for updating a user in the Identity Provider.
/// </summary>
public sealed record IdpUserForUpdate
{
    public string? Email { get; init; }
    public string? Username { get; init; }
    public string? FirstName { get; init; }
    public string? LastName { get; init; }
    public IReadOnlyList<string>? Roles { get; init; }
}
