namespace FullstackTemplate.Server.Services;

using System.Security.Claims;

/// <summary>
/// Provides access to the current authenticated user's information.
/// Works with all supported auth providers: Keycloak, FusionAuth, and Duende Demo.
/// </summary>
public interface ICurrentUserService
{
    /// <summary>
    /// The current user's claims principal, or null if not authenticated.
    /// </summary>
    ClaimsPrincipal? User { get; }

    /// <summary>
    /// The unique identifier for the user (from "sub" claim - OIDC standard).
    /// </summary>
    string? UserId { get; }

    /// <summary>
    /// The user's email address.
    /// </summary>
    string? Email { get; }

    /// <summary>
    /// The user's first/given name.
    /// </summary>
    string? FirstName { get; }

    /// <summary>
    /// The user's last/family name.
    /// </summary>
    string? LastName { get; }

    /// <summary>
    /// The user's display name. Falls back through multiple claim types
    /// to handle different providers.
    /// </summary>
    string? Name { get; }

    /// <summary>
    /// The user's preferred username.
    /// </summary>
    string? Username { get; }

    /// <summary>
    /// The client ID for machine-to-machine authentication.
    /// </summary>
    string? ClientId { get; }

    /// <summary>
    /// True if this is a machine-to-machine (client credentials) authentication.
    /// </summary>
    bool IsMachine { get; }

    /// <summary>
    /// True if the user is authenticated.
    /// </summary>
    bool IsAuthenticated { get; }

    /// <summary>
    /// Gets the user's roles from claims.
    /// Handles both "role" (Duende) and "roles" (Keycloak/FusionAuth) claim types.
    /// </summary>
    IReadOnlyList<string> Roles { get; }

    /// <summary>
    /// Checks if the user has a specific role.
    /// </summary>
    bool IsInRole(string role);

    /// <summary>
    /// Gets a specific claim value by type.
    /// </summary>
    string? GetClaimValue(string claimType);

    /// <summary>
    /// Gets all claim values for a given type (for multi-valued claims like roles).
    /// </summary>
    IReadOnlyList<string> GetClaimValues(string claimType);
}

public sealed class CurrentUserService(IHttpContextAccessor httpContextAccessor) : ICurrentUserService
{
    public ClaimsPrincipal? User => httpContextAccessor.HttpContext?.User;

    public string? UserId => User?.FindFirstValue(ClaimTypes.NameIdentifier)
        ?? User?.FindFirstValue("sub");

    public string? Email => User?.FindFirstValue(ClaimTypes.Email)
        ?? User?.FindFirstValue("email");

    public string? FirstName => User?.FindFirstValue(ClaimTypes.GivenName)
        ?? User?.FindFirstValue("given_name");

    public string? LastName => User?.FindFirstValue(ClaimTypes.Surname)
        ?? User?.FindFirstValue("family_name");

    public string? Name => User?.Identity?.Name
                           ?? User?.FindFirstValue("name")
                           ?? User?.FindFirstValue(ClaimTypes.Name);

    public string? Username => User?.FindFirstValue("preferred_username")
        ?? User?.FindFirstValue("username");

    public string? ClientId => User?.FindFirstValue("client_id")
        ?? User?.FindFirstValue("clientId")
        ?? User?.FindFirstValue("azp"); // Keycloak uses "azp" for authorized party

    public bool IsMachine => ClientId != null && UserId == null;

    public bool IsAuthenticated => User?.Identity?.IsAuthenticated ?? false;

    public IReadOnlyList<string> Roles
    {
        get
        {
            if (User == null) return [];

            // Check multiple role claim types to support all providers
            // Duende uses "role", Keycloak/FusionAuth use "roles"
            var roles = new HashSet<string>();

            foreach (var claim in User.Claims)
            {
                if (claim.Type is "role" or "roles" or ClaimTypes.Role)
                {
                    roles.Add(claim.Value);
                }
            }

            return roles.ToList();
        }
    }

    public bool IsInRole(string role)
    {
        if (User == null) return false;

        // First check the built-in method (respects RoleClaimType configuration)
        return User.IsInRole(role) ||
               // Also check our aggregated roles for cross-provider compatibility
               Roles.Contains(role, StringComparer.OrdinalIgnoreCase);
    }

    public string? GetClaimValue(string claimType)
    {
        return User?.FindFirstValue(claimType);
    }

    public IReadOnlyList<string> GetClaimValues(string claimType)
    {
        if (User == null) return [];

        return User.Claims
            .Where(c => c.Type == claimType)
            .Select(c => c.Value)
            .ToList();
    }
}
