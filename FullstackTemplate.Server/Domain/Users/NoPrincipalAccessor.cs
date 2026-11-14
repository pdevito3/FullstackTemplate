namespace FullstackTemplate.Server.Domain.Users;

using System.Security.Claims;

/// <summary>
/// The principal accessor of a Server that serves no requests, such as a worker. Such a Server has no caller.
/// <c>AddEntrypoints</c> replaces it with the accessor of the HTTP request.
/// </summary>
public sealed class NoPrincipalAccessor : ICurrentPrincipalAccessor
{
    public ClaimsPrincipal? Principal => null;
}
