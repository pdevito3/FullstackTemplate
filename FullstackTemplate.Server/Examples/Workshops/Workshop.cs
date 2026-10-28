namespace FullstackTemplate.Server.Examples.Workshops;

using Domain;
using Domain.Users;
using DomainEvents;
using Exceptions;
using Models;
using Venues;

/// <summary>A workshop at a venue. It moves from draft to scheduled, and then to cancelled or completed.</summary>
public class Workshop : BaseEntity, ITenantable
{
    public const string IdPrefix = "wks";
    public const int TitleMaxLength = 200;
    public const int CapacityMaximum = 500;

    [NotSensitive] public string TenantId { get; private set; } = default!;
    [NotSensitive] public string VenueId { get; private set; } = default!;
    [NotSensitive] public string Title { get; private set; } = default!;
    [NotSensitive] public DateTimeOffset StartsAt { get; private set; }
    [NotSensitive] public int Capacity { get; private set; }
    [NotSensitive] public WorkshopStatus Status { get; private set; } = default!;

    /// <summary>A new workshop is a draft in the tenant of its venue.</summary>
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
            $"A {Status.Value.ToLowerInvariant()} workshop cannot be changed.");

        Title = workshopForUpdate.Title;
        StartsAt = workshopForUpdate.StartsAt.ToUniversalTime();
        Capacity = workshopForUpdate.Capacity;

        ValidateWorkshop(this);
        QueueDomainEvent(new WorkshopUpdated(Id));

        return this;
    }

    /// <summary>Takes the proof of <see cref="WorkshopScheduling.Decide"/>, so a workshop cannot skip the rules of its venue.</summary>
    public Workshop Schedule(SchedulableStart start)
    {
        if (start.WorkshopId != Id || start.StartsAt != StartsAt)
            throw new ArgumentException("Give the proof of the start of this workshop.", nameof(start));

        return MoveTo(WorkshopStatus.Scheduled());
    }

    public Workshop Cancel() => MoveTo(WorkshopStatus.Cancelled());

    public Workshop Complete() => MoveTo(WorkshopStatus.Completed());

    /// <summary>The actions that the caller can send for this workshop now. The read DTO carries them.</summary>
    public IReadOnlyList<WorkshopAction> AllowedActions(Caller caller) => WorkshopAction.AllowedFor(caller, Status);

    private Workshop MoveTo(WorkshopStatus target)
    {
        // A command that runs again after its own commit finds the target status, so it changes nothing
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
        ValidationException.Must(workshop.Capacity is >= 1 and <= CapacityMaximum, "capacity",
            $"A workshop has room for 1 to {CapacityMaximum} people.");
    }

    protected Workshop() : base(IdPrefix) { } // For EF Core
}
