namespace FullstackTemplate.SharedTestHelpers.Fakes.User;

using AutoBogus;
using FullstackTemplate.Server.Domain.Users.Dtos;
using Tenant;

public sealed class FakeInitiateUserAndTenantDto : AutoFaker<InitiateUserAndTenantDto>
{
    public FakeInitiateUserAndTenantDto()
    {
        RuleFor(x => x.TenantName, TenantRules.Name);
        RuleFor(x => x.FirstName, UserRules.FirstName);
        RuleFor(x => x.LastName, UserRules.LastName);
        RuleFor(x => x.Identifier, UserRules.Identifier);
        RuleFor(x => x.Email, UserRules.Email);
        RuleFor(x => x.Username, UserRules.Username);
        RuleFor(x => x.Role, UserRules.Role);
    }
}
