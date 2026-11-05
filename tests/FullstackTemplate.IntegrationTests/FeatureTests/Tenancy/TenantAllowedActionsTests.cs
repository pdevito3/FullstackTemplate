namespace FullstackTemplate.IntegrationTests.FeatureTests.Tenancy;

using FullstackTemplate.Server.Domain.Tenants;
using FullstackTemplate.Server.Domain.Tenants.Dtos;
using FullstackTemplate.Server.Domain.Tenants.Features;
using FullstackTemplate.Server.Domain.Users;
using FullstackTemplate.SharedTestHelpers.EntityGraphs;
using Shouldly;

/// <summary>The tenant tells the SPA whether the caller can invite a colleague, so a plain member sees no button.</summary>
public class TenantAllowedActionsTests : TestBase
{
    [Test, Property("Rule", "INV-1")]
    [Arguments("Admin", true)]
    [Arguments("User", false)]
    public async Task the_tenant_lists_invite_user_only_for_a_caller_with_manage_users(string role, bool canInvite)
    {
        // Arrange
        var scope = new TestingServiceScope();
        await scope.StartGraph(out _).AddTenant(out var tenant).AddUser(out var caller, u => u.Role = role).BuildAsync();
        scope.SetUserWithDefaults(identifier: caller.Identifier);
        string[] expected = canInvite ? [TenantAction.InviteUser().Value] : [];

        // Act
        var record = await scope.SendAsync(new GetTenant.Query(tenant.Id));
        var list = await scope.SendAsync(new GetTenantList.Query(TenantParametersDto.From(null, null, null, null)));

        // Assert
        record.AllowedActions.ShouldBe(expected);
        list.ShouldHaveSingleItem().AllowedActions.ShouldBe(expected);
        UserRole.Of(role).GetDefaultPermissions().Contains(Permission.ManageUsers()).ShouldBe(canInvite);
    }
}
