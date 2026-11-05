namespace FullstackTemplate.Server.Domain.Invites;

using Ardalis.SmartEnum;
using Exceptions;

/// <summary>
/// The stored state of an invite. Expired is not a state: <see cref="Invite.IsExpiredAt(DateTimeOffset)"/> computes it from the expiry.
/// </summary>
public partial class InviteStatus : ValueObject, IFixedNames
{
    private const string RequestField = "status";

    private InviteStatusEnum _status = null!;

    public string Value
    {
        get => _status.Name;
        private set
        {
            if (!InviteStatusEnum.TryFromName(value, true, out var parsed))
                throw new ValidationException(RequestField, $"Invalid invite status: {value}");

            _status = parsed;
        }
    }

    public InviteStatus(string value)
    {
        ValidationException.ThrowWhenNullOrWhitespace(value, RequestField, "Please provide an invite status.");
        Value = value;
    }

    public string Words => _status.Words;

    public bool CanMoveTo(InviteStatus target) => _status.Next.Contains(target._status);

    public InviteStatus MoveTo(InviteStatus target)
    {
        ValidationException.Must(CanMoveTo(target), RequestField, $"A {Words} invite cannot be {target.Words}.");
        return target;
    }

    public override string ToString() => Value;

    public static InviteStatus Of(string value) => new(value);
    public static List<string> ListNames() => InviteStatusEnum.List.OrderBy(x => x.Value).Select(x => x.Name).ToList();

    public static InviteStatus Pending() => new(InviteStatusEnum.Pending.Name);
    public static InviteStatus Claimed() => new(InviteStatusEnum.Claimed.Name);
    public static InviteStatus Revoked() => new(InviteStatusEnum.Revoked.Name);

    protected InviteStatus() { } // EF Core

    private abstract class InviteStatusEnum(string name, int value, string words)
        : SmartEnum<InviteStatusEnum>(name, value)
    {
        public static readonly InviteStatusEnum Pending = new PendingType();
        public static readonly InviteStatusEnum Claimed = new ClaimedType();
        public static readonly InviteStatusEnum Revoked = new RevokedType();

        public string Words { get; } = words;

        public abstract IReadOnlyList<InviteStatusEnum> Next { get; }

        private sealed class PendingType() : InviteStatusEnum("Pending", 0, "pending")
        {
            public override IReadOnlyList<InviteStatusEnum> Next => [Claimed, Revoked];
        }

        private sealed class ClaimedType() : InviteStatusEnum("Claimed", 1, "claimed")
        {
            public override IReadOnlyList<InviteStatusEnum> Next => [];
        }

        private sealed class RevokedType() : InviteStatusEnum("Revoked", 2, "revoked")
        {
            public override IReadOnlyList<InviteStatusEnum> Next => [];
        }
    }
}
