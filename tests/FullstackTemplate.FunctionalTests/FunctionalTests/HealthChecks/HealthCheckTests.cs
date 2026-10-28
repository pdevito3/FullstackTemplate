namespace FullstackTemplate.FunctionalTests.FunctionalTests.HealthChecks;

using System.Globalization;
using System.Net;
using Serilog.Events;
using Shouldly;
using TestUtilities;

[NotInParallel(nameof(StallingHealthCheck))]
public class HealthCheckTests : TestBase
{
    [Test]
    public async Task health_check_returns_ok()
    {
        // Act
        var result = await FactoryClient.GetRequestAsync(ApiRoutes.Health);

        // Assert
        result.StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    [Test]
    public async Task alive_check_returns_ok()
    {
        // Act
        var result = await FactoryClient.GetRequestAsync(ApiRoutes.Alive);

        // Assert
        result.StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    [Test]
    public async Task health_check_that_the_caller_leaves_logs_no_error()
    {
        // Arrange
        var factory = TestingWebApplicationFactory.Instance;
        var errorsBefore = HealthErrors(factory).Count;
        var started = factory.StallingHealthCheck.ArmNext();
        using var leave = new CancellationTokenSource();

        // Act
        var probe = FactoryClient.GetAsync(ApiRoutes.Health, leave.Token);
        await started.WaitAsync(TimeSpan.FromSeconds(30));
        await leave.CancelAsync();
        await Should.ThrowAsync<OperationCanceledException>(probe);

        // Assert
        await WaitForRequestLogsAsync(() => HealthErrors(factory).Count > errorsBefore);
        HealthErrors(factory).Skip(errorsBefore).Select(e => e.RenderMessage(CultureInfo.InvariantCulture)).ShouldBeEmpty();
    }

    // The server ends the request after the client leaves, so its logs come a little later
    private static async Task WaitForRequestLogsAsync(Func<bool> arrived)
    {
        var deadline = DateTime.UtcNow.AddSeconds(2);
        while (!arrived() && DateTime.UtcNow < deadline)
            await Task.Delay(50);
    }

    private static List<LogEvent> HealthErrors(TestingWebApplicationFactory factory) => factory.CapturedLogs
        .ForRequestPath($"/{ApiRoutes.Health}")
        .Where(e => e.Level >= LogEventLevel.Error)
        .ToList();
}
