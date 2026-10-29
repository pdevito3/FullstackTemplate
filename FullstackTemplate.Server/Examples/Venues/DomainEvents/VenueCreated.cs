namespace FullstackTemplate.Server.Examples.Venues.DomainEvents;

using Domain;

public sealed record VenueCreated(string VenueId) : DomainEvent;
