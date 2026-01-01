namespace FullstackTemplate.SharedTestHelpers.Fakes.Tenant;

using FullstackTemplate.Server.Domain.Tenants;
using FullstackTemplate.Server.Domain.Tenants.Models;

public class FakeTenantBuilder
{
    private TenantForCreation _creationData = new FakeTenantForCreation().Generate();

    public FakeTenantBuilder WithName(string name)
    {
        _creationData = _creationData with { Name = name };
        return this;
    }

    public Tenant Build()
    {
        return Tenant.Create(_creationData);
    }
}
