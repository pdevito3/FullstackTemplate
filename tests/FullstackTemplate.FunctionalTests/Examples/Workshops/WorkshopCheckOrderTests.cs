namespace FullstackTemplate.FunctionalTests.Examples.Workshops;

using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using FullstackTemplate.Server.Domain;
using FullstackTemplate.Server.Domain.TimeZones;
using FullstackTemplate.Server.Domain.Users;
using FullstackTemplate.Server.Examples.Venues;
using FullstackTemplate.Server.Examples.Workshops;
using FullstackTemplate.Server.Examples.Workshops.Dtos;
using FullstackTemplate.Server.Examples.Workshops.Models;
using FullstackTemplate.Server.Resources.Entrypoints;
using FullstackTemplate.SharedTestHelpers.EntityGraphs;
using FullstackTemplate.SharedTestHelpers.Examples.EntityGraphs;
using FullstackTemplate.SharedTestHelpers.Examples.Fakes;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Shouldly;
using TestUtilities;

public class WorkshopCheckOrderTests : TestBase
{
    private const string RowRefusal = "Only the organizer of this workshop or a tenant admin can change it.";

    [Test]
    public async Task an_unknown_workshop_is_not_found_for_a_member_who_cannot_change_the_workshops_of_others()
    {
        // Arrange
        var members = await AddMembersAsync();
        FactoryClient.SignInAsAppUser(members.OtherWorkshopManager.Identifier);

        // Act
        var result = await FactoryClient.PutJsonRequestAsync(ApiRoutes.Workshops.Update(EntityId.New(Workshop.IdPrefix)),
            new { title = "Renamed", startsAt = DateTimeOffset.UtcNow.AddDays(7), capacity = 3, version = 1 });

        // Assert
        result.StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }

    [Test]
    [Arguments("Update")]
    [Arguments("Schedule")]
    [Arguments("Cancel")]
    [Arguments("Complete")]
    public async Task a_member_with_manage_workshops_who_is_not_the_organizer_is_forbidden(string action)
    {
        // Arrange
        var members = await AddMembersAsync();
        var workshop = await AddWorkshopAsAsync(members.Organizer, members.Venue);
        FactoryClient.SignInAsAppUser(members.OtherWorkshopManager.Identifier);

        // Act
        var result = await SendAsync(action, workshop);

        // Assert
        result.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
        (await result.Content.ReadFromJsonAsync<ProblemDetails>()).ShouldNotBeNull().Detail.ShouldBe(RowRefusal);
    }

    [Test]
    public async Task a_schedule_by_a_member_who_is_not_the_organizer_is_forbidden_before_the_venue_loads()
    {
        // Arrange
        var members = await AddMembersAsync();
        var workshop = await AddWorkshopAsAsync(members.Organizer, members.Venue);
        await ExecuteDbContextAsync(async db =>
        {
            db.Venues.Remove(await db.Venues.SingleAsync(v => v.Id == members.Venue.Id));
            await db.SaveChangesAsync();
        });
        FactoryClient.SignInAsAppUser(members.OtherWorkshopManager.Identifier);

        // Act
        var result = await SendAsync("Schedule", workshop);

        // Assert
        result.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
    }

    [Test]
    public async Task the_organizer_changes_their_own_workshop()
    {
        // Arrange
        var members = await AddMembersAsync();
        var workshop = await AddWorkshopAsAsync(members.Organizer, members.Venue);

        // Act
        var result = await SendAsync("Update", workshop);

        // Assert
        result.StatusCode.ShouldBe(HttpStatusCode.OK);
        (await result.Content.ReadFromJsonAsync<WorkshopDto>()).ShouldNotBeNull().Title.ShouldBe("Renamed");
    }

