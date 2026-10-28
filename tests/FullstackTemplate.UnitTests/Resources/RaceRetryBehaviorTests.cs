namespace FullstackTemplate.UnitTests.Resources;

using System.Diagnostics;
using FullstackTemplate.Server.Databases;
using FullstackTemplate.Server.Domain.Tenants;
using FullstackTemplate.Server.Exceptions;
using FullstackTemplate.Server.Resources;
using FullstackTemplate.Server.Services;
using FullstackTemplate.SharedTestHelpers.Fakes.Tenant;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Time.Testing;
using NSubstitute;
using Shouldly;

public sealed class RaceRetryBehaviorTests : IDisposable
{
    private readonly ActivitySource _testSource = new("FullstackTemplate.UnitTests.RaceRetry");
    private readonly ActivityListener _listener;

    public RaceRetryBehaviorTests()
    {
        _listener = new ActivityListener
        {
            ShouldListenTo = source => source.Name == "FullstackTemplate.UnitTests.RaceRetry",
            Sample = (ref ActivityCreationOptions<ActivityContext> _) => ActivitySamplingResult.AllDataAndRecorded,
        };
        ActivitySource.AddActivityListener(_listener);
    }

    public void Dispose()
    {
        _listener.Dispose();
        _testSource.Dispose();
        GC.SuppressFinalize(this);
    }

    [Test]
    public async Task command_that_loses_one_time_runs_again_on_an_empty_tracker_and_succeeds()
    {
        // Arrange
        using var span = _testSource.StartActivity("request").ShouldNotBeNull();
        using var dbContext = CreateContext();
        var trackedAtEachRun = new List<int>();
        RequestHandlerDelegate<string> handler = _ =>
        {
            trackedAtEachRun.Add(dbContext.ChangeTracker.Entries().Count());
            if (trackedAtEachRun.Count == 1)
            {
                dbContext.Add(Tenant.Create(new FakeTenantForCreation().Generate()));
                throw new StaleVersionException();
            }
            return Task.FromResult("saved");
        };

        // Act
        var result = await new RaceRetryBehavior<RacedCommand, string>(dbContext, TimeProvider.System)
            .Handle(new RacedCommand(), handler, CancellationToken.None);

        // Assert
        result.ShouldBe("saved");
        trackedAtEachRun.ShouldBe([0, 0]);
        var lost = span.Events.ShouldHaveSingleItem();
        lost.Name.ShouldBe("race.lost");
        lost.Tags.ShouldContain(new KeyValuePair<string, object?>("app.race.run", 1));
    }

    [Test]
    public async Task command_that_loses_each_run_is_a_concurrent_change_after_the_last_run()
    {
        // Arrange
        using var span = _testSource.StartActivity("request").ShouldNotBeNull();
        using var dbContext = CreateContext();
        var clock = new FakeTimeProvider();
        var runs = 0;
        RequestHandlerDelegate<string> handler = _ =>
        {
            runs++;
            throw new StaleVersionException();
        };

        // Act
        var handle = new RaceRetryBehavior<RacedCommand, string>(dbContext, clock)
            .Handle(new RacedCommand(), handler, CancellationToken.None);
        while (!handle.IsCompleted)
        {
            clock.Advance(TimeSpan.FromMilliseconds(RaceRetryBehavior<RacedCommand, string>.MaxWaitMs));
            await Task.Yield();
        }

        // Assert
        const int maxRuns = RaceRetryBehavior<RacedCommand, string>.MaxRuns;
        var exception = await Should.ThrowAsync<ConcurrentChangeException>(handle);
        exception.InnerException.ShouldBeOfType<StaleVersionException>();
        runs.ShouldBe(maxRuns);
        span.Events.Select(e => e.Name).ShouldBe([.. Enumerable.Repeat("race.lost", maxRuns - 1), "race.exhausted"]);
        span.Events.Last().Tags.ShouldContain(new KeyValuePair<string, object?>("app.race.run", maxRuns));
    }

    [Test]
    public async Task other_failure_is_not_run_again()
    {
        // Arrange
        using var dbContext = CreateContext();
        var runs = 0;
        RequestHandlerDelegate<string> handler = _ =>
        {
            runs++;
            throw new ConflictException("name", "A tenant with this name already exists.");
        };

        // Act
        var act = () => new RaceRetryBehavior<RacedCommand, string>(dbContext, TimeProvider.System)
            .Handle(new RacedCommand(), handler, CancellationToken.None);

        // Assert
        await act.ShouldThrowAsync<ConflictException>();
        runs.ShouldBe(1);
    }

    private sealed record RacedCommand : IRequest<string>, IRunsAgainWhenRaced;

    private static AppDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseNpgsql("Host=unused")
            .UseSnakeCaseNamingConvention()
            .Options;
        return new AppDbContext(options, TimeProvider.System, Substitute.For<ICurrentUserService>(),
            Substitute.For<ICallerIdentityResolver>(), Substitute.For<ITenantScopeProvider>(),
            new DomainEventDispatcher(new ServiceCollection().BuildServiceProvider()));
    }
}
