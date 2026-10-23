namespace FullstackTemplate.Server.Domain.Users.DomainEvents;

public sealed record UserCreated(string UserId) : DomainEvent;
