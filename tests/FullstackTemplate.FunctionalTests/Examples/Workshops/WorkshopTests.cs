namespace FullstackTemplate.FunctionalTests.Examples.Workshops;

using System.Net;
using System.Net.Http.Json;
using FullstackTemplate.Server.Domain.Tenants;
using FullstackTemplate.Server.Domain.TimeZones;
using FullstackTemplate.Server.Examples.Workshops;
using FullstackTemplate.Server.Examples.Workshops.Dtos;
using FullstackTemplate.Server.Resources.Entrypoints;
using FullstackTemplate.SharedTestHelpers.EntityGraphs;
using FullstackTemplate.SharedTestHelpers.Examples.EntityGraphs;
using FullstackTemplate.SharedTestHelpers.Examples.Fakes;
using Microsoft.AspNetCore.Mvc;
using Shouldly;
using TestUtilities;

public class WorkshopTests : TestBase
{
    [Test]
    public async Task an_admin_adds_a_venue_and_a_workshop_at_it()
    {
        // Arrange
        await AddTenantAndSignInAsync("Admin");
        var venueResult = await FactoryClient.PostJsonRequestAsync(ApiRoutes.Venues.Create,
            new FakeVenueForCreationDto().Generate());
        venueResult.StatusCode.ShouldBe(HttpStatusCode.Created);
        var venueId = (await venueResult.Content.ReadFromJsonAsync<VenueIdOnly>()).ShouldNotBeNull().Id;

        // Act
        var result = await FactoryClient.PostJsonRequestAsync(ApiRoutes.Workshops.Create,
            new FakeWorkshopForCreationDto(venueId).Generate());

        // Assert
        result.StatusCode.ShouldBe(HttpStatusCode.Created);
        var workshop = (await result.Content.ReadFromJsonAsync<WorkshopDto>()).ShouldNotBeNull();
        result.Headers.Location.ShouldNotBeNull().ToString().ShouldEndWith($"/workshops/{workshop.Id}");
        workshop.Status.ShouldBe("Draft");
        workshop.AllowedActions.ShouldBe(["Update", "Schedule", "Cancel"]);
    }

    [Test]
    public async Task a_member_reads_a_workshop_with_no_actions_and_cannot_cancel_it()
    {
        // Arrange
        var (_, workshop) = await AddWorkshopAndSignInAsync("User");

        // Act
        var cancel = await FactoryClient.PostJsonRequestAsync(ApiRoutes.Workshops.Cancel(workshop.Id), new { });

        // Assert
        cancel.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
        var read = await FactoryClient.GetRequestAsync(ApiRoutes.Workshops.Get(workshop.Id));
        read.StatusCode.ShouldBe(HttpStatusCode.OK);
        var dto = (await read.Content.ReadFromJsonAsync<WorkshopDto>()).ShouldNotBeNull();
        dto.Status.ShouldBe("Draft");
        dto.AllowedActions.ShouldBeEmpty();
    }

    [Test]
    public async Task put_with_a_stale_version_is_a_stale_version_problem_on_the_version()
    {
        // Arrange
        var (_, workshop) = await AddWorkshopAndSignInAsync("Admin");
        var update = new
        {
            title = "Glazing basics",
            startsAt = workshop.StartsAt,
            capacity = 12,
            version = workshop.Version,
        };
        (await FactoryClient.PutJsonRequestAsync(ApiRoutes.Workshops.Update(workshop.Id), update))
            .StatusCode.ShouldBe(HttpStatusCode.OK);

        // Act
        var result = await FactoryClient.PutJsonRequestAsync(ApiRoutes.Workshops.Update(workshop.Id),
            update with { title = "Wheel throwing" });

        // Assert
        result.StatusCode.ShouldBe(HttpStatusCode.Conflict);
        var problem = (await result.Content.ReadFromJsonAsync<ValidationProblemDetails>()).ShouldNotBeNull();
        problem.Type.ShouldBe(DomainProblemDetails.StaleVersionType);
        problem.Errors.ShouldHaveSingleItem().Key.ShouldBe("version");
    }

    [Test]
    public async Task a_schedule_of_a_start_today_is_a_validation_problem_on_the_start()
    {
        // Arrange
        var zone = TimeZoneId.Of("America/Chicago");
        var (_, workshop) = await AddWorkshopAndSignInAsync("Admin",
            w => w.StartsAt = ZonedDate.TodayAt(zone, DateTimeOffset.UtcNow).Start, zone);

        // Act
        var result = await FactoryClient.PostJsonRequestAsync(ApiRoutes.Workshops.Schedule(workshop.Id), new { });

        // Assert
        result.StatusCode.ShouldBe(HttpStatusCode.UnprocessableEntity);
        var problem = (await result.Content.ReadFromJsonAsync<ValidationProblemDetails>()).ShouldNotBeNull();
        problem.Errors.ShouldHaveSingleItem().Key.ShouldBe("startsAt");
    }

