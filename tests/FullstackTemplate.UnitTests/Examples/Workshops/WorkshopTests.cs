namespace FullstackTemplate.UnitTests.Examples.Workshops;

using FullstackTemplate.Server.Examples.Workshops;
using FullstackTemplate.Server.Examples.Workshops.DomainEvents;
using FullstackTemplate.Server.Examples.Workshops.Models;
using FullstackTemplate.Server.Exceptions;
using FullstackTemplate.SharedTestHelpers.EntityGraphs;
using FullstackTemplate.SharedTestHelpers.Examples.EntityGraphs;
using Shouldly;

public class WorkshopTests
{
    [Test]
    public void a_new_workshop_is_a_draft_in_the_tenant_of_its_venue()
    {
        // Arrange
        var startsAt = new DateTimeOffset(2026, 11, 3, 10, 0, 0, TimeSpan.FromHours(-6));

        // Act
        TestGraph.Start(out var seed)
            .AddVenue(out var venue)
            .AddWorkshop(out var workshop, w => w.StartsAt = startsAt);

        // Assert
        workshop.Status.ShouldBe(WorkshopStatus.Draft());
        workshop.TenantId.ShouldBe(seed.Tenant.Id);
        workshop.VenueId.ShouldBe(venue.Id);
        workshop.StartsAt.ShouldBe(startsAt);
        workshop.StartsAt.Offset.ShouldBe(TimeSpan.Zero);
        workshop.DomainEvents.ShouldBe([new WorkshopCreated(workshop.Id)]);
    }

    public static IEnumerable<Func<(Action<WorkshopInput> Customize, string Field, string Message)>> InvalidInputs()
    {
        yield return () => (w => w.Title = " ", "title", "Please provide a workshop title.");
        yield return () => (w => w.Title = new string('a', Workshop.TitleMaxLength + 1), "title",
            $"A workshop title can have no more than {Workshop.TitleMaxLength} characters.");
        yield return () => (w => w.Title = "a\0b", "title", "A workshop title has a character that is not allowed.");
        yield return () => (w => w.StartsAt = default(DateTimeOffset), "startsAt", "Please provide a start.");
        yield return () => (w => w.Capacity = Workshop.CapacityMinimum - 1, "capacity", "A workshop has room for 1 to 500 people.");
        yield return () => (w => w.Capacity = Workshop.CapacityMaximum + 1, "capacity",
            "A workshop has room for 1 to 500 people.");
    }

    [Test]
    [MethodDataSource(nameof(InvalidInputs))]
    public void a_workshop_refuses_an_invalid_value(Action<WorkshopInput> customize, string field, string message)
    {
        // Arrange
        var graph = TestGraph.Start(out _).AddVenue(out _);

        // Act
        var act = () => graph.AddWorkshop(out _, customize);

        // Assert
        var refusal = act.ShouldThrow<Exception>().GetBaseException().ShouldBeOfType<ValidationException>();
        refusal.Property.ShouldBe(field);
        refusal.Message.ShouldBe(message);
    }

    [Test]
    public void an_update_with_an_old_version_gives_the_stale_version()
    {
        // Arrange
        TestGraph.Start(out _).AddVenue(out _).AddWorkshop(out var workshop);

        // Act
        var act = () => workshop.Update(ForUpdate(workshop) with { Version = workshop.Version + 1 });

        // Assert
        act.ShouldThrow<StaleVersionException>();
    }

    [Test]
    public void an_update_with_the_current_values_of_a_cancelled_workshop_is_refused_on_the_status()
    {
        // Arrange
        TestGraph.Start(out _).AddVenue(out _).AddWorkshop(out var workshop);
        workshop.Cancel();

        // Act
        var act = () => workshop.Update(ForUpdate(workshop));

        // Assert
        var refusal = act.ShouldThrow<ValidationException>();
        refusal.Property.ShouldBe("status");
        refusal.Message.ShouldBe("A cancelled workshop cannot be changed.");
    }

    [Test]
    public void an_update_with_the_current_values_changes_nothing_and_queues_no_event()
    {
        // Arrange
        TestGraph.Start(out _).AddVenue(out _).AddWorkshop(out var workshop);
        var current = ForUpdate(workshop) with { StartsAt = workshop.StartsAt.ToOffset(TimeSpan.FromHours(-6)) };

        // Act
        workshop.Update(current);

        // Assert
        workshop.StartsAt.Offset.ShouldBe(TimeSpan.Zero);
        workshop.DomainEvents.OfType<WorkshopUpdated>().ShouldBeEmpty();
    }

    [Test]
    public void an_update_that_changes_one_value_queues_the_update()
    {
        // Arrange
        TestGraph.Start(out _).AddVenue(out _).AddWorkshop(out var workshop);

        // Act
        workshop.Update(ForUpdate(workshop) with { Capacity = workshop.Capacity == 1 ? 2 : 1 });

        // Assert
        workshop.DomainEvents.OfType<WorkshopUpdated>().ShouldBe([new WorkshopUpdated(workshop.Id)]);
    }

    [Test]
    public void a_second_cancel_changes_nothing()
    {
        // Arrange
        TestGraph.Start(out _).AddVenue(out _).AddWorkshop(out var workshop);
        workshop.Cancel();

        // Act
        workshop.Cancel();

        // Assert
        workshop.Status.ShouldBe(WorkshopStatus.Cancelled());
        workshop.DomainEvents.OfType<WorkshopStatusChanged>().ShouldBe([new WorkshopStatusChanged(workshop.Id, "Cancelled")]);
    }

    [Test]
    public void a_draft_cannot_be_completed()
    {
        // Arrange
        TestGraph.Start(out _).AddVenue(out _).AddWorkshop(out var workshop);

        // Act
        var act = () => workshop.Complete();

        // Assert
        act.ShouldThrow<ValidationException>().Message.ShouldBe("A draft workshop cannot be completed.");
        workshop.Status.ShouldBe(WorkshopStatus.Draft());
    }

    [Test]
    public void a_schedule_refuses_the_proof_of_another_workshop()
    {
        // Arrange
        TestGraph.Start(out _, Clock.Fixed).AddVenue(out var venue).AddWorkshops(out var workshops, 2);
        var proof = WorkshopScheduling.Decide(new ScheduleCase(workshops[0], venue, Clock.Now, workshops[0].Version))
            .ShouldBeOfType<ScheduleDecision.Offered>().Start;

        // Act
        var act = () => workshops[1].Schedule(proof);

        // Assert
        act.ShouldThrow<ArgumentException>().ParamName.ShouldBe("start");
    }

    private static WorkshopForUpdate ForUpdate(Workshop workshop) => new()
    {
        Title = workshop.Title,
        StartsAt = workshop.StartsAt,
        Capacity = workshop.Capacity,
        Version = workshop.Version,
    };
}
