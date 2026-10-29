namespace FullstackTemplate.Server.Examples.Workshops.Mappings;

using Domain.Users;
using Dtos;
using Models;
using Riok.Mapperly.Abstractions;

[Mapper]
public static partial class WorkshopMapper
{
    public static WorkshopDto ToWorkshopDto(this Workshop workshop, Caller caller)
        => workshop.ToWorkshopDtoWithoutActions() with { AllowedActions = NamesOf(workshop.AllowedActions(caller)) };

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

    // It takes the configuration of ToWorkshopDtoWithoutActions. The list handler adds the actions after the page loads.
    public static partial IQueryable<WorkshopDto> ToWorkshopDtoQueryable(this IQueryable<Workshop> queryable);

    [MapperIgnoreSource(nameof(WorkshopForCreationDto.VenueId))]
    [MapperIgnoreTarget(nameof(WorkshopForCreation.Id))]
    public static partial WorkshopForCreation ToWorkshopForCreation(this WorkshopForCreationDto dto);

    public static partial WorkshopForUpdate ToWorkshopForUpdate(this WorkshopForUpdateDto dto);

    [MapperIgnoreSource(nameof(Workshop.TenantId))]
    [MapperIgnoreSource(nameof(Workshop.LastModifiedOn))]
    [MapperIgnoreSource(nameof(Workshop.IsDeleted))]
    [MapperIgnoreSource(nameof(Workshop.DomainEvents))]
    [MapperIgnoreTarget(nameof(WorkshopDto.AllowedActions))]
    private static partial WorkshopDto ToWorkshopDtoWithoutActions(this Workshop workshop);

    private static List<string> NamesOf(IEnumerable<WorkshopAction> actions) => actions.Select(a => a.Value).ToList();
    private static string MapStatus(WorkshopStatus status) => status.Value;
}
