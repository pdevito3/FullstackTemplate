namespace FullstackTemplate.Server.Services;

using Microsoft.Extensions.Options;

public interface ICallerIdentityResolver
{
    /// <summary>
    /// The identity of the current caller. It is synchronous, because EF Core reads the tenant query filter
    /// synchronously. <see cref="Resources.CallerIdentityBehavior{TRequest,TResponse}"/> awaits <see cref="GetAsync"/> before
    /// each MediatR handler, so in a handler this reads a finished resolution. It blocks on the lookups only when it is
    /// the first read in a DI scope, for example in code that queries outside a MediatR request, or when the caller of
    /// the scope changed since the last read.
    /// </summary>
    CallerIdentity Current { get; }

    /// <summary>
    /// The same value as <see cref="Current"/>, without a block. The first read in a DI scope gives
    /// <paramref name="cancellationToken"/> to the lookups.
    /// </summary>
    Task<CallerIdentity> GetAsync(CancellationToken cancellationToken);
}

/// <summary>
/// The one place that reads a token as an identity. The rule, in order:
/// <list type="number">
/// <item>A system scope is <see cref="CallerIdentity.System"/>.</item>
/// <item>A request with no authenticated principal is <see cref="CallerIdentity.Anonymous"/>.</item>
/// <item>A token with no client claim is an <see cref="CallerIdentity.UnknownClient"/> with no client ID.</item>
/// <item>A token from a first-party client with a <c>sub</c> is <see cref="CallerIdentity.User"/>.</item>
/// <item>A client on <see cref="TenancyOptions.ManageAllTenantsClientIds"/> or
/// <see cref="TenancyOptions.ReadAllTenantsClientIds"/> is an <see cref="CallerIdentity.AllTenantsClient"/>, whatever
/// the <c>sub</c>.</item>
/// <item>A client with a live machine agent in a live tenant is a <see cref="CallerIdentity.MachineAgent"/>, whatever
/// the <c>sub</c>.</item>
/// <item>A token whose <c>sub</c> names a live app user in tenant T is a
/// <see cref="CallerIdentity.DelegatedAgent"/> when T has a live delegated agent for its client.</item>
/// <item>Any other token is an <see cref="CallerIdentity.UnknownClient"/>.</item>
/// </list>
/// The operator's bindings (the allow-lists and the machine agents) come before the <c>sub</c>, because a tenant admin
/// writes the identifiers of users. A user record with the service-account <c>sub</c> of a client cannot take the
/// client from its binding.
/// </summary>
public sealed class CallerIdentityResolver(
    ICurrentUserService currentUserService,
    ITenantIdProvider tenantIdProvider,
    IOptions<TenancyOptions> tenancyOptions,
    IOptions<FirstPartyClientOptions> firstPartyClients,
    IAgentDirectory agentDirectory,
    SystemTenantAccess systemTenantAccess) : ICallerIdentityResolver
{
    private (Token Token, Task<CallerIdentity> Resolution)? _kept;

    public CallerIdentity Current => Resolution(CancellationToken.None).GetAwaiter().GetResult();

    public Task<CallerIdentity> GetAsync(CancellationToken cancellationToken)
        => Resolution(cancellationToken).WaitAsync(cancellationToken);

    private Task<CallerIdentity> Resolution(CancellationToken cancellationToken)
    {
        var token = new Token(
            systemTenantAccess.IsGranted,
            currentUserService.IsAuthenticated,
            currentUserService.UserIdentifier,
            currentUserService.ClientId);

        if (_kept is not { } kept || kept.Token != token || kept.Resolution.IsCanceled)
        {
            kept = (token, ResolveAsync(token, cancellationToken));
            _kept = kept;
        }

        return kept.Resolution;
    }

    private async Task<CallerIdentity> ResolveAsync(Token token, CancellationToken cancellationToken)
    {
        if (token.IsSystem)
            return new CallerIdentity.System();

        if (!token.IsAuthenticated)
            return new CallerIdentity.Anonymous();

        if (token.ClientId is not { } clientId)
            return new CallerIdentity.UnknownClient(null);

        if (token.Subject is { } subject && firstPartyClients.Value.Contains(clientId))
            return new CallerIdentity.User(subject, await tenantIdProvider.GetTenantIdAsync(subject, cancellationToken));

        var tenancy = tenancyOptions.Value;
        if (tenancy.ManageAllTenantsClientIds.Contains(clientId, StringComparer.Ordinal))
            return new CallerIdentity.AllTenantsClient(clientId, CanManage: true);

        if (tenancy.ReadAllTenantsClientIds.Contains(clientId, StringComparer.Ordinal))
            return new CallerIdentity.AllTenantsClient(clientId, CanManage: false);

        if (await agentDirectory.FindMachineAgentAsync(clientId, cancellationToken) is { } machineAgent)
            return new CallerIdentity.MachineAgent(machineAgent.AgentId, clientId, machineAgent.TenantId);

        if (token.Subject is { } delegatingUser
            && await tenantIdProvider.GetTenantIdAsync(delegatingUser, cancellationToken) is { } tenantId
            && await agentDirectory.FindDelegatedAgentAsync(tenantId, clientId, cancellationToken) is { } agentId)
            return new CallerIdentity.DelegatedAgent(agentId, clientId, delegatingUser, tenantId);

        return new CallerIdentity.UnknownClient(clientId);
    }

    private sealed record Token(bool IsSystem, bool IsAuthenticated, string? Subject, string? ClientId);
}
