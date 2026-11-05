namespace FullstackTemplate.Server.Domain.Invites.Mappings;

using Dtos;
using EmailAddresses;
using Models;
using Riok.Mapperly.Abstractions;
using Users;

[Mapper]
public static partial class InviteMapper
{
    /// <summary>The DTO with the actions that <paramref name="caller"/> can send for the invite.</summary>
    public static InviteDto ToInviteDto(this Invite invite, Caller caller)
        => invite.ToInviteDtoWithoutActions() with { AllowedActions = NamesOf(invite.AllowedActions(caller)) };

    /// <summary>
    /// Gives each row of a list the actions that <paramref name="caller"/> can send for it. The list query projects
    /// to the DTO, so the rule reads the status of the DTO.
    /// </summary>
    public static void AddAllowedActions(this List<InviteDto> invites, Caller caller)
    {
        for (var i = 0; i < invites.Count; i++)
        {
            var status = InviteStatus.Of(invites[i].Status);
            invites[i] = invites[i] with { AllowedActions = NamesOf(InviteAction.AllowedFor(caller, status)) };
        }
    }

    // It takes the configuration of ToInviteDtoWithoutActions. The list handler adds the actions after the page loads.
    public static partial IQueryable<InviteDto> ToInviteDtoQueryable(this IQueryable<Invite> queryable);

    // The handler decides the tenant from the caller's scope. The tenant in the DTO is only a request.
    [MapperIgnoreSource(nameof(InviteForCreationDto.TenantId))]
    [MapperIgnoreTarget(nameof(InviteForCreation.Id))]
    public static partial InviteForCreation ToInviteForCreation(this InviteForCreationDto dto, string tenantId);

    [MapperIgnoreSource(nameof(Invite.TokenHash))]
    [MapperIgnoreSource(nameof(Invite.LastModifiedOn))]
    [MapperIgnoreSource(nameof(Invite.IsDeleted))]
    [MapperIgnoreSource(nameof(Invite.DomainEvents))]
    [MapperIgnoreTarget(nameof(InviteDto.AllowedActions))]
    private static partial InviteDto ToInviteDtoWithoutActions(this Invite invite);

    private static List<string> NamesOf(IEnumerable<InviteAction> actions) => actions.Select(a => a.Value).ToList();
    private static string MapEmail(EmailAddress email) => email.Value;
    private static string MapRole(UserRole role) => role.Value;
    private static string MapStatus(InviteStatus status) => status.Value;
}
