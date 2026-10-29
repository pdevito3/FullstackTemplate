namespace FullstackTemplate.UnitTests.Examples.Workshops;

using FullstackTemplate.Server.Domain.TimeZones;
using FullstackTemplate.Server.Examples.Venues;
using FullstackTemplate.Server.Examples.Workshops;
using FullstackTemplate.SharedTestHelpers.EntityGraphs;
using FullstackTemplate.SharedTestHelpers.Examples.EntityGraphs;
using Shouldly;

public class WorkshopSchedulingTests
{
    private static readonly DateTimeOffset Now = Clock.Now;
    private const string Chicago = "America/Chicago";
    private const string Tongatapu = "Pacific/Tongatapu";

    private const string NotAfterToday = "A workshop must start on a day after today at its venue.";
    private const string NotInOpeningHours = "A workshop must start from 08:00 to 20:00 in the time zone of its venue.";

    public static IEnumerable<Func<(string Case, string Zone, int DaysAhead, int Hour, string Status, Type Kind, string Field, string Message)>> OneBrokenRule()
    {
        yield return () => ("a cancelled workshop", Chicago, 7, 10, "Cancelled",
            typeof(ScheduleRefusal.WrongStatus), "status", "A cancelled workshop cannot be scheduled.");
        yield return () => ("a completed workshop", Chicago, 7, 10, "Completed",
            typeof(ScheduleRefusal.WrongStatus), "status", "A completed workshop cannot be scheduled.");
        yield return () => ("today at the venue", Chicago, 0, 18, "Draft",
            typeof(ScheduleRefusal.NotOffered), "startsAt", NotAfterToday);
        yield return () => ("the UTC tomorrow is today in Tongatapu", Tongatapu, 0, 10, "Draft",
            typeof(ScheduleRefusal.NotOffered), "startsAt", NotAfterToday);
        yield return () => ("before the opening", Chicago, 7, 7, "Draft",
            typeof(ScheduleRefusal.NotOffered), "startsAt", NotInOpeningHours);
        yield return () => ("after the closing", Chicago, 7, 21, "Draft",
            typeof(ScheduleRefusal.NotOffered), "startsAt", NotInOpeningHours);
    }

    [Test]
    [MethodDataSource(nameof(OneBrokenRule))]
    public void a_case_that_breaks_one_rule_gets_the_refusal_of_that_rule(
        string @case, string zone, int daysAhead, int hour, string status, Type kind, string field, string message)
    {
        // Arrange
        var schedule = Case(zone, daysAhead, hour, status);

        // Act
        var decision = WorkshopScheduling.Decide(schedule);

        // Assert
        var refusal = decision.ShouldBeOfType<ScheduleDecision.Refused>(@case).Refusal;
        refusal.ShouldBeOfType(kind);
        refusal.Field.ShouldBe(field);
        refusal.Message.ShouldBe(message);
    }

    [Test]
    [Arguments(Chicago, 1, 8)]
    [Arguments(Chicago, 30, 20)]
    [Arguments(Tongatapu, 1, 12)]
    public void a_draft_that_starts_after_today_in_the_opening_hours_is_offered(string zone, int daysAhead, int hour)
    {
        // Arrange
        var schedule = Case(zone, daysAhead, hour, "Draft");

        // Act
        var decision = WorkshopScheduling.Decide(schedule);

        // Assert
        var start = decision.ShouldBeOfType<ScheduleDecision.Offered>().Start;
        start.WorkshopId.ShouldBe(schedule.Workshop.Id);
        start.StartsAt.ShouldBe(schedule.Workshop.StartsAt);
    }

    [Test]
    public void a_scheduled_workshop_is_offered_again_so_a_second_schedule_changes_nothing()
    {
        // Arrange
        var schedule = Case(Chicago, 7, 10, "Scheduled");

        // Act
        var decision = WorkshopScheduling.Decide(schedule);

        // Assert
        decision.ShouldBeOfType<ScheduleDecision.Offered>();
    }

    [Test]
    public void a_scheduled_workshop_on_its_start_day_is_offered_again_because_the_date_rules_apply_only_to_a_move()
    {
        // Arrange
        var schedule = Case(Chicago, 0, 18, "Scheduled");

        // Act
        var decision = WorkshopScheduling.Decide(schedule);

        // Assert
        decision.ShouldBeOfType<ScheduleDecision.Offered>().Start.StartsAt.ShouldBe(schedule.Workshop.StartsAt);
    }

    [Test]
    [Arguments("Draft")]
    [Arguments("Scheduled")]
    [Arguments("Cancelled")]
    public void a_stale_version_is_refused_before_the_status_and_the_start(string status)
    {
        // Arrange
        var schedule = Case(Chicago, 0, 10, status, stale: true);

        // Act
        var decision = WorkshopScheduling.Decide(schedule);

        // Assert
        var refusal = decision.ShouldBeOfType<ScheduleDecision.Refused>().Refusal;
        refusal.ShouldBeOfType<ScheduleRefusal.StaleVersion>();
        refusal.Field.ShouldBe("version");
    }

