namespace FullstackTemplate.SharedTestHelpers.Fakes.Invite;

using AutoBogus;
using FullstackTemplate.Server.Domain.Invites.Dtos;

public sealed class FakeInviteForCreationDto : AutoFaker<InviteForCreationDto>
{
    public FakeInviteForCreationDto()
    {
        // A user invites to their own tenant, so the default request names no tenant.
        RuleFor(x => x.TenantId, _ => null);
        RuleFor(x => x.Role, InviteRules.Role);
        RuleFor(x => x.Email, InviteRules.Email);
        RuleFor(x => x.RequireEmailMatch, InviteRules.RequireEmailMatch);
    }
}
