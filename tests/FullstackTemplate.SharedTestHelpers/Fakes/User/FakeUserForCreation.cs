namespace FullstackTemplate.SharedTestHelpers.Fakes.User;

using AutoBogus;
using FullstackTemplate.Server.Domain.Users.Models;

public sealed class FakeUserForCreation : AutoFaker<UserForCreation>
{
    public FakeUserForCreation()
    {
        // A null ID makes the domain factory generate a new ID.
        RuleFor(x => x.Id, _ => null);
        RuleFor(x => x.TenantId, _ => TestContext.DefaultTenantId);
        RuleFor(x => x.FirstName, UserRules.FirstName);
        RuleFor(x => x.LastName, UserRules.LastName);
        RuleFor(x => x.Identifier, UserRules.Identifier);
        RuleFor(x => x.Email, UserRules.Email);
        RuleFor(x => x.Username, UserRules.Username);
        RuleFor(x => x.Role, UserRules.Role);
    }
}
