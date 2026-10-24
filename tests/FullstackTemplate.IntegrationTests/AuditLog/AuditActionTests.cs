namespace FullstackTemplate.IntegrationTests.AuditLog;

using System.Text.Json;
using FullstackTemplate.IntegrationTests.DomainEvents;
using FullstackTemplate.Server.Databases;
using FullstackTemplate.Server.Domain.AuditEntries;
using FullstackTemplate.Server.Domain.Tenants;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Shouldly;
using static FullstackTemplate.IntegrationTests.DomainEvents.DomainEventTestServices;

public class AuditActionTests : TestBase
{
    [Test]
    public async Task action_of_a_request_that_changes_no_data_is_saved_under_its_subject()
    {
        // Arrange
        await using var services = TestFixture.BuildServiceProvider(AddExportRequests);
        var scope = new TestingServiceScope(services);
        var scenario = await scope.StartTenantScenarioAsync();

        // Act
        await scope.SendAsync(new ExportUsers.Command(scenario.Tenant.Id, Save: true));

        // Assert
        var action = (await EntriesOfRootAsync(scenario.Tenant.Id)).Single(e => e.Kind == AuditEntryKind.Action);
        action.Action.ShouldBe("test_users_exported");
        JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(action.Data)!
            .Select(p => $"{p.Key}={p.Value}").Order().ShouldBe(["Count=3", "Format=csv"]);
        (action.EntityType, action.EntityId, action.RootType, action.TenantId)
            .ShouldBe((nameof(Tenant), scenario.Tenant.Id, nameof(Tenant), scenario.Tenant.Id));
        (action.ActorSubject, action.ActorUserId, action.RequestName)
            .ShouldBe((scenario.Admin.Identifier, scenario.Admin.Id, "ExportUsers.Command"));
    }

    [Test]
    public async Task action_next_to_a_change_is_in_the_record_of_that_change_after_it()
    {
        // Arrange
        var scope = new TestingServiceScope();
        var scenario = await scope.StartTenantScenarioAsync();
        var db = scope.GetService<AppDbContext>();
        var user = await db.Users.SingleAsync(u => u.Id == scenario.FirstUser.Id);

        // Act
        user.UpdateFromIdp($"{user.FirstName}x", user.LastName, user.Email, user.Username);
        db.RecordAction(new UsersExported(AuditSubject.Of(user), "csv", 1));
        await db.SaveChangesAsync();

        // Assert
        var entries = await EntriesOfRootAsync(user.Id);
        var update = entries.Single(e => e.Kind == AuditEntryKind.Updated);
        var action = entries.Single(e => e.Kind == AuditEntryKind.Action);
        action.RecordId.ShouldBe(update.RecordId);
        action.Ordinal.ShouldBeGreaterThan(update.Ordinal);
        (action.EntityType, action.EntityId, action.TenantId).ShouldBe(("User", user.Id, scenario.Tenant.Id));
    }

    [Test]
    public async Task request_that_records_an_action_and_does_not_save_it_fails()
    {
        // Arrange
        await using var services = TestFixture.BuildServiceProvider(AddExportRequests);
        var scope = new TestingServiceScope(services);
        var scenario = await scope.StartTenantScenarioAsync();

        // Act
        var act = () => scope.SendAsync(new ExportUsers.Command(scenario.Tenant.Id, Save: false));

        // Assert
        (await act.ShouldThrowAsync<InvalidOperationException>()).Message.ShouldBe(
            "ExportUsers.Command recorded an audit action and did not call SaveChangesAsync. Save it, also when the handler changes no data.");
    }

    [Test]
    public async Task action_of_a_nested_request_is_saved_by_the_outer_request_under_the_outer_name()
    {
        // Arrange
        await using var services = TestFixture.BuildServiceProvider(AddExportRequests);
        var scope = new TestingServiceScope(services);
        var scenario = await scope.StartTenantScenarioAsync();

        // Act
        await scope.SendAsync(new ExportUsersThenSave.Command(scenario.Tenant.Id));

        // Assert
        (await EntriesOfRootAsync(scenario.Tenant.Id)).Single(e => e.Kind == AuditEntryKind.Action)
            .RequestName.ShouldBe("ExportUsersThenSave.Command");
    }

    private static void AddExportRequests(IServiceCollection services) => services
        .AddTransient<IRequestHandler<ExportUsers.Command>, ExportUsers.Handler>()
        .AddTransient<IRequestHandler<ExportUsersThenSave.Command>, ExportUsersThenSave.Handler>();

    private static Task<List<AuditEntry>> EntriesOfRootAsync(string rootId)
        => ReadAsync(read => read.AuditEntries.Where(e => e.RootId == rootId).OrderBy(e => e.Ordinal).ToListAsync());

    [AuditActionName("test_users_exported")]
    public sealed record UsersExported(AuditSubject Subject, string Format, int Count) : AuditAction(Subject);

    public static class ExportUsers
    {
        public sealed record Command(string TenantId, bool Save) : IRequest;

        public sealed class Handler(AppDbContext dbContext) : IRequestHandler<Command>
        {
            public async Task Handle(Command request, CancellationToken cancellationToken)
            {
                var tenant = await dbContext.Tenants.SingleAsync(t => t.Id == request.TenantId, cancellationToken);
                dbContext.RecordAction(new UsersExported(AuditSubject.Of(tenant), "csv", 3));
                if (request.Save)
                    await dbContext.SaveChangesAsync(cancellationToken);
            }
        }
    }

    public static class ExportUsersThenSave
    {
        public sealed record Command(string TenantId) : IRequest;

        public sealed class Handler(ISender sender, AppDbContext dbContext) : IRequestHandler<Command>
        {
            public async Task Handle(Command request, CancellationToken cancellationToken)
            {
                await sender.Send(new ExportUsers.Command(request.TenantId, Save: false), cancellationToken);
                await dbContext.SaveChangesAsync(cancellationToken);
            }
        }
    }
}
