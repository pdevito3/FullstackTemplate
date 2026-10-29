namespace FullstackTemplate.Server.Examples.Venues.Mappings;

using Domain.TimeZones;
using Dtos;
using Models;
using Riok.Mapperly.Abstractions;

[Mapper]
public static partial class VenueMapper
{
    [MapperIgnoreSource(nameof(Venue.TenantId))]
    [MapperIgnoreSource(nameof(Venue.LastModifiedOn))]
    [MapperIgnoreSource(nameof(Venue.IsDeleted))]
    [MapperIgnoreSource(nameof(Venue.DomainEvents))]
    public static partial VenueDto ToVenueDto(this Venue venue);

    public static partial IQueryable<VenueDto> ToVenueDtoQueryable(this IQueryable<Venue> queryable);

    [MapperIgnoreTarget(nameof(VenueForCreation.Id))]
    public static partial VenueForCreation ToVenueForCreation(this VenueForCreationDto dto, string tenantId);

    private static string MapTimeZone(TimeZoneId timeZone) => timeZone.Value;
}
