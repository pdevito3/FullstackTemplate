namespace FullstackTemplate.SharedTestHelpers.Examples.Fakes;

using AutoBogus;
using FullstackTemplate.Server.Examples.Venues.Dtos;

public sealed class FakeVenueForCreationDto : AutoFaker<VenueForCreationDto>
{
    public FakeVenueForCreationDto()
    {
        RuleFor(x => x.Name, VenueRules.Name);
        RuleFor(x => x.TimeZone, VenueRules.TimeZone);
    }
}
