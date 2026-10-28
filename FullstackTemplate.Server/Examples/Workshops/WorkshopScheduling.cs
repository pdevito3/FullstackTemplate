namespace FullstackTemplate.Server.Examples.Workshops;

using Venues;

// The decision shape of docs/backend/features-and-cqrs.md: the rules of a schedule read the workshop, its venue, and
// the time of the request.

/// <summary>The facts that the rules of a schedule read. ScheduleWorkshop loads them.</summary>
public sealed record ScheduleCase
{
    public ScheduleCase(Workshop workshop, Venue venue, DateTimeOffset now)
    {
        if (workshop.VenueId != venue.Id)
            throw new ArgumentException("Give the venue of the workshop.", nameof(venue));
        (Workshop, Venue, Now) = (workshop, venue, now);
    }

    public Workshop Workshop { get; }
    public Venue Venue { get; }
    public DateTimeOffset Now { get; }
}

public abstract record ScheduleRefusal
{
    private ScheduleRefusal(string field, string message) => (Field, Message) = (field, message);

    public string Field { get; }
    public string Message { get; }

    public sealed record WrongStatus(string Field, string Message) : ScheduleRefusal(Field, Message);
    public sealed record NotOffered(string Field, string Message) : ScheduleRefusal(Field, Message);
}

/// <summary>The proof that the start of a workshop passed the rules. Only <see cref="WorkshopScheduling.Decide"/> makes it.</summary>
public sealed class SchedulableStart
{
    internal SchedulableStart(string workshopId, DateTimeOffset startsAt) => (WorkshopId, StartsAt) = (workshopId, startsAt);

    public string WorkshopId { get; }
    public DateTimeOffset StartsAt { get; }
}

public abstract record ScheduleDecision
{
    private ScheduleDecision() { }

    public sealed record Offered(SchedulableStart Start) : ScheduleDecision;
    public sealed record Refused(ScheduleRefusal Refusal) : ScheduleDecision;
}

public static class WorkshopScheduling
{
    public static readonly TimeOnly OpensAt = new(8, 0);
    public static readonly TimeOnly ClosesAt = new(20, 0);

    private static readonly Func<ScheduleCase, ScheduleRefusal?>[] Rules = [Movable, AfterToday, InOpeningHours];

    public static ScheduleDecision Decide(ScheduleCase schedule)
        => Rules.Select(rule => rule(schedule)).FirstOrDefault(r => r is not null) is { } refusal
            ? new ScheduleDecision.Refused(refusal)
            : new ScheduleDecision.Offered(new SchedulableStart(schedule.Workshop.Id, schedule.Workshop.StartsAt));

    // A scheduled workshop passes, so a command that runs again after its own commit changes nothing
    private static ScheduleRefusal.WrongStatus? Movable(ScheduleCase schedule)
    {
        var status = schedule.Workshop.Status;
        var scheduled = WorkshopStatus.Scheduled();
        return status == scheduled || status.CanMoveTo(scheduled)
            ? null
            : new ScheduleRefusal.WrongStatus("status", status.MoveRefusal(scheduled));
    }

    private static ScheduleRefusal.NotOffered? AfterToday(ScheduleCase schedule)
    {
        var zone = schedule.Venue.TimeZone;
        return zone.ToLocal(schedule.Workshop.StartsAt).Date > zone.ToLocal(schedule.Now).Date
            ? null
            : new ScheduleRefusal.NotOffered("startsAt", "A workshop must start on a day after today at its venue.");
    }

    private static ScheduleRefusal.NotOffered? InOpeningHours(ScheduleCase schedule)
    {
        var time = schedule.Venue.TimeZone.ToLocal(schedule.Workshop.StartsAt).Time;
        return time >= OpensAt && time <= ClosesAt
            ? null
            : new ScheduleRefusal.NotOffered("startsAt",
                $"A workshop must start from {OpensAt:HH\\:mm} to {ClosesAt:HH\\:mm} in the time zone of its venue.");
    }
}
