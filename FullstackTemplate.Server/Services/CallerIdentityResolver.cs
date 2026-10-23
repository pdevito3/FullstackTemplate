namespace FullstackTemplate.Server.Services;

using Microsoft.Extensions.Options;

public interface ICallerIdentityResolver
{
    /// <summary>
    /// The identity of the current caller. It is synchronous, because EF Core reads the tenant query filter
    /// synchronously. The first read in a DI scope resolves the identity and blocks on its lookups. Later reads give
    /// the kept value, unless the caller of the scope changed.
    /// </summary>
    CallerIdentity Current { get; }

    /// <summary>The same value as <see cref="Current"/>, without a block.</summary>
    Task<CallerIdentity> GetAsync(CancellationToken cancellationToken);
}

/// <summary>
/// The one place that reads a token as an identity. The rule, in order:
/// <list type="number">
/// <item>A system scope is <see cref="CallerIdentity.System"/>.</item>
/// <item>A request with no authenticated principal is <see cref="CallerIdentity.Anonymous"/>.</item>
/// <item>A token from a first-party client with a <c>sub</c> is <see cref="CallerIdentity.User"/>.</item>
/// <item>A token whose <c>sub</c> names a live app user is an <see cref="CallerIdentity.UnknownClient"/>, because no
/// agent is registered for its client. It never takes the allow-list path.</item>
/// <item>A token with no app user (no <c>sub</c>, or a service-account <c>sub</c>) on an allow-list is an
/// <see cref="CallerIdentity.AllTenantsClient"/>. Any other token is an <see cref="CallerIdentity.UnknownClient"/>.</item>
/// </list>
/// The rule does not depend on a missing <c>sub</c>, so a client_credentials token from Keycloak, ZeroFactor, or
/// Cognito, which carries a service-account <c>sub</c>, also matches an allow-list.
/// </summary>
public sealed class CallerIdentityResolver(
    ICurrentUserService currentUserService,
    ITenantIdProvider tenantIdProvider,
    IOptions<TenancyOptions> tenancyOptions,
    IOptions<FirstPartyClientOptions> firstPartyClients,
    SystemTenantAccess systemTenantAccess) : ICallerIdentityResolver
{
    private (Token Token, Task<CallerIdentity> Resolution)? _kept;

    public CallerIdentity Current => Resolution().GetAwaiter().GetResult();

    public Task<CallerIdentity> GetAsync(CancellationToken cancellationToken)
        => Resolution().WaitAsync(cancellationToken);

    // A DI scope serves one caller, but a test can switch the caller of its scope. The facts of the token are the key,
    // so a new caller gets a new resolution.
    private Task<CallerIdentity> Resolution()
    {
        var token = new Token(
            systemTenantAccess.IsGranted,
            currentUserService.IsAuthenticated,
            currentUserService.UserIdentifier,
            currentUserService.ClientId);

        if (_kept is not { } kept || kept.Token != token)
        {
            kept = (token, ResolveAsync(token));
            _kept = kept;
        }

        return kept.Resolution;
    }

    private async Task<CallerIdentity> ResolveAsync(Token token)
    {
        if (token.IsSystem)
            return new CallerIdentity.System();

        if (!token.IsAuthenticated)
            return new CallerIdentity.Anonymous();

        // A token with no client claim could come from any client of the issuer, so it fails closed.
        if (token.ClientId is not { } clientId)
            return new CallerIdentity.UnknownClient(null);

        if (token.Subject is { } subject)
        {
            if (firstPartyClients.Value.Contains(clientId))
                return new CallerIdentity.User(subject, await tenantIdProvider.GetTenantIdAsync(subject));

            if (await tenantIdProvider.GetTenantIdAsync(subject) is not null)
                return new CallerIdentity.UnknownClient(clientId);
        }

        var tenancy = tenancyOptions.Value;
        if (tenancy.ManageAllTenantsClientIds.Contains(clientId, StringComparer.Ordinal))
            return new CallerIdentity.AllTenantsClient(clientId, CanManage: true);

        if (tenancy.ReadAllTenantsClientIds.Contains(clientId, StringComparer.Ordinal))
            return new CallerIdentity.AllTenantsClient(clientId, CanManage: false);

        return new CallerIdentity.UnknownClient(clientId);
    }

    private sealed record Token(bool IsSystem, bool IsAuthenticated, string? Subject, string? ClientId);
}
