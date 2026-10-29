namespace FullstackTemplate.UnitTests.Examples.Workshops;

using FullstackTemplate.Server.Domain.Users;
using FullstackTemplate.Server.Examples.Venues;
using FullstackTemplate.Server.Examples.Workshops;
using FullstackTemplate.Server.Examples.Workshops.Mappings;
using FullstackTemplate.Server.Examples.Workshops.Models;
using FullstackTemplate.Server.Exceptions;
using FullstackTemplate.SharedTestHelpers.EntityGraphs;
using FullstackTemplate.SharedTestHelpers.Examples.EntityGraphs;
using Shouldly;

public class WorkshopActionTests
{
    private const string OrganizerId = "usr_organizer";
    private static readonly string[] Statuses = ["Draft", "Scheduled", "Cancelled", "Completed"];

    private static readonly Caller[] Callers =
    [
        Caller.AllTenantsManager.Instance,
        new Caller.Outsider(OutsiderKind.Anonymous),
        new Caller.Member("usr_caller", []),
        new Caller.Member("usr_caller", [Permission.ManageWorkshops()]),
        new Caller.Member("usr_caller", [Permission.ManageTenant()]),
        new Caller.Member("usr_caller", [Permission.ManageWorkshops(), Permission.ManageTenant()]),
        new Caller.Member(OrganizerId, []),
        new Caller.Member(OrganizerId, [Permission.ManageWorkshops()]),
        new Caller.DelegatedAgent("planner", Permission.GetAll(), [Permission.ReadUsers()]),
        new Caller.DelegatedAgent("planner", Permission.GetAll(), [Permission.ManageWorkshops()]),
        new Caller.MachineAgent("planner", [Permission.ManageWorkshops()]),
        new Caller.MachineAgent("planner", [Permission.ManageWorkshops(), Permission.ManageTenant()]),
    ];

    public static IEnumerable<Func<(string Status, Caller Caller)>> StatusesAndCallers()
        => Statuses.SelectMany(status => Callers.Select(caller => (Func<(string, Caller)>)(() => (status, caller))));

    [Test]
    [MethodDataSource(nameof(StatusesAndCallers))]
    public void an_action_is_allowed_only_when_its_command_goes_through(string status, Caller caller)
    {
        // Arrange
        string[] actions = ["Update", "Schedule", "Cancel", "Complete"];

        // Act
        var allowed = Arrange(status).AllowedActions(caller).Select(a => a.Value).ToList();
        var goesThrough = actions.Where(action => GoesThrough(action, status, caller)).ToList();

        // Assert
        allowed.ShouldBe(goesThrough);
    }

    [Test]
    public void a_row_of_the_list_gets_the_actions_of_its_record()
    {
        // Arrange
        var caller = new Caller.Member(OrganizerId, [Permission.ManageWorkshops()]);
        var arranged = Statuses.Select(ArrangeAtVenue).ToList();
        var rows = arranged
            .Select(a => a.Workshop.ToWorkshopDto(a.Venue, Caller.AllTenantsManager.Instance) with { AllowedActions = [] })
            .ToList();

        // Act
        rows.AddAllowedActions(caller);

        // Assert
        rows.Select(r => r.AllowedActions).ShouldBe(arranged.Select(a => a.Workshop.ToWorkshopDto(a.Venue, caller).AllowedActions));
    }

    [Test]
    public void a_caller_without_manage_workshops_gets_a_403_before_the_load()
    {
        // Act
        var act = () => WorkshopAction.Cancel().GuardCaller(new Caller.Member("usr_caller", []));

        // Assert
        act.ShouldThrow<ForbiddenAccessException>().Message.ShouldBe("You cannot manage workshops. Ask a tenant admin for access.");
    }

    [Test]
    public void a_member_who_is_not_the_organizer_gets_the_refusal_of_the_row()
    {
        // Arrange
        var workshop = Arrange("Draft");

        // Act
        var act = () => workshop.GuardChangeBy(new Caller.Member("usr_caller", [Permission.ManageWorkshops()]));

        // Assert
        act.ShouldThrow<ForbiddenAccessException>().Message
            .ShouldBe("Only the organizer of this workshop or a tenant admin can change it.");
    }

    [Test]
    public void a_workshop_with_no_organizer_is_changed_only_by_a_caller_who_can_manage_the_tenant()
    {
        // Arrange
        TestGraph.Start(out _, Clock.Fixed).AddVenue(out _).AddWorkshop(out var workshop);

        // Act
        var byMember = () => workshop.GuardChangeBy(new Caller.Member("usr_caller", [Permission.ManageWorkshops()]));
        var byAdmin = () => workshop.GuardChangeBy(new Caller.Member("usr_caller", [Permission.ManageTenant()]));

        // Assert
        byMember.ShouldThrow<ForbiddenAccessException>();
        byAdmin.ShouldNotThrow();
    }

    private static bool GoesThrough(string action, string status, Caller caller)
    {
        var (workshop, venue) = ArrangeAtVenue(status);
        try
        {
            ActionOf(action).GuardCaller(caller);
            workshop.GuardChangeBy(caller);
            var before = workshop.Status;
            switch (action)
            {
                case "Update":
                    workshop.Update(new WorkshopForUpdate
                    {
                        Title = "Renamed", StartsAt = workshop.StartsAt, Capacity = 3, Version = workshop.Version,
                    });
                    return true;
                case "Schedule":
                    if (WorkshopScheduling.Decide(new ScheduleCase(workshop, venue, Clock.Now, workshop.Version)) is not ScheduleDecision.Offered offered)
                        return false;
                    workshop.Schedule(offered.Start);
                    break;
                case "Cancel":
                    workshop.Cancel();
                    break;
                case "Complete":
                    workshop.Complete();
                    break;
            }
            return workshop.Status != before;
        }
        catch (Exception e) when (e is ForbiddenAccessException or ValidationException)
        {
            return false;
        }
    }

    private static WorkshopAction ActionOf(string name) => name switch
    {
        "Update" => WorkshopAction.Update(),
        "Schedule" => WorkshopAction.Schedule(),
        "Cancel" => WorkshopAction.Cancel(),
        "Complete" => WorkshopAction.Complete(),
        _ => throw new ArgumentOutOfRangeException(nameof(name), name, null),
    };

    private static Workshop Arrange(string status) => ArrangeAtVenue(status).Workshop;

    private static (Workshop Workshop, Venue Venue) ArrangeAtVenue(string status)
    {
        TestGraph.Start(out _, Clock.Fixed).AddVenue(out var venue).AddWorkshop(out var workshop, w => w.OrganizerId = OrganizerId);
        if (status is "Scheduled" or "Completed")
        {
            var decision = WorkshopScheduling.Decide(new ScheduleCase(workshop, venue, Clock.Now, workshop.Version));
            workshop.Schedule(decision.ShouldBeOfType<ScheduleDecision.Offered>().Start);
        }
        if (status == "Cancelled")
            workshop.Cancel();
        if (status == "Completed")
            workshop.Complete();
        return (workshop, venue);
    }
}