    [Test]
    public async Task a_tenant_admin_changes_the_workshop_of_another_organizer()
    {
        // Arrange
        var members = await AddMembersAsync();
        var workshop = await AddWorkshopAsAsync(members.Organizer, members.Venue);
        FactoryClient.SignInAsAppUser(members.TenantAdmin.Identifier);

        // Act
        var result = await SendAsync("Update", workshop);

        // Assert
        result.StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    [Test]
    public async Task the_actions_of_a_workshop_follow_the_organizer_on_the_detail_and_in_the_list()
    {
        // Arrange
        var members = await AddMembersAsync();
        var workshop = await AddWorkshopAsAsync(members.Organizer, members.Venue);

        // Act
        var byOrganizer = await ActionsAsync(members.Organizer, workshop.Id);
        var byOtherWorkshopManager = await ActionsAsync(members.OtherWorkshopManager, workshop.Id);
        var byTenantAdmin = await ActionsAsync(members.TenantAdmin, workshop.Id);

        // Assert
        byOrganizer.ShouldBe(["detail: Update, Schedule, Cancel", "row: Update, Schedule, Cancel"]);
        byOtherWorkshopManager.ShouldBe(["detail: ", "row: "]);
        byTenantAdmin.ShouldBe(["detail: Update, Schedule, Cancel", "row: Update, Schedule, Cancel"]);
    }

    [Test]
    public async Task an_mcp_cancel_by_a_member_who_is_not_the_organizer_is_a_permission_error()
    {
        // Arrange
        var members = await AddMembersAsync();
        var workshop = await AddWorkshopAsAsync(members.Organizer, members.Venue);
        FactoryClient.SignInAsAppUser(members.OtherWorkshopManager.Identifier);
        await using var client = await CreateMcpClientAsync();

        // Act
        var result = await client.CallToolAsync("cancel_workshop",
            new Dictionary<string, object?> { ["id"] = workshop.Id });

        // Assert
        result.ShouldBeToolError("permission_denied");
    }

    [Test]
    [Arguments("Update")]
    [Arguments("Schedule")]
    public async Task a_stale_version_on_a_cancelled_workshop_is_a_conflict(string action)
    {
        // Arrange
        var members = await AddMembersAsync();
        var workshop = await AddWorkshopAsAsync(members.Organizer, members.Venue);
        (await FactoryClient.PostJsonRequestAsync(ApiRoutes.Workshops.Cancel(workshop.Id), new { }))
            .StatusCode.ShouldBe(HttpStatusCode.OK);

        // Act
        var result = await SendAsync(action, workshop);

        // Assert
        result.StatusCode.ShouldBe(HttpStatusCode.Conflict);
        var problem = (await result.Content.ReadFromJsonAsync<ValidationProblemDetails>()).ShouldNotBeNull();
        problem.Type.ShouldBe(DomainProblemDetails.StaleVersionType);
        problem.Errors.ShouldHaveSingleItem().Key.ShouldBe("version");
    }

    [Test]
    public async Task an_update_with_the_current_values_of_a_cancelled_workshop_is_refused_on_the_status()
    {
        // Arrange
        var members = await AddMembersAsync();
        var workshop = await AddWorkshopAsAsync(members.Organizer, members.Venue);
        var cancel = await FactoryClient.PostJsonRequestAsync(ApiRoutes.Workshops.Cancel(workshop.Id), new { });
        var cancelled = (await cancel.Content.ReadFromJsonAsync<WorkshopDto>()).ShouldNotBeNull();

        // Act
        var result = await FactoryClient.PutJsonRequestAsync(ApiRoutes.Workshops.Update(workshop.Id), new
        {
            title = cancelled.Title, startsAt = cancelled.StartsAt, capacity = cancelled.Capacity, version = cancelled.Version,
        });

        // Assert
        result.StatusCode.ShouldBe(HttpStatusCode.UnprocessableEntity);
        var problem = (await result.Content.ReadFromJsonAsync<ValidationProblemDetails>()).ShouldNotBeNull();
        problem.Errors.ShouldHaveSingleItem().Key.ShouldBe("status");
    }

    [Test]
    public async Task a_schedule_with_no_version_is_a_bad_request()
    {
        // Arrange
        var members = await AddMembersAsync();
        var workshop = await AddWorkshopAsAsync(members.Organizer, members.Venue);

        // Act
        var result = await FactoryClient.PostJsonRequestAsync(ApiRoutes.Workshops.Schedule(workshop.Id), new { });

        // Assert
        result.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
    }

    [Test]
    public async Task a_schedule_of_a_scheduled_workshop_that_starts_today_gives_it_with_no_change()
    {
        // Arrange
        var members = await AddMembersAsync();
        var workshop = await AddScheduledWorkshopThatStartsTodayAsync(members);
        FactoryClient.SignInAsAppUser(members.Organizer.Identifier);

        // Act
        var result = await FactoryClient.PostJsonRequestAsync(ApiRoutes.Workshops.Schedule(workshop.Id),
            new { version = workshop.Version });

        // Assert
        result.StatusCode.ShouldBe(HttpStatusCode.OK);
        var dto = (await result.Content.ReadFromJsonAsync<WorkshopDto>()).ShouldNotBeNull();
        dto.Status.ShouldBe("Scheduled");
        dto.Version.ShouldBe(workshop.Version);
    }

    private sealed record Members(User Organizer, User OtherWorkshopManager, User TenantAdmin, Venue Venue);

    private static async Task<Members> AddMembersAsync()
    {
        TestGraph.Start(out _)
            .AddTenant(out var tenant)
            .AddUsers(out var users, u => u.Role = "User", u => u.Role = "User", u => u.Role = "Admin")
            .Under(tenant).AddVenue(out var venue);
        users[0].AddPermission(Permission.ManageWorkshops());
        users[1].AddPermission(Permission.ManageWorkshops());
        await InsertAsync(tenant);
        await InsertAsync(users);
        await InsertAsync(venue);
        return new Members(users[0], users[1], users[2], venue);
    }

    private static async Task<Workshop> AddScheduledWorkshopThatStartsTodayAsync(Members members)
    {
        var zone = members.Venue.TimeZone;
        var today = ZonedDate.TodayAt(zone, DateTimeOffset.UtcNow);
        var workshop = Workshop.Create(members.Venue, new WorkshopForCreation
        {
            Title = "Glazing basics",
            StartsAt = zone.ToInstant(today.Date, new TimeOnly(10, 0))!.Value,
            Capacity = 12,
            OrganizerId = members.Organizer.Id,
        });
        var plan = new ScheduleCase(workshop, members.Venue, workshop.StartsAt.AddDays(-30), workshop.Version);
        workshop.Schedule(((ScheduleDecision.Offered)WorkshopScheduling.Decide(plan)).Start);
        await InsertAsync(workshop);
        return workshop;
    }

    private async Task<WorkshopDto> AddWorkshopAsAsync(User organizer, Venue venue)
    {
        FactoryClient.SignInAsAppUser(organizer.Identifier);
        var result = await FactoryClient.PostJsonRequestAsync(ApiRoutes.Workshops.Create,
            new FakeWorkshopForCreationDto(venue.Id).Generate());
        result.StatusCode.ShouldBe(HttpStatusCode.Created);
        return (await result.Content.ReadFromJsonAsync<WorkshopDto>()).ShouldNotBeNull();
    }

    private Task<HttpResponseMessage> SendAsync(string action, WorkshopDto workshop) => action switch
    {
        "Update" => FactoryClient.PutJsonRequestAsync(ApiRoutes.Workshops.Update(workshop.Id),
            new { title = "Renamed", startsAt = workshop.StartsAt, capacity = workshop.Capacity, version = workshop.Version }),
        "Schedule" => FactoryClient.PostJsonRequestAsync(ApiRoutes.Workshops.Schedule(workshop.Id),
            new { version = workshop.Version }),
        "Cancel" => FactoryClient.PostJsonRequestAsync(ApiRoutes.Workshops.Cancel(workshop.Id), new { }),
        "Complete" => FactoryClient.PostJsonRequestAsync(ApiRoutes.Workshops.Complete(workshop.Id), new { }),
        _ => throw new ArgumentOutOfRangeException(nameof(action), action, null),
    };

    private async Task<List<string>> ActionsAsync(User reader, string workshopId)
    {
        FactoryClient.SignInAsAppUser(reader.Identifier);
        var detail = await FactoryClient.GetRequestAsync(ApiRoutes.Workshops.Get(workshopId));
        var list = await FactoryClient.GetRequestAsync(ApiRoutes.Workshops.GetList);
        var row = (await list.Content.ReadFromJsonAsync<JsonElement>()).EnumerateArray()
            .Single(r => r.GetProperty("id").GetString() == workshopId);
        var onDetail = (await detail.Content.ReadFromJsonAsync<WorkshopDto>()).ShouldNotBeNull().AllowedActions;
        var onRow = row.GetProperty("allowedActions").EnumerateArray().Select(a => a.GetString());
        return [$"detail: {string.Join(", ", onDetail)}", $"row: {string.Join(", ", onRow)}"];
    }
}
