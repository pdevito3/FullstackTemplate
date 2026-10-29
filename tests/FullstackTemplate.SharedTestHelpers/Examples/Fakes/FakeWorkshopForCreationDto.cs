namespace FullstackTemplate.SharedTestHelpers.Examples.Fakes;

using AutoBogus;
using FullstackTemplate.Server.Examples.Workshops.Dtos;

public sealed class FakeWorkshopForCreationDto : AutoFaker<WorkshopForCreationDto>
{
    public FakeWorkshopForCreationDto(string venueId)
    {
        RuleFor(x => x.VenueId, _ => venueId);
        RuleFor(x => x.Title, WorkshopRules.Title);
        RuleFor(x => x.StartsAt, f => f.Date.SoonOffset(30).ToUniversalTime());
        RuleFor(x => x.Capacity, WorkshopRules.Capacity);
    }
}
