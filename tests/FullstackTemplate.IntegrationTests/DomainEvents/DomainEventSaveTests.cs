namespace FullstackTemplate.IntegrationTests.DomainEvents;

using FullstackTemplate.Server.Databases;
using FullstackTemplate.Server.Domain.Tenants.DomainEvents;
using FullstackTemplate.Server.Domain.Tenants.Dtos;
using FullstackTemplate.Server.Domain.Tenants.Features;
using FullstackTemplate.Server.Domain.Tenants.Models;
using FullstackTemplate.Server.Domain.Users;
using FullstackTemplate.Server.Domain.Users.DomainEvents;
using FullstackTemplate.Server.Exceptions;
using Microsoft.EntityFrameworkCore;
using Shouldly;
using static DomainEventTestServices;

public class DomainEventSaveTests : TestBase
{
    [Test]
    public async Task handler_change_commits_with_the_save()
    {
        // Arrange
        var feeIdentifier = $"fee-{Guid.NewGuid()}";
        await using var services = TestFixture.BuildServiceProvider(s => s.AddTestHandler<TenantUpdated>((e, db, _) =>
        {
            db.Users.Add(NewUser(e.Id, feeIdentifier));
            return Task.CompletedTask;
        }));
        var scope = new TestingServiceScope(services);
        var scenario = await scope.StartTenantScenarioAsync();
        var db = scope.GetService<AppDbContext>();
        var tenant = await db.Tenants.SingleAsync(t => t.Id == scenario.Tenant.Id);
        var newName = $"renamed-{Guid.NewGuid()}";

        // Act
        tenant.Update(new TenantForUpdate { Name = newName });
        await db.SaveChangesAsync();

        // Assert
        (await ReadAsync(read => read.Tenants.Where(t => t.Id == tenant.Id).Select(t => t.Name).SingleAsync()))
            .ShouldBe(newName);
        (await ReadAsync(read => read.Users.Where(u => u.Identifier == feeIdentifier).Select(u => u.TenantId).ToListAsync()))
            .ShouldBe([tenant.Id]);
    }

    [Test]
    public async Task handler_failure_saves_nothing_and_ends_the_context()
    {
        // Arrange
        string? userId = null;
        var failures = 1;
        await using var services = TestFixture.BuildServiceProvider(s => s.AddTestHandler<TenantUpdated>(async (_, db, ct) =>
        {
            var user = await db.Users.SingleAsync(u => u.Id == userId, ct);
            user.UpdateRole(UserRole.Admin());
            if (failures-- > 0)
                throw new InvalidOperationException("The handler failed.");
        }));
        var scope = new TestingServiceScope(services);
        var scenario = await scope.StartTenantScenarioAsync();
        userId = scenario.FirstUser.Id;
        var db = scope.GetService<AppDbContext>();
        var tenant = await db.Tenants.SingleAsync(t => t.Id == scenario.Tenant.Id);
        var newName = $"renamed-{Guid.NewGuid()}";

        // Act
        tenant.Update(new TenantForUpdate { Name = newName });
        var act = () => db.SaveChangesAsync();

        // Assert
        (await act.ShouldThrowAsync<InvalidOperationException>()).Message.ShouldBe("The handler failed.");
        (await ReadAsync(read => read.Tenants.Where(t => t.Id == tenant.Id).Select(t => t.Name).SingleAsync()))
            .ShouldBe(scenario.Tenant.Name);
        (await ReadAsync(read => read.Users.SingleAsync(u => u.Id == userId))).Role.ShouldBe(UserRole.User());

        var saveAgain = () => db.SaveChangesAsync();
        (await saveAgain.ShouldThrowAsync<InvalidOperationException>()).Message.ShouldContain("Use a new DI scope");

        var retryScope = new TestingServiceScope(services);
        retryScope.SetUserWithDefaults(identifier: scenario.Admin.Identifier);
        var retryDb = retryScope.GetService<AppDbContext>();
        (await retryDb.Tenants.SingleAsync(t => t.Id == tenant.Id)).Update(new TenantForUpdate { Name = newName });
        await retryDb.SaveChangesAsync();
        (await ReadAsync(read => read.Tenants.Where(t => t.Id == tenant.Id).Select(t => t.Name).SingleAsync()))
            .ShouldBe(newName);
        (await ReadAsync(read => read.Users.SingleAsync(u => u.Id == userId))).Role.ShouldBe(UserRole.Admin());
    }

    [Test]
    public async Task client_error_from_a_handler_reaches_the_caller_and_saves_nothing()
    {
        // Arrange
        await using var services = TestFixture.BuildServiceProvider(s => s.AddTestHandler<TenantUpdated>((_, _, _) =>
            throw new ValidationException("name", "The handler refused the name.")));
        var scope = new TestingServiceScope(services);
        var scenario = await scope.StartTenantScenarioAsync();

        // Act
        var act = () => scope.SendAsync(new UpdateTenant.Command(scenario.Tenant.Id,
            new TenantForUpdateDto { Name = $"renamed-{Guid.NewGuid()}" }));

        // Assert
        var exception = await act.ShouldThrowAsync<ValidationException>();
        exception.Property.ShouldBe("name");
        exception.Message.ShouldBe("The handler refused the name.");
        (await ReadAsync(read => read.Tenants.Where(t => t.Id == scenario.Tenant.Id).Select(t => t.Name).SingleAsync()))
            .ShouldBe(scenario.Tenant.Name);
    }

