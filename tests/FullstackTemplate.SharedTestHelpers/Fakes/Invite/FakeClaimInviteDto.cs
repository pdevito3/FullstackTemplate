namespace FullstackTemplate.SharedTestHelpers.Fakes.Invite;

using AutoBogus;
using FullstackTemplate.Server.Domain.Invites.Dtos;
using User;

public sealed class FakeClaimInviteDto : AutoFaker<ClaimInviteDto>
{
    /// <param name="token">The token of the invite that the test claims.</param>
    public FakeClaimInviteDto(string token)
    {
        RuleFor(x => x.Token, _ => token);
        RuleFor(x => x.FirstName, UserRules.FirstName);
        RuleFor(x => x.LastName, UserRules.LastName);
        RuleFor(x => x.Email, UserRules.Email);
        RuleFor(x => x.Username, UserRules.Username);
    }
}
