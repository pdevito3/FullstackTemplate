namespace FullstackTemplate.SharedTestHelpers.Fakes.Tenant;

using AutoBogus;
using FullstackTemplate.Server.Domain.Tenants.Models;

public sealed class FakeTenantForCreation : AutoFaker<TenantForCreation>
{
    public FakeTenantForCreation()
    {
        RuleFor(x => x.Name, f => f.Company.CompanyName());
    }
}
