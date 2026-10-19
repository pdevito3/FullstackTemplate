namespace FullstackTemplate.SharedTestHelpers.Fakes.User;

using AutoBogus;
using FullstackTemplate.Server.Domain.Users.Dtos;

public sealed class FakeUserForCreationDto : AutoFaker<UserForCreationDto>
{
    public FakeUserForCreationDto()
    {
        // A user adds to their own tenant, so the default request names no tenant.
        RuleFor(x => x.TenantId, _ => null);
        RuleFor(x => x.FirstName, UserRules.FirstName);
        RuleFor(x => x.LastName, UserRules.LastName);
        RuleFor(x => x.Identifier, UserRules.Identifier);
        RuleFor(x => x.Email, UserRules.Email);
        RuleFor(x => x.Username, UserRules.Username);
        RuleFor(x => x.Role, UserRules.Role);
    }
}