    [Test]
    public void a_case_that_breaks_each_rule_gets_the_refusal_of_the_first_rule_that_it_breaks()
    {
        // Act
        var breaksEach = WorkshopScheduling.Decide(Case(Chicago, 0, 21, "Cancelled", stale: true));
        var breaksTheLastThree = WorkshopScheduling.Decide(Case(Chicago, 0, 21, "Cancelled"));
        var breaksTheLastTwo = WorkshopScheduling.Decide(Case(Chicago, 0, 21, "Draft"));
        var breaksTheLast = WorkshopScheduling.Decide(Case(Chicago, 7, 21, "Draft"));
        var breaksNone = WorkshopScheduling.Decide(Case(Chicago, 7, 10, "Draft"));

        // Assert
        breaksEach.ShouldBeOfType<ScheduleDecision.Refused>().Refusal.ShouldBeOfType<ScheduleRefusal.StaleVersion>();
        breaksTheLastThree.ShouldBeOfType<ScheduleDecision.Refused>().Refusal.ShouldBeOfType<ScheduleRefusal.WrongStatus>();
        breaksTheLastTwo.ShouldBeOfType<ScheduleDecision.Refused>().Refusal.Message.ShouldBe(NotAfterToday);
        breaksTheLast.ShouldBeOfType<ScheduleDecision.Refused>().Refusal.Message.ShouldBe(NotInOpeningHours);
        breaksNone.ShouldBeOfType<ScheduleDecision.Offered>();
    }

    [Test]
    public void the_proof_of_an_offered_start_schedules_the_workshop()
    {
        // Arrange
        var schedule = Case(Chicago, 7, 10, "Draft");
        var start = WorkshopScheduling.Decide(schedule).ShouldBeOfType<ScheduleDecision.Offered>().Start;

        // Act
        schedule.Workshop.Schedule(start);

        // Assert
        schedule.Workshop.Status.ShouldBe(WorkshopStatus.Scheduled());
    }

    [Test]
    [Arguments(typeof(ScheduleRefusal))]
    [Arguments(typeof(ScheduleDecision))]
    public void each_kind_of_a_closed_type_is_nested_in_it(Type closed)
    {
        // Act
        var kinds = typeof(WorkshopScheduling).Assembly.GetTypes().Where(t => t.IsSubclassOf(closed));

        // Assert
        kinds.ShouldNotBeEmpty();
        kinds.ShouldAllBe(kind => kind.DeclaringType == closed);
    }

    [Test]
    public void a_case_refuses_a_venue_that_is_not_the_venue_of_the_workshop()
    {
        // Arrange
        TestGraph.Start(out _, Clock.Fixed)
            .AddVenues(out var venues, 2)
            .Under(venues[0]).AddWorkshop(out var workshop);

        // Act
        var act = () => new ScheduleCase(workshop, venues[1], Now, workshop.Version);

        // Assert
        act.ShouldThrow<ArgumentException>().ParamName.ShouldBe("venue");
    }

    private static ScheduleCase Case(string zone, int daysAhead, int hour, string status, bool stale = false)
    {
        var timeZone = TimeZoneId.Of(zone);
        var day = ZonedDate.TodayAt(timeZone, Now).Date.AddDays(daysAhead);
        var startsAt = timeZone.ToInstant(day, new TimeOnly(hour, 0))!.Value;

        TestGraph.Start(out _, Clock.Fixed)
            .AddVenue(out var venue, v => v.TimeZone = zone)
            .AddWorkshop(out var workshop, w => w.StartsAt = startsAt);
        MoveTo(workshop, status, venue);
        return new ScheduleCase(workshop, venue, Now, stale ? workshop.Version + 1 : workshop.Version);
    }

    private static void MoveTo(Workshop workshop, string status, Venue venue)
    {
        switch (status)
        {
            case "Draft":
                return;
            case "Cancelled":
                workshop.Cancel();
                return;
            case "Scheduled" or "Completed":
                Schedule(workshop, venue);
                if (status == "Completed")
                    workshop.Complete();
                return;
            default:
                throw new ArgumentOutOfRangeException(nameof(status), status, null);
        }
    }

    private static void Schedule(Workshop workshop, Venue venue)
    {
        var decision = WorkshopScheduling.Decide(new ScheduleCase(workshop, venue, workshop.StartsAt.AddDays(-30), workshop.Version));
        workshop.Schedule(decision.ShouldBeOfType<ScheduleDecision.Offered>().Start);
    }
}
