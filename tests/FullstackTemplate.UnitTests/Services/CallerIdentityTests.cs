namespace FullstackTemplate.UnitTests.Services;

using FullstackTemplate.Server.Domain;
using FullstackTemplate.Server.Domain.Agents;
using FullstackTemplate.Server.Domain.Tenants;
using FullstackTemplate.Server.Services;
using Shouldly;

public sealed class CallerIdentityTests
{
    private static readonly string TenantId = EntityId.New(Tenant.IdPrefix);
    private static readonly string AgentId = EntityId.New(Agent.IdPrefix);

    public static IEnumerable<Func<(string Case, CallerIdentity Identity, bool CanManage)>> ManageAllTenantsCases()
    {
        yield return () => ("system work", new CallerIdentity.System(), true);
        yield return () => ("client on the manage list", new CallerIdentity.AllTenantsClient("sync", CanManage: true), true);
        yield return () => ("client on the read list", new CallerIdentity.AllTenantsClient("report", CanManage: false), false);
        yield return () => ("user", new CallerIdentity.User("user-1", TenantId), false);
        yield return () => ("delegated agent",
            new CallerIdentity.DelegatedAgent(AgentId, "clinic-assistant", "user-1", TenantId), false);
        yield return () => ("machine agent", new CallerIdentity.MachineAgent(AgentId, "fleet-sync", TenantId), false);
        yield return () => ("unknown client", new CallerIdentity.UnknownClient("m2m"), false);
        yield return () => ("anonymous", new CallerIdentity.Anonymous(), false);
    }

    [Test]
    [MethodDataSource(nameof(ManageAllTenantsCases))]
    public void only_system_work_and_a_client_on_the_manage_list_can_manage_all_tenants(
        string @case, CallerIdentity identity, bool canManage)
    {
        // Act & Assert
        identity.CanManageAllTenants.ShouldBe(canManage);
    }
}