    [Test]
    public async Task a_schedule_that_runs_again_gives_the_scheduled_workshop()
    {
        // Arrange
        var (_, workshop) = await AddWorkshopAndSignInAsync("Admin");
        (await FactoryClient.PostJsonRequestAsync(ApiRoutes.Workshops.Schedule(workshop.Id), new { }))
            .StatusCode.ShouldBe(HttpStatusCode.OK);

        // Act
        var result = await FactoryClient.PostJsonRequestAsync(ApiRoutes.Workshops.Schedule(workshop.Id), new { });

        // Assert
        result.StatusCode.ShouldBe(HttpStatusCode.OK);
        var dto = (await result.Content.ReadFromJsonAsync<WorkshopDto>()).ShouldNotBeNull();
        dto.Status.ShouldBe("Scheduled");
        dto.AllowedActions.ShouldBe(["Cancel", "Complete"]);
    }

    [Test]
    public async Task an_mcp_client_lists_and_cancels_a_workshop()
    {
        // Arrange
        var (_, workshop) = await AddWorkshopAndSignInAsync("Admin");
        await using var client = await CreateMcpClientAsync();

        // Act
        var list = await client.CallToolAsync("list_workshops", new Dictionary<string, object?>());
        var cancel = await client.CallToolAsync("cancel_workshop",
            new Dictionary<string, object?> { ["id"] = workshop.Id });

        // Assert
        list.IsError.ShouldNotBe(true);
        list.StructuredContent.ShouldNotBeNull().GetProperty("items").EnumerateArray().ShouldHaveSingleItem()
            .GetProperty("id").GetString().ShouldBe(workshop.Id);
        cancel.IsError.ShouldNotBe(true);
        cancel.StructuredContent.ShouldNotBeNull().GetProperty("status").GetString().ShouldBe("Cancelled");
    }

    [Test]
    public async Task an_mcp_cancel_by_a_member_is_a_permission_error()
    {
        // Arrange
        var (_, workshop) = await AddWorkshopAndSignInAsync("User");
        await using var client = await CreateMcpClientAsync();

        // Act
        var result = await client.CallToolAsync("cancel_workshop",
            new Dictionary<string, object?> { ["id"] = workshop.Id });

        // Assert
        result.ShouldBeToolError("permission_denied");
    }

    [Test]
    public async Task an_mcp_cancel_of_a_completed_workshop_is_a_validation_error()
    {
        // Arrange
        var (_, workshop) = await AddWorkshopAndSignInAsync("Admin");
        (await FactoryClient.PostJsonRequestAsync(ApiRoutes.Workshops.Schedule(workshop.Id), new { }))
            .StatusCode.ShouldBe(HttpStatusCode.OK);
        (await FactoryClient.PostJsonRequestAsync(ApiRoutes.Workshops.Complete(workshop.Id), new { }))
            .StatusCode.ShouldBe(HttpStatusCode.OK);
        await using var client = await CreateMcpClientAsync();

        // Act
        var result = await client.CallToolAsync("cancel_workshop",
            new Dictionary<string, object?> { ["id"] = workshop.Id });

        // Assert
        result.ShouldBeToolError("invalid_argument");
    }

    private sealed record VenueIdOnly(string Id);

    private async Task<Tenant> AddTenantAndSignInAsync(string role)
    {
        await StartGraph()
            .AddTenant(out var tenant)
            .AddUser(out var user, u => u.Role = role)
            .BuildAsync();
        FactoryClient.SignInAsAppUser(user.Identifier);
        return tenant;
    }

    private async Task<(Tenant Tenant, Workshop Workshop)> AddWorkshopAndSignInAsync(
        string role, Action<WorkshopInput>? configure = null, TimeZoneId? zone = null)
    {
        await StartGraph()
            .AddTenant(out var tenant)
            .AddUser(out var user, u => u.Role = role)
            .Under(tenant).AddVenue(out _, v => v.TimeZone = zone?.Value ?? v.TimeZone)
            .AddWorkshop(out var workshop, w => configure?.Invoke(w))
            .BuildAsync();
        FactoryClient.SignInAsAppUser(user.Identifier);
        return (tenant, workshop);
    }
}
