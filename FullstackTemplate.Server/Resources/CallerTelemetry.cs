namespace FullstackTemplate.Server.Resources;

using System.Diagnostics;
using Services;

/// <summary>
/// The span tags and the log properties of a caller. The values are ids only (see the privacy rule in
/// <see cref="Telemetry"/>). A value that the caller does not have is not in the list.
/// </summary>
public static class CallerTelemetry
{
    public static List<(string Tag, string LogProperty, string Value)> Properties(CallerIdentity caller)
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
