namespace FullstackTemplate.SharedTestHelpers.Fakes.User;

using AutoBogus;
using FullstackTemplate.Server.Domain.Users.Dtos;

public sealed class FakeUserForUpdateDto : AutoFaker<UserForUpdateDto>
{
    public FakeUserForUpdateDto()
    {
        RuleFor(x => x.FirstName, UserRules.FirstName);
        RuleFor(x => x.LastName, UserRules.LastName);
        RuleFor(x => x.Email, UserRules.Email);
        RuleFor(x => x.Username, UserRules.Username);
        // A test sets the version of the record that it loaded. No row has version 0, so a test that does not set it gets 409.
        RuleFor(x => x.Version, _ => 0u);
    }
}
