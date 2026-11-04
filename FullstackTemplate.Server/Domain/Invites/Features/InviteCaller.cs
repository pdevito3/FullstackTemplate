namespace FullstackTemplate.Server.Domain.Invites.Features;

using Exceptions;
using Users;

/// <summary>The caller check of the preview and of the claim of an invite.</summary>
internal static class InviteCaller
{
    /// <summary>
    /// The <c>sub</c> of a user of a first-party client. A machine client or an agent cannot join a tenant with a link.
    /// </summary>
    /// <exception cref="ForbiddenAccessException">The caller is not a user of a first-party client.</exception>
    public static CallerIdentity.User RequireUser(CallerIdentity caller)
        => caller as CallerIdentity.User
            ?? throw new ForbiddenAccessException("Only a user who signs in through this app can open an invite.");
}
