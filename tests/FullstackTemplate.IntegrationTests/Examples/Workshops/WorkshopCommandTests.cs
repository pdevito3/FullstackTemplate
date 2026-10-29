namespace FullstackTemplate.IntegrationTests.Examples.Workshops;

using FullstackTemplate.IntegrationTests.Databases;
using FullstackTemplate.Server.Databases;
using FullstackTemplate.Server.Domain.AuditEntries.Dtos;
using FullstackTemplate.Server.Domain.AuditEntries.Features;
using FullstackTemplate.Server.Domain.TimeZones;
using FullstackTemplate.Server.Examples.Venues.Features;
using FullstackTemplate.Server.Examples.Workshops;
using FullstackTemplate.Server.Examples.Workshops.Dtos;
using FullstackTemplate.Server.Examples.Workshops.Features;
using FullstackTemplate.Server.Exceptions;
using FullstackTemplate.SharedTestHelpers.EntityGraphs;
using FullstackTemplate.SharedTestHelpers.Examples.EntityGraphs;
using FullstackTemplate.SharedTestHelpers.Examples.Fakes;
using FullstackTemplate.SharedTestHelpers.Utilities;
using Microsoft.EntityFrameworkCore;
using Shouldly;

public class WorkshopCommandTests : TestBase
{
    [Test]
    public async Task a_venue_loads_with_its_time_zone()
    {
        // Arrange
        var testingServiceScope = new TestingServiceScope();
        var added = await testingServiceScope.SendAsync(
            new AddVenue.Command(new FakeVenueForCreationDto().Generate() with { TimeZone = "Pacific/Tongatapu" }));

        // Act
        var venue = await testingServiceScope.ExecuteDbContextAsync(db => db.Venues.AsNoTracking().GetById(added.Id));

        // Assert
        venue.TimeZone.ShouldBe(TimeZoneId.Of("Pacific/Tongatapu"));
        venue.TimeZone.ToLocal(new DateTimeOffset(2026, 3, 2, 12, 0, 0, TimeSpan.Zero))
            .ShouldBe((new DateOnly(2026, 3, 3), new TimeOnly(1, 0)));
    }

    [Test]
    public async Task an_added_workshop_is_a_draft_with_its_actions()
    {
        // Arrange
        var testingServiceScope = new TestingServiceScope();
        await testingServiceScope.StartGraph(out _).AddVenue(out var venue).BuildAsync();

        // Act
        var added = await testingServiceScope.SendAsync(
            new AddWorkshop.Command(new FakeWorkshopForCreationDto(venue.Id).Generate()));

        // Assert
        added.Status.ShouldBe("Draft");
        added.VenueId.ShouldBe(venue.Id);
        added.AllowedActions.ShouldBe(["Update", "Schedule", "Cancel"]);
        var saved = await testingServiceScope.ExecuteDbContextAsync(db => db.Workshops.GetById(added.Id));
        saved.TenantId.ShouldBe(venue.TenantId);
    }

    [Test]
    public async Task a_workshop_at_a_venue_of_another_tenant_is_refused()
    {
        // Arrange
        var testingServiceScope = new TestingServiceScope();
        await testingServiceScope.StartGraph(out _).AddTenant(out _).AddVenue(out var otherVenue).BuildAsync();

        // Act
        var act = () => testingServiceScope.SendAsync(
            new AddWorkshop.Command(new FakeWorkshopForCreationDto(otherVenue.Id).Generate()));

        // Assert
        var refusal = await act.ShouldThrowAsync<ValidationException>();
        refusal.Property.ShouldBe("venueId");
    }

    [Test]
    public async Task a_member_without_manage_workshops_cannot_cancel()
    {
        // Arrange
        var testingServiceScope = new TestingServiceScope();
        await testingServiceScope.StartGraph(out var seed)
            .AddUser(out var member, u => u.Role = "User")
            .Under(seed.Tenant).AddVenue(out _).AddWorkshop(out var workshop)
            .BuildAsync();
        testingServiceScope.SetUserWithDefaults(identifier: member.Identifier);

        // Act
        var act = () => testingServiceScope.SendAsync(new CancelWorkshop.Command(workshop.Id));

        // Assert
        (await act.ShouldThrowAsync<ForbiddenAccessException>())
            .Message.ShouldBe("You cannot manage workshops. Ask a tenant admin for access.");
        var read = await testingServiceScope.SendAsync(new GetWorkshop.Query(workshop.Id));
        read.AllowedActions.ShouldBeEmpty();
    }

