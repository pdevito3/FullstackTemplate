namespace FullstackTemplate.UnitTests.Services;

using FullstackTemplate.Server.Domain;
using FullstackTemplate.Server.Domain.Tenants;
using FullstackTemplate.Server.Services;
using Shouldly;

public class TenantScopeTests
{
    [Test]
    [Arguments("ten_0199f0c1a2b37c4d8e9f0a1b2c3d4e5f")]
    [Arguments(null)]
    public void all_tenants_includes_any_tenant_and_a_null_id(string? tenantId)
    {
        TenantScope.AllTenants.Includes(tenantId).ShouldBeTrue();
    }

    [Test]
    public void no_tenant_includes_no_tenant_and_not_the_empty_id()
    {
        TenantScope.NoTenant.Includes(EntityId.New(Tenant.IdPrefix)).ShouldBeFalse();
        TenantScope.NoTenant.Includes(string.Empty).ShouldBeFalse();
    }

    [Test]
    public void no_tenant_and_one_tenant_do_not_include_a_null_id()
    {
        TenantScope.NoTenant.Includes(null).ShouldBeFalse();
        TenantScope.ForTenant(EntityId.New(Tenant.IdPrefix)).Includes(null).ShouldBeFalse();
    }

    [Test]
    public void one_tenant_includes_only_that_tenant()
    {
        var tenantId = EntityId.New(Tenant.IdPrefix);
        var scope = TenantScope.ForTenant(tenantId);

        scope.Includes(tenantId).ShouldBeTrue();
        scope.Includes(EntityId.New(Tenant.IdPrefix)).ShouldBeFalse();
    }

    [Test]
    [Arguments("")]
    [Arguments("usr_0199f0c1a2b37c4d8e9f0a1b2c3d4e5f")]
    public void one_tenant_scope_needs_a_valid_tenant_id(string tenantId)
    {
        Should.Throw<ArgumentException>(() => TenantScope.ForTenant(tenantId));
    }
}
