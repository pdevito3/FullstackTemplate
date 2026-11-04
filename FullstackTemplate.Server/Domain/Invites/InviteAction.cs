namespace FullstackTemplate.Server.Domain.Invites;

using Ardalis.SmartEnum;
using Exceptions;
using Users;

/// <summary>
/// A command that a caller can send for an invite. <see cref="Invite.AllowedActions"/> and the command read one
/// table, so a button in the SPA and the handler cannot disagree.
/// </summary>
public partial class InviteAction : ValueObject, IFixedNames
{
    private readonly InviteActionEnum _action;

    private InviteAction(InviteActionEnum action) => _action = action;

    public string Value => _action.Name;

    public override string ToString() => Value;

    public static List<string> ListNames() => InviteActionEnum.List.OrderBy(x => x.Value).Select(x => x.Name).ToList();

    public static InviteAction Revoke() => new(InviteActionEnum.Revoke);

    /// <summary>Call it before the load, so a caller without the permission gets a 403 for any ID.</summary>
    /// <exception cref="ForbiddenAccessException">The caller lacks the permission.</exception>
    public void GuardCaller(Caller caller) => caller.Require(_action.Permission);

    internal static IReadOnlyList<InviteAction> AllowedFor(Caller caller, InviteStatus status)
        => InviteActionEnum.List
            .OrderBy(action => action.Value)
            .Where(action => caller.Has(action.Permission) && action.AppliesTo(status))
            .Select(action => new InviteAction(action))
            .ToList();

    private abstract class InviteActionEnum(string name, int value) : SmartEnum<InviteActionEnum>(name, value)
    {
        public static readonly InviteActionEnum Revoke = new MoveType(nameof(Revoke), 0, InviteStatus.Revoked());

        public Permission Permission { get; } = Permission.ManageUsers();

        public abstract bool AppliesTo(InviteStatus status);

        private sealed class MoveType(string name, int value, InviteStatus target) : InviteActionEnum(name, value)
        {
            public override bool AppliesTo(InviteStatus status) => status.CanMoveTo(target);
        }
    }
}