    [Test]
    public async Task events_that_handlers_queue_run_in_later_rounds_in_queue_order()
    {
        // Arrange
        var runs = new List<string>();
        string? adminId = null;
        await using var services = TestFixture.BuildServiceProvider(s => s
            .AddTestHandler<UserUpdated>((e, _, _) =>
            {
                runs.Add($"{nameof(UserUpdated)}:{e.Id}");
                return Task.CompletedTask;
            })
            .AddTestHandler<TenantUpdated>(async (e, db, ct) =>
            {
                runs.Add($"{nameof(TenantUpdated)}:{e.Id}");
                (await db.Users.SingleAsync(u => u.Id == adminId, ct)).UpdateRole(UserRole.Admin());
            }));
        var scope = new TestingServiceScope(services);
        var scenario = await scope.StartTenantScenarioAsync();
        adminId = scenario.Admin.Id;
        var db = scope.GetService<AppDbContext>();
        var tenant = await db.Tenants.SingleAsync(t => t.Id == scenario.Tenant.Id);
        var firstUser = await db.Users.SingleAsync(u => u.Id == scenario.FirstUser.Id);
        var secondUser = await db.Users.SingleAsync(u => u.Id == scenario.SecondUser.Id);

        // Act
        secondUser.UpdateRole(UserRole.User());
        tenant.Update(new TenantForUpdate { Name = $"renamed-{Guid.NewGuid()}" });
        firstUser.UpdateRole(UserRole.User());
        await db.SaveChangesAsync();

        // Assert
        runs.ShouldBe([
            $"{nameof(UserUpdated)}:{secondUser.Id}",
            $"{nameof(TenantUpdated)}:{tenant.Id}",
            $"{nameof(UserUpdated)}:{firstUser.Id}",
            $"{nameof(UserUpdated)}:{adminId}",
        ]);
    }

    [Test]
    public async Task handlers_that_form_a_cycle_make_the_save_throw_and_save_nothing()
    {
        // Arrange
        var runs = 0;
        await using var services = TestFixture.BuildServiceProvider(s => s.AddTestHandler<TenantUpdated>(async (e, db, ct) =>
        {
            runs++;
            var tenant = await db.Tenants.SingleAsync(t => t.Id == e.Id, ct);
            tenant.Update(new TenantForUpdate { Name = tenant.Name });
        }));
        var scope = new TestingServiceScope(services);
        var scenario = await scope.StartTenantScenarioAsync();
        var db = scope.GetService<AppDbContext>();
        var tenant = await db.Tenants.SingleAsync(t => t.Id == scenario.Tenant.Id);

        // Act
        tenant.Update(new TenantForUpdate { Name = $"renamed-{Guid.NewGuid()}" });
        var act = () => db.SaveChangesAsync();

        // Assert
        (await act.ShouldThrowAsync<InvalidOperationException>()).Message.ShouldContain(nameof(TenantUpdated));
        runs.ShouldBe(10);
        (await ReadAsync(read => read.Tenants.Where(t => t.Id == tenant.Id).Select(t => t.Name).SingleAsync()))
            .ShouldBe(scenario.Tenant.Name);
    }

    [Test]
    public async Task save_with_a_token_runs_each_handler_once_and_a_later_save_does_not_run_it_again()
    {
        // Arrange
        var runs = 0;
        await using var services = TestFixture.BuildServiceProvider(s => s.AddTestHandler<TenantUpdated>((_, _, _) =>
        {
            runs++;
            return Task.CompletedTask;
        }));
        var scope = new TestingServiceScope(services);
        var scenario = await scope.StartTenantScenarioAsync();
        var db = scope.GetService<AppDbContext>();
        var tenant = await db.Tenants.SingleAsync(t => t.Id == scenario.Tenant.Id);
        using var cancellation = new CancellationTokenSource();

        // Act
        tenant.Update(new TenantForUpdate { Name = $"renamed-{Guid.NewGuid()}" });
        await db.SaveChangesAsync(cancellation.Token);
        var runsAfterFirstSave = runs;
        await db.SaveChangesAsync(cancellation.Token);

        // Assert
        runsAfterFirstSave.ShouldBe(1);
        runs.ShouldBe(1);
    }

    [Test]
    public async Task save_inside_an_outer_transaction_is_refused_and_keeps_the_context_usable()
    {
        // Arrange
        var runs = 0;
        await using var services = TestFixture.BuildServiceProvider(s => s.AddTestHandler<TenantUpdated>((_, _, _) =>
        {
            runs++;
            return Task.CompletedTask;
        }));
        var scope = new TestingServiceScope(services);
        var scenario = await scope.StartTenantScenarioAsync();
        var db = scope.GetService<AppDbContext>();
        var tenant = await db.Tenants.SingleAsync(t => t.Id == scenario.Tenant.Id);
        var newName = $"renamed-{Guid.NewGuid()}";
        tenant.Update(new TenantForUpdate { Name = newName });

        // Act
        await using (await db.Database.BeginTransactionAsync())
        {
            var act = () => db.SaveChangesAsync();
            await act.ShouldThrowAsync<NotSupportedException>();
        }

        // Assert
        runs.ShouldBe(0);
        await db.SaveChangesAsync();
        runs.ShouldBe(1);
        (await ReadAsync(read => read.Tenants.Where(t => t.Id == tenant.Id).Select(t => t.Name).SingleAsync()))
            .ShouldBe(newName);
    }
}
