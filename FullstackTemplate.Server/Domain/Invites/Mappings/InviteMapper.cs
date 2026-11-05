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
    public static InviteDto ToInviteDto(this Invite invite, Caller caller, DateTimeOffset now)
        => invite.ToInviteDtoWithoutAnswers() with
        {
            AllowedActions = NamesOf(invite.AllowedActions(caller)),
            IsExpired = invite.IsExpiredAt(now),
        };

    /// <summary>
    /// Gives each row of a list the actions that <paramref name="caller"/> can send for it, and its expired flag at
    /// <paramref name="now"/>. The list query projects to the DTO, so each rule reads the values of the DTO. The
    /// projection cannot compute the flag, because Mapperly cannot give a value of the handler to a query.
    /// </summary>
    public static void AddAllowedActionsAndExpiry(this List<InviteDto> invites, Caller caller, DateTimeOffset now)
    {
        for (var i = 0; i < invites.Count; i++)
        {
            var status = InviteStatus.Of(invites[i].Status);
            invites[i] = invites[i] with
            {
                AllowedActions = NamesOf(InviteAction.AllowedFor(caller, status)),
                IsExpired = Invite.IsExpiredAt(status, invites[i].ExpiresAt, now),
            };
        }
    }

    // It takes the configuration of ToInviteDtoWithoutAnswers. The list handler adds the answers after the page loads.
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
    [MapperIgnoreTarget(nameof(InviteDto.IsExpired))]
    private static partial InviteDto ToInviteDtoWithoutAnswers(this Invite invite);

    private static List<string> NamesOf(IEnumerable<InviteAction> actions) => actions.Select(a => a.Value).ToList();
    private static string MapEmail(EmailAddress email) => email.Value;
    private static string MapRole(UserRole role) => role.Value;
    private static string MapStatus(InviteStatus status) => status.Value;
}