    [Test]
    public async Task an_update_with_an_old_version_gives_the_stale_version()
    {
        // Arrange
        var testingServiceScope = new TestingServiceScope();
        await testingServiceScope.StartGraph(out _).AddVenue(out _).AddWorkshop(out var workshop).BuildAsync();
        var loaded = await testingServiceScope.SendAsync(new GetWorkshop.Query(workshop.Id));
        await testingServiceScope.SendAsync(new UpdateWorkshop.Command(workshop.Id, ForUpdate(loaded) with { Title = "First" }));

        // Act
        var act = () => testingServiceScope.SendAsync(
            new UpdateWorkshop.Command(workshop.Id, ForUpdate(loaded) with { Title = "Second" }));

        // Assert
        await act.ShouldThrowAsync<StaleVersionException>();
        (await testingServiceScope.SendAsync(new GetWorkshop.Query(workshop.Id))).Title.ShouldBe("First");
    }

    [Test]
    public async Task a_cancel_that_a_rival_update_beat_after_the_load_runs_again_and_both_save()
    {
        // Arrange
        var pause = PauseAtQuery.After("workshops.cancel.load");
        await using var services = pause.BuildServices();
        var testingServiceScope = new TestingServiceScope();
        await testingServiceScope.StartGraph(out _).AddVenue(out _).AddWorkshop(out var workshop).BuildAsync();
        var loaded = await testingServiceScope.SendAsync(new GetWorkshop.Query(workshop.Id));

        // Act
        var cancel = new TestingServiceScope(services).SendAsync(new CancelWorkshop.Command(workshop.Id));
        await pause.ReachedAsync();
        await new TestingServiceScope().SendAsync(new UpdateWorkshop.Command(workshop.Id, ForUpdate(loaded) with { Title = "Rival" }));
        pause.Release();

        // Assert
        (await cancel).Status.ShouldBe("Cancelled");
        var saved = await new TestingServiceScope().SendAsync(new GetWorkshop.Query(workshop.Id));
        saved.Status.ShouldBe("Cancelled");
        saved.Title.ShouldBe("Rival");
    }

    [Test]
    public async Task a_schedule_that_a_rival_update_beat_after_the_load_is_a_stale_version_and_keeps_the_update()
    {
        // Arrange
        var pause = PauseAtQuery.After("workshops.schedule.load_venue");
        await using var services = pause.BuildServices();
        var testingServiceScope = new TestingServiceScope();
        await testingServiceScope.StartGraph(out _).AddVenue(out _).AddWorkshop(out var workshop).BuildAsync();
        var loaded = await testingServiceScope.SendAsync(new GetWorkshop.Query(workshop.Id));

        // Act
        var schedule = new TestingServiceScope(services).SendAsync(new ScheduleWorkshop.Command(workshop.Id, ForScheduling(loaded.Version)));
        await pause.ReachedAsync();
        await new TestingServiceScope().SendAsync(new UpdateWorkshop.Command(workshop.Id, ForUpdate(loaded) with { Capacity = 7 }));
        pause.Release();

        // Assert
        await Should.ThrowAsync<StaleVersionException>(schedule);
        var saved = await new TestingServiceScope().SendAsync(new GetWorkshop.Query(workshop.Id));
        saved.Status.ShouldBe("Draft");
        saved.Capacity.ShouldBe(7);
    }

