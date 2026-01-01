namespace FullstackTemplate.SharedTestHelpers.Fakes.Tenant;

using AutoBogus;
using FullstackTemplate.Server.Domain.Tenants.Dtos;

public sealed class FakeTenantForCreationDto : AutoFaker<TenantForCreationDto>
{
    public FakeTenantForCreationDto()
    {
        RuleFor(x => x.Name, f => f.Company.CompanyName());
    }
}
