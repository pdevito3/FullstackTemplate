namespace FullstackTemplate.SharedTestHelpers.Fakes.User;

using AutoBogus;
using FullstackTemplate.Server.Domain.Users.Models;

public sealed class FakeUserForUpdate : AutoFaker<UserForUpdate>
{
    public FakeUserForUpdate()
    {
        RuleFor(x => x.FirstName, UserRules.FirstName);
        RuleFor(x => x.LastName, UserRules.LastName);
        RuleFor(x => x.Email, UserRules.Email);
        RuleFor(x => x.Username, UserRules.Username);
    }
}
