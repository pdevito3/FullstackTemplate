namespace FullstackTemplate.Bff.Tests.Integration;

using System.Globalization;
using FullstackTemplate.Bff.Tests.Fakes;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Serilog;
using Serilog.AspNetCore;
using Serilog.Events;
using Shouldly;

public class HealthCheckTests
{
    [Test]
    public async Task health_check_that_the_caller_leaves_logs_no_error()
    {
        // Arrange
        var started = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var requestLogs = new CapturedLogs();
        await using var requestLogger = new LoggerConfiguration().MinimumLevel.Verbose().WriteTo.Sink(requestLogs).CreateLogger();
        await using var app = BffApp.StartAnotherInstance(services: services =>
        {
            services.AddHealthChecks().AddAsyncCheck("stalling", async cancellationToken =>
            {
                // Like the EF Core DbContext check, it reports each exception as Unhealthy
                try
                {
                    started.SetResult();
                    await Task.Delay(Timeout.Infinite, cancellationToken);
                    return HealthCheckResult.Healthy();
                }
                catch (Exception exception)
                {
                    return HealthCheckResult.Unhealthy(exception.Message, exception);
                }
            });
            // The logger of the app drops the Verbose request log of a health probe, and another test host can close it
            services.AddOptions<RequestLoggingOptions>().Configure(options => options.Logger = requestLogger);
        });
        using var browser = app.CreateBrowser();
        using var leave = new CancellationTokenSource();

        // Act
        var probe = browser.GetAsync("/health", leave.Token);
        await started.Task.WaitAsync(TimeSpan.FromSeconds(30));
        await leave.CancelAsync();
        await Should.ThrowAsync<OperationCanceledException>(probe);

        // Assert
        var requestLog = await WaitForRequestLogAsync(requestLogs);
        requestLog.Properties["StatusCode"].ToString().ShouldBe("499");
        requestLog.Level.ShouldBe(LogEventLevel.Verbose);
        BffApp.Logs.InTrace(requestLog.TraceId.ShouldNotBeNull())
            .Where(e => e.Level >= LogEventLevel.Error)
            .Select(e => e.RenderMessage(CultureInfo.InvariantCulture))
            .ShouldBeEmpty();
    }

    // The BFF ends the request after the client leaves, so its request log comes a little later
    private static async Task<LogEvent> WaitForRequestLogAsync(CapturedLogs requestLogs)
    {
        var deadline = DateTime.UtcNow.AddSeconds(5);
        while (true)
        {
            if (requestLogs.AtLeast(LogEventLevel.Verbose) is [var requestLog, ..])
                return requestLog;
            if (DateTime.UtcNow > deadline)
                throw new TimeoutException("No request log for /health.");
            await Task.Delay(20);
        }
    }
}
