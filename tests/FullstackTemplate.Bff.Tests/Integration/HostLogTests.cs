namespace FullstackTemplate.Bff.Tests.Integration;

using FullstackTemplate.Bff.Sessions;
using FullstackTemplate.Bff.Tests.Fakes;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;
using Serilog.Core;
using Shouldly;

public class HostLogTests
{
    [Test]
    public async Task a_failed_command_logs_to_the_sinks_of_its_own_host_and_not_of_a_host_that_started_later()
    {
        var tag = $"host_log_tests.{Guid.NewGuid():N}";
        var failingLogs = new CapturedLogs();
        var laterLogs = new CapturedLogs();
        await using var failing = BffApp.StartAnotherInstance(services: services =>
        {
            services.AddSingleton<ILogEventSink>(failingLogs);
            services.ConfigureDbContext<BffDbContext>(options => options.AddInterceptors(new FailingCommandInterceptor(tag)));
        });
        await using var later = BffApp.StartAnotherInstance(services: services => services.AddSingleton<ILogEventSink>(laterLogs));

        await using (var scope = failing.Services.CreateAsyncScope())
        {
            var dbContext = scope.ServiceProvider.GetRequiredService<BffDbContext>();
            await Should.ThrowAsync<NpgsqlException>(() => dbContext.UsedLogoutTokens.TagWith(tag).ToListAsync());
        }

        laterLogs.WithPropertyContaining(tag).ShouldBeEmpty();
        failingLogs.WithPropertyContaining(tag).ShouldNotBeEmpty();
    }
}
