namespace FullstackTemplate.Server.Domain.Users.DomainEvents;

public sealed record UserUpdated(Guid Id) : DomainEvent;
