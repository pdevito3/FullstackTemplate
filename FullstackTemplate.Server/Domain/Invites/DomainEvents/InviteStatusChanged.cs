namespace FullstackTemplate.Server.Domain.Invites.DomainEvents;

public sealed record InviteStatusChanged(string InviteId, string Status) : DomainEvent;
