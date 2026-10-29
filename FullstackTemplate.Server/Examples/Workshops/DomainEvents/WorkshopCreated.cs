namespace FullstackTemplate.Server.Examples.Workshops.DomainEvents;

using Domain;

public sealed record WorkshopCreated(string WorkshopId) : DomainEvent;
