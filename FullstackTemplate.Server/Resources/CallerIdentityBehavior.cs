namespace FullstackTemplate.Server.Resources;

using System.Diagnostics;
using MediatR;
using Serilog.Context;
using Serilog.Core;
using Serilog.Core.Enrichers;
using Services;

/// <summary>
/// Resolves the caller before the handler runs. The tenant query filter reads
/// <see cref="ICallerIdentityResolver.Current"/> synchronously, and the first read in a DI scope blocks a thread on
/// the identity lookups. This behavior awaits <see cref="ICallerIdentityResolver.GetAsync"/> first, with the
/// cancellation token of the request, so the filter of each query in the handler reads a finished resolution.
/// <para>
/// It also puts the caller on the feature slice span and on each log that the handler writes, so support can find
/// the requests of one tenant, user, or agent. The values are ids only (see the privacy rule in <see cref="Telemetry"/>).
/// </para>
/// </summary>
public sealed class CallerIdentityBehavior<TRequest, TResponse>(ICallerIdentityResolver callerIdentityResolver)
    : IPipelineBehavior<TRequest, TResponse>
    where TRequest : notnull
{
    public async Task<TResponse> Handle(
        TRequest request, RequestHandlerDelegate<TResponse> next, CancellationToken cancellationToken)
    {
        var caller = await callerIdentityResolver.GetAsync(cancellationToken);
        var properties = TelemetryProperties(caller);

        // RequestTracingBehavior runs first, so the current activity is the feature slice span
        foreach (var (tag, _, value) in properties)
            Activity.Current?.SetTag(tag, value);

        ILogEventEnricher[] enrichers = [.. properties.Select(p => new PropertyEnricher(p.LogProperty, p.Value))];
        using (LogContext.Push(enrichers))
            return await next(cancellationToken);
    }

    private static List<(string Tag, string LogProperty, string Value)> TelemetryProperties(CallerIdentity caller)
    {
        (string Kind, string? Subject, string? ClientId, string? AgentId, string? TenantId) values = caller switch
        {
            CallerIdentity.System => ("system", null, null, null, null),
            CallerIdentity.AllTenantsClient client => ("all_tenants_client", null, client.ClientId, null, null),
            CallerIdentity.User user => ("user", user.Subject, null, null, user.LiveUser?.TenantId),
            CallerIdentity.DelegatedAgent agent => ("delegated_agent", agent.Subject, agent.ClientId, agent.AgentId, agent.LiveUser.TenantId),
            CallerIdentity.MachineAgent agent => ("machine_agent", null, agent.ClientId, agent.AgentId, agent.TenantId),
            CallerIdentity.UnknownClient client => ("unknown_client", null, client.ClientId, null, null),
            CallerIdentity.Anonymous => ("anonymous", null, null, null, null),
            _ => throw new UnreachableException($"No telemetry for the caller type {caller.GetType().Name}"),
        };

        List<(string Tag, string LogProperty, string Value)> properties = [("app.caller.kind", "CallerKind", values.Kind)];
        if (values.Subject is not null)
            properties.Add(("app.caller.subject", "CallerSubject", values.Subject));
        if (values.ClientId is not null)
            properties.Add(("app.caller.client_id", "CallerClientId", values.ClientId));
        if (values.AgentId is not null)
            properties.Add(("app.caller.agent_id", "CallerAgentId", values.AgentId));
        if (values.TenantId is not null)
            properties.Add(("app.caller.tenant_id", "CallerTenantId", values.TenantId));
        return properties;
    }
}
