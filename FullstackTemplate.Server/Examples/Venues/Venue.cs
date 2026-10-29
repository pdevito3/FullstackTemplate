namespace FullstackTemplate.Server.Examples.Venues;

using Domain;
using Domain.Tenants;
using Domain.TimeZones;
using DomainEvents;
using Exceptions;
using Models;

public class Venue : BaseEntity, ITenantable
{
    public const string IdPrefix = "ven";
    public const int NameMaxLength = 200;

    [NotSensitive] public string TenantId { get; private set; } = default!;
    [NotSensitive] public string Name { get; private set; } = default!;
    [NotSensitive] public TimeZoneId TimeZone { get; private set; } = default!;

    public static Venue Create(VenueForCreation venueForCreation)
    {
        var venue = new Venue
        {
            TenantId = venueForCreation.TenantId,
            Name = venueForCreation.Name,
            TimeZone = TimeZoneId.Of(venueForCreation.TimeZone),
        };
        if (venueForCreation.Id is not null)
            venue.SetId(venueForCreation.Id);

        ValidateVenue(venue);
        venue.QueueDomainEvent(new VenueCreated(venue.Id));

        return venue;
    }

    private static void ValidateVenue(Venue venue)
    {
        ValidationException.Must(EntityId.IsValid(venue.TenantId, Tenant.IdPrefix), "tenantId", "Please provide a valid tenant ID.");
        ValidationException.ThrowWhenNullOrWhitespace(venue.Name, "name", "Please provide a venue name.");
        ValidationException.ThrowWhenLongerThan(venue.Name, NameMaxLength, "name",
            $"A venue name can have no more than {NameMaxLength} characters.");
        ValidationException.ThrowWhenHasCharacterPostgresCannotStore(venue.Name, "name",
            "A venue name has a character that is not allowed.");
    }

    protected Venue() : base(IdPrefix) { } // For EF Core
}
