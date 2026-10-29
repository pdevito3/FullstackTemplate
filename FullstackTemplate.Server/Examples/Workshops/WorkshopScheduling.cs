namespace FullstackTemplate.Server.Examples.Workshops;

using Exceptions;
using Venues;

public sealed record ScheduleCase
{
    public ScheduleCase(Workshop workshop, Venue venue, DateTimeOffset now, uint loadedVersion)
    {
        if (workshop.VenueId != venue.Id)
            throw new ArgumentException("Give the venue of the workshop.", nameof(venue));
        (Workshop, Venue, Now, LoadedVersion) = (workshop, venue, now, loadedVersion);
    }

    public Workshop Workshop { get; }
    public Venue Venue { get; }
    public DateTimeOffset Now { get; }
    public uint LoadedVersion { get; }
}

public abstract record ScheduleRefusal
{
    private ScheduleRefusal(string field, string message) => (Field, Message) = (field, message);

    public string Field { get; }
    public string Message { get; }

    public sealed record StaleVersion(string Field, string Message) : ScheduleRefusal(Field, Message);
    public sealed record WrongStatus(string Field, string Message) : ScheduleRefusal(Field, Message);
    public sealed record NotOffered(string Field, string Message) : ScheduleRefusal(Field, Message);
}

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

    private static readonly Func<ScheduleCase, ScheduleRefusal?>[] Guards = [CurrentVersion, Movable];
    private static readonly Func<ScheduleCase, ScheduleRefusal?>[] StartRules = [AfterToday, InOpeningHours];

    public static ScheduleDecision Decide(ScheduleCase schedule)
    {
        if (FirstRefusal(Guards, schedule) is { } refusal)
            return new ScheduleDecision.Refused(refusal);

        if (schedule.Workshop.Status == WorkshopStatus.Scheduled())
            return Offer(schedule);

        return FirstRefusal(StartRules, schedule) is { } startRefusal
            ? new ScheduleDecision.Refused(startRefusal)
            : Offer(schedule);
    }

    private static ScheduleRefusal? FirstRefusal(Func<ScheduleCase, ScheduleRefusal?>[] rules, ScheduleCase schedule)
        => rules.Select(rule => rule(schedule)).FirstOrDefault(r => r is not null);

    private static ScheduleDecision.Offered Offer(ScheduleCase schedule)
        => new(new SchedulableStart(schedule.Workshop.Id, schedule.Workshop.StartsAt));

    private static ScheduleRefusal.StaleVersion? CurrentVersion(ScheduleCase schedule)
        => schedule.LoadedVersion == schedule.Workshop.Version
            ? null
            : new ScheduleRefusal.StaleVersion(StaleVersionException.Field, StaleVersionException.StaleMessage);

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
