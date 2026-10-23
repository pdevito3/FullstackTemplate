namespace FullstackTemplate.IntegrationTests.DomainEvents;

using System.Data.Common;
using FullstackTemplate.Server.Databases;
using FullstackTemplate.Server.Domain.Tenants.DomainEvents;
using FullstackTemplate.Server.Domain.Tenants.Dtos;
using FullstackTemplate.Server.Domain.Tenants.Features;
using FullstackTemplate.Server.Domain.Tenants.Models;
using FullstackTemplate.Server.Services;
using FullstackTemplate.SharedTestHelpers.EntityGraphs;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;
using Shouldly;
using static DomainEventTestServices;

public class DomainEventRetryTests : TestBase
{
    [Test]
    public async Task transient_failure_before_the_commit_is_retried_without_running_the_handlers_again()
    {
        // Arrange
        var runs = 0;
        var effectIdentifier = $"effect-{Guid.NewGuid()}";
        var failFirstWrite = new FailFirstWrite();
        await using var services = TestFixture.BuildServiceProvider(s => s
            .AddTestHandler<TenantUpdated>((e, db, _) =>
            {
                runs++;
                db.Users.Add(NewUser(e.Id, effectIdentifier));
                return Task.CompletedTask;
            })
            .ConfigureDbContext<AppDbContext>((_, options) => options.AddInterceptors(failFirstWrite)));
        var scope = new TestingServiceScope(services);
        var scenario = await scope.StartTenantScenarioAsync();
        var db = scope.GetService<AppDbContext>();
        db.Database.CreateExecutionStrategy().RetriesOnFailure.ShouldBeTrue();
        var tenant = await db.Tenants.SingleAsync(t => t.Id == scenario.Tenant.Id);
        var newName = $"renamed-{Guid.NewGuid()}";
        failFirstWrite.Arm();

        // Act
        tenant.Update(new TenantForUpdate { Name = newName, Version = tenant.Version });
        await db.SaveChangesAsync();

        // Assert
        failFirstWrite.Failures.ShouldBe(1);
        runs.ShouldBe(1);
        (await ReadAsync(read => read.Users.CountAsync(u => u.Identifier == effectIdentifier))).ShouldBe(1);
        (await ReadAsync(read => read.Tenants.Where(t => t.Id == tenant.Id).Select(t => t.Name).SingleAsync()))
            .ShouldBe(newName);
    }

    [Test]
    public async Task conflict_saves_nothing_and_a_resend_in_a_new_scope_runs_the_handlers_again()
    {
        // Arrange
        var runs = 0;
        var effectIdentifier = $"effect-{Guid.NewGuid()}";
        await using var services = TestFixture.BuildServiceProvider(s => s.AddTestHandler<TenantUpdated>((e, db, _) =>
        {
            runs++;
            db.Users.Add(NewUser(e.Id, effectIdentifier));
            return Task.CompletedTask;
        }));
        var scope = new TestingServiceScope(services);
        await scope.StartGraph(out _)
            .AddTenant(out _)
            .AddUser(out var blocker, u => u.Identifier = effectIdentifier)
            .BuildAsync();
        var scenario = await scope.StartTenantScenarioAsync();
        var command = new UpdateTenant.Command(scenario.Tenant.Id, new TenantForUpdateDto { Name = $"renamed-{Guid.NewGuid()}", Version = scenario.Tenant.Version });

        // Act
        var send = () => scope.SendAsync(command);

        // Assert
        (await send.ShouldThrowAsync<ConstraintConflictException>()).Constraint.ShouldBe("ix_users_identifier");
        runs.ShouldBe(1);
        (await ReadAsync(read => read.Tenants.Where(t => t.Id == scenario.Tenant.Id).Select(t => t.Name).SingleAsync()))
            .ShouldBe(scenario.Tenant.Name);
        (await ReadAsync(read => read.Users.Where(u => u.Identifier == effectIdentifier).Select(u => u.Id).ToListAsync()))
            .ShouldBe([blocker.Id]);

        var saveAgain = () => scope.GetService<AppDbContext>().SaveChangesAsync();
        (await saveAgain.ShouldThrowAsync<InvalidOperationException>()).Message.ShouldContain("Use a new DI scope");

        await using (var system = TestFixture.BaseScopeFactory.CreateSystemScope())
        {
            await system.ServiceProvider.GetRequiredService<AppDbContext>().Users.Where(u => u.Id == blocker.Id)
                .ExecuteUpdateAsync(set => set.SetProperty(u => u.Identifier, $"moved-{Guid.NewGuid()}"));
        }
        var resendScope = new TestingServiceScope(services);
        resendScope.SetUserWithDefaults(identifier: scenario.Admin.Identifier);
        await resendScope.SendAsync(command);

        runs.ShouldBe(2);
        (await ReadAsync(read => read.Tenants.Where(t => t.Id == scenario.Tenant.Id).Select(t => t.Name).SingleAsync()))
            .ShouldBe(command.Dto.Name);
        (await ReadAsync(read => read.Users.Where(u => u.Identifier == effectIdentifier).Select(u => u.TenantId).ToListAsync()))
            .ShouldBe([scenario.Tenant.Id]);
    }

    private sealed class FailFirstWrite : DbCommandInterceptor
    {
        private int _armed;

        public int Failures { get; private set; }

        public void Arm() => Interlocked.Exchange(ref _armed, 1);

        public override ValueTask<InterceptionResult<DbDataReader>> ReaderExecutingAsync(DbCommand command,
            CommandEventData eventData, InterceptionResult<DbDataReader> result, CancellationToken cancellationToken = default)
        {
            FailWhenArmed(command);
            return ValueTask.FromResult(result);
        }

        public override ValueTask<InterceptionResult<int>> NonQueryExecutingAsync(DbCommand command,
            CommandEventData eventData, InterceptionResult<int> result, CancellationToken cancellationToken = default)
        {
            FailWhenArmed(command);
            return ValueTask.FromResult(result);
        }

        private void FailWhenArmed(DbCommand command)
        {
            var isWrite = command.CommandText.Contains("INSERT INTO", StringComparison.Ordinal)
                          || command.CommandText.Contains("UPDATE ", StringComparison.Ordinal);
            if (!isWrite || Interlocked.Exchange(ref _armed, 0) == 0)
                return;

            Failures++;
            throw new PostgresException("Injected serialization failure.", "ERROR", "ERROR", PostgresErrorCodes.SerializationFailure);
        }
    }
}
