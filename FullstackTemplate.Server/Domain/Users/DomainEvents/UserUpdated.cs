namespace FullstackTemplate.Server.Domain.Users.DomainEvents;

public sealed record UserUpdated(string Id) : DomainEvent;
