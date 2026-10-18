namespace FullstackTemplate.UnitTests.Services;

using FullstackTemplate.Server.Services;
using Shouldly;

public class TenantScopeTests
{
    [Fact]
    public void all_tenants_includes_any_tenant()
    {
        TenantScope.AllTenants.Includes(Guid.NewGuid()).ShouldBeTrue();
    }

    [Fact]
    public void no_tenant_includes_no_tenant_and_not_the_empty_id()
    {
        TenantScope.NoTenant.Includes(Guid.NewGuid()).ShouldBeFalse();
        TenantScope.NoTenant.Includes(Guid.Empty).ShouldBeFalse();
    }

    [Fact]
    public void one_tenant_includes_only_that_tenant()
    {
        var tenantId = Guid.NewGuid();
        var scope = TenantScope.ForTenant(tenantId);

        scope.Includes(tenantId).ShouldBeTrue();
        scope.Includes(Guid.NewGuid()).ShouldBeFalse();
    }

    [Fact]
    public void one_tenant_scope_needs_a_tenant_id()
    {
        Should.Throw<ArgumentException>(() => TenantScope.ForTenant(Guid.Empty));
    }
}
