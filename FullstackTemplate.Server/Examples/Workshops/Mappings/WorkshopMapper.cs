namespace FullstackTemplate.Server.Examples.Workshops.Mappings;

using Domain.Users;
using Dtos;
using Models;
using Riok.Mapperly.Abstractions;
using Venues;

[Mapper]
public static partial class WorkshopMapper
{
    public static WorkshopDto ToWorkshopDto(this Workshop workshop, Venue venue, Caller caller)
    {
        if (workshop.VenueId != venue.Id)
            throw new ArgumentException("Give the venue of the workshop.", nameof(venue));

        return workshop.ToWorkshopDtoWithoutActions() with
        {
            VenueTimeZone = venue.TimeZone.Value,
            AllowedActions = NamesOf(workshop.AllowedActions(caller)),
        };
    }

    /// <summary>
    /// Gives each row of a list the actions that <paramref name="caller"/> can send for it. The list query projects
    /// to the DTO, so the rule reads the status of the DTO.
    /// </summary>
    public static void AddAllowedActions(this List<WorkshopDto> workshops, Caller caller)
    {
        for (var i = 0; i < workshops.Count; i++)
        {
            var status = WorkshopStatus.Of(workshops[i].Status);
            workshops[i] = workshops[i] with { AllowedActions = NamesOf(WorkshopAction.AllowedFor(caller, status)) };
        }
    }

    /// <summary>The DTO of each workshop with the time zone of its venue. The list handler adds the actions after the page loads.</summary>
    public static IQueryable<WorkshopDto> ToWorkshopDtoQueryable(this IQueryable<Workshop> workshops, IQueryable<Venue> venues)
        => workshops
            .Join(venues, w => w.VenueId, v => v.Id,
                (w, v) => new WorkshopAtVenue { Workshop = w, VenueTimeZone = v.TimeZone.Value })
            .ProjectToWorkshopDto();

    [MapperIgnoreSource(nameof(WorkshopForCreationDto.VenueId))]
    [MapperIgnoreTarget(nameof(WorkshopForCreation.Id))]
    public static partial WorkshopForCreation ToWorkshopForCreation(this WorkshopForCreationDto dto);

    public static partial WorkshopForUpdate ToWorkshopForUpdate(this WorkshopForUpdateDto dto);

    private static partial IQueryable<WorkshopDto> ProjectToWorkshopDto(this IQueryable<WorkshopAtVenue> rows);

    [MapNestedProperties(nameof(WorkshopAtVenue.Workshop))]
    [MapperIgnoreTarget(nameof(WorkshopDto.AllowedActions))]
    private static partial WorkshopDto ToWorkshopDto(WorkshopAtVenue row);

    [MapperIgnoreSource(nameof(Workshop.TenantId))]
    [MapperIgnoreSource(nameof(Workshop.LastModifiedOn))]
    [MapperIgnoreSource(nameof(Workshop.IsDeleted))]
    [MapperIgnoreSource(nameof(Workshop.DomainEvents))]
    [MapperIgnoreTarget(nameof(WorkshopDto.VenueTimeZone))]
    [MapperIgnoreTarget(nameof(WorkshopDto.AllowedActions))]
    private static partial WorkshopDto ToWorkshopDtoWithoutActions(this Workshop workshop);

    private static List<string> NamesOf(IEnumerable<WorkshopAction> actions) => actions.Select(a => a.Value).ToList();
    private static string MapStatus(WorkshopStatus status) => status.Value;

    private sealed record WorkshopAtVenue
    {
        public required Workshop Workshop { get; init; }
        public required string VenueTimeZone { get; init; }
    }
}
