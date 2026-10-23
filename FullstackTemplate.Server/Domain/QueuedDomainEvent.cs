namespace FullstackTemplate.Server.Domain;

internal sealed record QueuedDomainEvent(long Sequence, DomainEvent Event);
