namespace FullstackTemplate.Server.Examples.Workshops.DomainEvents;

using Domain;

public sealed record WorkshopStatusChanged(string WorkshopId, string Status) : DomainEvent;
