namespace FullstackTemplate.Server.Domain.Invites.DomainEvents;

public sealed record InviteCreated(string InviteId) : DomainEvent;
