namespace FullstackTemplate.IntegrationTests.Databases;

using FullstackTemplate.Server.Databases;
using FullstackTemplate.SharedTestHelpers.Utilities;
using Microsoft.EntityFrameworkCore;
using Shouldly;

public class PauseAtQueryTests : TestBase
{
    [Test]
    [Arguments(false)]
    [Arguments(true)]
    public async Task query_with_the_tag_waits_for_the_release(bool after)
    {
        // Arrange
        var pause = after ? PauseAtQuery.After("pause_test.tagged") : PauseAtQuery.Before("pause_test.tagged");
        await using var services = pause.BuildServices();
        var dbContext = new TestingServiceScope(services).GetService<AppDbContext>();

        // Act
        var read = dbContext.Tenants.TagWith("pause_test.tagged").IgnoreQueryFilters().CountAsync();
        await pause.ReachedAsync();
        var finishedWhileHeld = await Task.WhenAny(read, Task.Delay(TimeSpan.FromMilliseconds(200))) == read;
        pause.Release();

        // Assert
        finishedWhileHeld.ShouldBeFalse();
        (await read).ShouldBeGreaterThan(0);
    }

    [Test]
    public async Task query_with_another_tag_does_not_wait()
    {
        // Arrange
        var pause = PauseAtQuery.Before("pause_test.tagged", TimeSpan.FromMilliseconds(200));
        await using var services = pause.BuildServices();
        var dbContext = new TestingServiceScope(services).GetService<AppDbContext>();

        // Act
        await dbContext.Tenants.TagWith("pause_test.tagged_other").IgnoreQueryFilters().CountAsync();
        await dbContext.Tenants.TagWith("other.pause_test.tagged").IgnoreQueryFilters().CountAsync();

        // Assert
        await Should.ThrowAsync<TimeoutException>(pause.ReachedAsync());
    }

    [Test]
    public async Task reached_times_out_with_the_tag_in_its_message()
    {
        // Arrange
        var pause = PauseAtQuery.After("pause_test.never", TimeSpan.FromMilliseconds(100));

        // Act
        var exception = await Should.ThrowAsync<TimeoutException>(pause.ReachedAsync());

        // Assert
        exception.Message.ShouldContain("'pause_test.never'");
    }

    [Test]
    public async Task held_command_that_the_test_does_not_release_times_out_with_the_tag_in_its_message()
    {
        // Arrange
        var pause = PauseAtQuery.Before("pause_test.tagged", TimeSpan.FromMilliseconds(200));
        await using var services = pause.BuildServices();
        var dbContext = new TestingServiceScope(services).GetService<AppDbContext>();

        // Act
        var read = dbContext.Tenants.TagWith("pause_test.tagged").IgnoreQueryFilters().CountAsync();

        // Assert
        var exception = await Should.ThrowAsync<InvalidOperationException>(read);
        exception.Message.ShouldContain("'pause_test.tagged'");
    }

    [Test]
    public async Task wait_until_blocked_returns_when_the_rival_waits_on_a_lock_of_the_held_request()
    {
        // Arrange
        var key = Random.Shared.NextInt64();
        var pause = PauseAtQuery.After("pause_test.lock");
        await using var services = pause.BuildServices();
        var database = new TestingServiceScope(services).GetService<AppDbContext>().Database;
        var underTest = database.CreateExecutionStrategy().ExecuteAsync(async () =>
        {
            await using var transaction = await database.BeginTransactionAsync();
            await database.SqlQuery<int>($"""
                -- pause_test.lock
                SELECT 1 AS "Value" FROM pg_advisory_xact_lock({key})
                """).ToListAsync();
            await transaction.CommitAsync();
        });
        await pause.ReachedAsync();

        // Act
        var rival = TestFixture.ExecuteOutOfBandAsync("SELECT pg_advisory_xact_lock(@key)", ("key", key));
        await pause.WaitUntilBlockedAsync(rival);

        // Assert
        rival.IsCompleted.ShouldBeFalse();
        pause.Release();
        await underTest;
        await rival;
    }

    [Test]
    public async Task held_requests_leave_the_pooled_connections_to_the_rivals()
    {
        // Arrange
        var pauses = Enumerable.Range(0, PostgresTestDatabase.MaxPoolSize)
            .Select(_ => PauseAtQuery.Before("pause_test.tagged"))
            .ToList();
        var providers = pauses.Select(pause => pause.BuildServices()).ToList();
        var reads = providers
            .Select(services => new TestingServiceScope(services).GetService<AppDbContext>()
                .Tenants.TagWith("pause_test.tagged").IgnoreQueryFilters().CountAsync())
            .ToList();
        try
        {
            await Task.WhenAll(pauses.Select(pause => pause.ReachedAsync()));

            // Act
            var rival = await new TestingServiceScope().ExecuteDbContextAsync(db =>
                db.Tenants.IgnoreQueryFilters().CountAsync()).WaitAsync(TimeSpan.FromSeconds(10));

            // Assert
            rival.ShouldBeGreaterThan(0);
        }
        finally
        {
            pauses.ForEach(pause => pause.Release());
            await Task.WhenAll(reads);
            foreach (var services in providers)
                await services.DisposeAsync();
        }
    }
}
