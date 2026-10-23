namespace FullstackTemplate.UnitTests.Domain.Agents;

using FullstackTemplate.Server.Domain;
using FullstackTemplate.Server.Domain.Agents;
using FullstackTemplate.Server.Domain.Agents.Models;
using FullstackTemplate.Server.Domain.Tenants;
using FullstackTemplate.Server.Domain.Users;
using FullstackTemplate.Server.Exceptions;
using Shouldly;

/// <summary>
/// A grantor can give or take away only the permissions that it holds, and only a caller that manages all tenants
/// can bind a machine client to a tenant.
/// </summary>
public class AgentTests
{
    private static readonly Caller.Member TenantAdmin = new("usr_admin", Permission.GetAll());

    [Test]
    public void tenant_admin_creates_a_delegated_agent_with_its_first_grant()
    {
        // Arrange
        var forCreation = ForCreation() with { Permissions = [Permission.Names.ManageTenant] };

        // Act
        var agent = Agent.Create(forCreation, TenantAdmin);

        // Assert
        agent.TenantId.ShouldBe(forCreation.TenantId);
        agent.Name.ShouldBe(forCreation.Name);
        agent.ClientId.ShouldBe(forCreation.ClientId);
        agent.Mode.ShouldBe(AgentMode.Delegated());
        agent.Mode.IsMachine.ShouldBeFalse();
        agent.Permissions.Select(p => p.Permission).ShouldBe([Permission.ManageTenant()]);
    }

    [Test]
    public void tenant_admin_cannot_create_a_machine_agent()
    {
        // Arrange
        var forCreation = ForCreation() with { Mode = "machine" };

        // Act
        var act = () => Agent.Create(forCreation, TenantAdmin);

        // Assert
        act.ShouldThrow<ForbiddenAccessException>().Message
            .ShouldBe("Only a client that can manage all tenants can do this.");
    }

    [Test]
    public void caller_that_manages_all_tenants_creates_a_machine_agent()
    {
        // Arrange
        var forCreation = ForCreation() with { Mode = "Machine", Permissions = [Permission.Names.ManageUsers] };

        // Act
        var agent = Agent.Create(forCreation, Caller.AllTenantsManager.Instance);

        // Assert
        agent.Mode.Value.ShouldBe("machine");
        agent.Mode.IsMachine.ShouldBeTrue();
        agent.Permissions.Select(p => p.Permission).ShouldBe([Permission.ManageUsers()]);
    }

    [Test]
    public void first_grant_cannot_hold_a_permission_that_the_grantor_lacks()
    {
        // Arrange
        var grantor = new Caller.Member("usr_member", [Permission.ManageAgents()]);
        var forCreation = ForCreation() with { Permissions = [Permission.Names.ManageTenant] };

        // Act
        var act = () => Agent.Create(forCreation, grantor);

        // Assert
        act.ShouldThrow<ForbiddenAccessException>().Message
            .ShouldBe("You need the manage_tenant permission to do this.");
    }

    [Test]
    public void grant_above_the_grantor_is_denied_and_changes_nothing()
    {
        // Arrange
        var agent = Agent.Create(ForCreation(), TenantAdmin);
        var grantor = new Caller.Member("usr_member", [Permission.ManageAgents()]);

        // Act
        var act = () => agent.Grant(Permission.ManageUsers(), grantor);

        // Assert
        act.ShouldThrow<ForbiddenAccessException>().Message
            .ShouldBe("You need the manage_users permission to do this.");
        agent.Permissions.ShouldBeEmpty();
    }

    [Test]
    public void second_grant_of_a_permission_keeps_one_row()
    {
        // Arrange
        var agent = Agent.Create(ForCreation(), TenantAdmin);

        // Act
        agent.Grant(Permission.ManageTenant(), TenantAdmin);
        agent.Grant(Permission.ManageTenant(), TenantAdmin);

        // Assert
        var row = agent.Permissions.ShouldHaveSingleItem();
        row.Permission.ShouldBe(Permission.ManageTenant());
        row.AgentId.ShouldBe(agent.Id);
    }

    [Test]
    public void revoke_takes_the_permission_out_of_the_grant()
    {
        // Arrange
        var agent = Agent.Create(ForCreation() with
        {
            Permissions = [Permission.Names.ManageTenant, Permission.Names.ManageUsers]
        }, TenantAdmin);

        // Act
        agent.Revoke(Permission.ManageTenant(), TenantAdmin);

        // Assert
        agent.Permissions.Select(p => p.Permission).ShouldBe([Permission.ManageUsers()]);
    }

    [Test]
    public void revoke_of_a_permission_that_the_grantor_lacks_is_denied_and_changes_nothing()
    {
        // Arrange
        var agent = Agent.Create(ForCreation() with { Permissions = [Permission.Names.ManageTenant] }, TenantAdmin);
        var grantor = new Caller.Member("usr_member", [Permission.ManageAgents()]);

        // Act
        var act = () => agent.Revoke(Permission.ManageTenant(), grantor);

        // Assert
        act.ShouldThrow<ForbiddenAccessException>().Message
            .ShouldBe("You need the manage_tenant permission to do this.");
        agent.Permissions.Select(p => p.Permission).ShouldBe([Permission.ManageTenant()]);
    }

    [Test]
    public void fixed_id_is_kept_and_each_grant_row_copies_it()
    {
        // Arrange
        var forCreation = ForCreation() with { Id = "agt_fixed", Permissions = [Permission.Names.ManageTenant] };

        // Act
        var agent = Agent.Create(forCreation, TenantAdmin);

        // Assert
        agent.Id.ShouldBe("agt_fixed");
        agent.Permissions.ShouldHaveSingleItem().AgentId.ShouldBe("agt_fixed");
    }

    public static IEnumerable<Func<(AgentForCreation, string, string)>> InvalidFields()
    {
        yield return () => (ForCreation() with { TenantId = null! }, "tenantId", "Please provide a tenant.");
        yield return () => (ForCreation() with { TenantId = "usr_0199f0c1a2b37c4d8e9f0a1b2c3d4e5f" }, "tenantId",
            "Please provide a valid tenant ID.");
        yield return () => (ForCreation() with { Name = " " }, "name", "Please provide a name.");
        yield return () => (ForCreation() with { ClientId = "" }, "clientId", "Please provide a client ID.");
        yield return () => (ForCreation() with { ClientId = "clinic-assistant " }, "clientId",
            "A client ID cannot start or end with a space.");
        yield return () => (ForCreation() with { Mode = "robot" }, "mode", "Invalid mode: robot. Use delegated or machine.");
        yield return () => (ForCreation() with { Mode = "" }, "mode", "Please provide a mode.");
        yield return () => (ForCreation() with { Permissions = ["manage_everything"] }, "permission",
            "Invalid permission: manage_everything");
    }

    [Test]
    [MethodDataSource(nameof(InvalidFields))]
    public void invalid_field_is_rejected_with_its_request_field(AgentForCreation forCreation, string field, string message)
    {
        // Act
        var act = () => Agent.Create(forCreation, TenantAdmin);

        // Assert
        var exception = act.ShouldThrow<ValidationException>();
        exception.Property.ShouldBe(field);
        exception.Message.ShouldBe(message);
    }

    private static AgentForCreation ForCreation() => new()
    {
        TenantId = EntityId.New(Tenant.IdPrefix),
        Name = "Clinic assistant",
        ClientId = "clinic-assistant",
        Mode = "delegated",
    };
}
