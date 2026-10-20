namespace FullstackTemplate.Server.Resources.Entrypoints;

using System.Security.Claims;
using Services;

/// <summary>
/// Gives the core the principal of the current HTTP request. HTTP, gRPC, and MCP each run in an HTTP request, and
/// the JWT bearer handler sets its user, so all three transports give the same caller.
/// </summary>
public sealed class HttpContextPrincipalAccessor(IHttpContextAccessor httpContextAccessor) : ICurrentPrincipalAccessor
{
    public ClaimsPrincipal? Principal => httpContextAccessor.HttpContext?.User;
}
