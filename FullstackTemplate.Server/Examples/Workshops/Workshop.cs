namespace FullstackTemplate.Server.Examples.Workshops;

using Domain;
using Domain.Users;
using DomainEvents;
using Exceptions;
using Models;
using Venues;

public class Workshop : BaseEntity, ITenantable
{
    public const string IdPrefix = "wks";
    public const int TitleMaxLength = 200;
    public const int CapacityMinimum = 1;
    public const int CapacityMaximum = 500;
    public const string ChangeRefusedMessage = "Only the organizer of this workshop or a tenant admin can change it.";

    [NotSensitive] public string TenantId { get; private set; } = default!;
    [NotSensitive] public string VenueId { get; private set; } = default!;
    [NotSensitive] public string Title { get; private set; } = default!;
    [NotSensitive] public DateTimeOffset StartsAt { get; private set; }
    [NotSensitive] public int Capacity { get; private set; }
    [NotSensitive] public WorkshopStatus Status { get; private set; } = default!;

    /// <summary>The user ID of the member who added the workshop. Null when an agent or system work added it.</summary>
    [NotSensitive] public string? OrganizerId { get; private set; }

    public static Workshop Create(Venue venue, WorkshopForCreation workshopForCreation)
    {
        var workshop = new Workshop
        {
            TenantId = venue.TenantId,
            VenueId = venue.Id,
            Title = workshopForCreation.Title,
            StartsAt = workshopForCreation.StartsAt.ToUniversalTime(),
            Capacity = workshopForCreation.Capacity,
            Status = WorkshopStatus.Draft(),
            OrganizerId = workshopForCreation.OrganizerId,
        };
        if (workshopForCreation.Id is not null)
            workshop.SetId(workshopForCreation.Id);

        ValidateWorkshop(workshop);
        workshop.QueueDomainEvent(new WorkshopCreated(workshop.Id));

        return workshop;
    }

    public Workshop Update(WorkshopForUpdate workshopForUpdate)
    {
        GuardVersion(workshopForUpdate.Version);
        ValidationException.Must(Status.CanBeEdited, "status",
            $"A {Status.Words} workshop cannot be changed.");
        if (workshopForUpdate.Title == Title && workshopForUpdate.StartsAt == StartsAt
            && workshopForUpdate.Capacity == Capacity)
            return this;

        Title = workshopForUpdate.Title;
        StartsAt = workshopForUpdate.StartsAt.ToUniversalTime();
        Capacity = workshopForUpdate.Capacity;

        ValidateWorkshop(this);
        QueueDomainEvent(new WorkshopUpdated(Id));

        return this;
    }

    public Workshop Schedule(SchedulableStart start)
    {
        if (start.WorkshopId != Id || start.StartsAt != StartsAt)
            throw new ArgumentException("Give the proof of the start of this workshop.", nameof(start));

        return MoveTo(WorkshopStatus.Scheduled());
    }

    public Workshop Cancel() => MoveTo(WorkshopStatus.Cancelled());

    public Workshop Complete() => MoveTo(WorkshopStatus.Completed());

    public IReadOnlyList<WorkshopAction> AllowedActions(Caller caller) => WorkshopAction.AllowedFor(caller, OrganizerId, Status);

    /// <summary>The rule on the row that each command of <see cref="WorkshopAction"/> checks after the load.</summary>
    /// <exception cref="ForbiddenAccessException">The caller is not the organizer and cannot manage the tenant.</exception>
    public void GuardChangeBy(Caller caller)
    {
        if (!IsChangeableBy(caller, OrganizerId))
            throw new ForbiddenAccessException(ChangeRefusedMessage);
    }

    internal static bool IsChangeableBy(Caller caller, string? organizerId)
        => (organizerId is not null && caller.IsUser(organizerId)) || caller.Has(Permission.ManageTenant());

    private Workshop MoveTo(WorkshopStatus target)
    {
        if (Status == target)
            return this;

        Status = Status.MoveTo(target);
        QueueDomainEvent(new WorkshopStatusChanged(Id, Status.Value));

        return this;
    }

    private static void ValidateWorkshop(Workshop workshop)
    {
        ValidationException.ThrowWhenNullOrWhitespace(workshop.Title, "title", "Please provide a workshop title.");
        ValidationException.ThrowWhenLongerThan(workshop.Title, TitleMaxLength, "title",
            $"A workshop title can have no more than {TitleMaxLength} characters.");
        ValidationException.ThrowWhenHasCharacterPostgresCannotStore(workshop.Title, "title",
            "A workshop title has a character that is not allowed.");
        ValidationException.MustNot(workshop.StartsAt == default, "startsAt", "Please provide a start.");
        ValidationException.Must(workshop.Capacity is >= CapacityMinimum and <= CapacityMaximum, "capacity",
            $"A workshop has room for {CapacityMinimum} to {CapacityMaximum} people.");
    }

    protected Workshop() : base(IdPrefix) { } // For EF Core
}
