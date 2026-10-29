namespace FullstackTemplate.Server.Examples.Workshops.DomainEvents;

using Domain;

public sealed record WorkshopUpdated(string WorkshopId) : DomainEvent;