    [Test]
    public async Task a_complete_that_a_rival_cancel_beat_after_the_load_runs_again_and_keeps_the_cancel()
    {
        // Arrange
        var pause = PauseAtQuery.After("workshops.complete.load");
        await using var services = pause.BuildServices();
        var testingServiceScope = new TestingServiceScope();
        await testingServiceScope.StartGraph(out _).AddVenue(out _).AddWorkshop(out var workshop).BuildAsync();
        await testingServiceScope.SendAsync(new ScheduleWorkshop.Command(workshop.Id, ForScheduling(workshop.Version)));

        // Act
        var complete = new TestingServiceScope(services).SendAsync(new CompleteWorkshop.Command(workshop.Id));
        await pause.ReachedAsync();
        await new TestingServiceScope().SendAsync(new CancelWorkshop.Command(workshop.Id));
        pause.Release();

        // Assert
        (await Should.ThrowAsync<ValidationException>(complete))
            .Message.ShouldBe("A cancelled workshop cannot be completed.");
        (await new TestingServiceScope().SendAsync(new GetWorkshop.Query(workshop.Id))).Status.ShouldBe("Cancelled");
    }

    [Test]
    public async Task a_schedule_of_a_start_today_at_the_venue_is_refused()
    {
        // Arrange
        var testingServiceScope = new TestingServiceScope();
        var zone = TimeZoneId.Of("America/Chicago");
        var today = ZonedDate.TodayAt(zone, DateTimeOffset.UtcNow);
        await testingServiceScope.StartGraph(out _)
            .AddVenue(out _, v => v.TimeZone = zone.Value)
            .AddWorkshop(out var workshop, w => w.StartsAt = zone.ToInstant(today.Date, new TimeOnly(10, 0))!.Value)
            .BuildAsync();

        // Act
        var act = () => testingServiceScope.SendAsync(new ScheduleWorkshop.Command(workshop.Id, ForScheduling(workshop.Version)));

        // Assert
        var refusal = await act.ShouldThrowAsync<ValidationException>();
        refusal.Property.ShouldBe("startsAt");
        refusal.Message.ShouldBe("A workshop must start on a day after today at its venue.");
        (await testingServiceScope.SendAsync(new GetWorkshop.Query(workshop.Id))).Status.ShouldBe("Draft");
    }

    [Test]
    public async Task each_change_of_a_workshop_is_in_the_audit_log()
    {
        // Arrange
        var testingServiceScope = new TestingServiceScope();
        await testingServiceScope.StartGraph(out _).AddVenue(out _).AddWorkshop(out var workshop).BuildAsync();

        // Act
        await testingServiceScope.SendAsync(new ScheduleWorkshop.Command(workshop.Id, ForScheduling(workshop.Version)));
        await testingServiceScope.SendAsync(new CancelWorkshop.Command(workshop.Id));

        // Assert
        var page = await testingServiceScope.SendAsync(
            new GetAuditEntryPage.Query(AuditEntryParametersDto.From(workshop.Id, null, 10)));
        page.Items.Select(e => (e.Kind, e.RequestName)).ShouldBe(
        [
            ("Updated", "CancelWorkshop.Command"),
            ("Updated", "ScheduleWorkshop.Command"),
            ("Created", null),
        ]);
        page.Items[0].Values.ShouldContain(v => v.Name.StartsWith(nameof(Workshop.Status)) && v.New == "Cancelled");
    }

    [Test]
    public async Task the_audit_entry_of_an_added_workshop_names_the_member_who_added_it_as_its_organizer()
    {
        // Arrange
        var testingServiceScope = new TestingServiceScope();
        await testingServiceScope.StartGraph(out var seed).AddVenue(out var venue).BuildAsync();

        // Act
        var added = await testingServiceScope.SendAsync(
            new AddWorkshop.Command(new FakeWorkshopForCreationDto(venue.Id).Generate()));

        // Assert
        added.OrganizerId.ShouldBe(seed.User.Id);
        var page = await testingServiceScope.SendAsync(
            new GetAuditEntryPage.Query(AuditEntryParametersDto.From(added.Id, null, 10)));
        var created = page.Items.ShouldHaveSingleItem();
        created.Kind.ShouldBe("Created");
        created.Values.ShouldContain(v => v.Name == nameof(Workshop.OrganizerId) && v.New == seed.User.Id && !v.Hidden);
    }

    private static WorkshopForUpdateDto ForUpdate(WorkshopDto workshop) => new()
    {
        Title = workshop.Title,
        StartsAt = workshop.StartsAt,
        Capacity = workshop.Capacity,
        Version = workshop.Version,
    };

    private static WorkshopForSchedulingDto ForScheduling(uint version) => new() { Version = version };
}
