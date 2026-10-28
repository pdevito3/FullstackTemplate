namespace FullstackTemplate.UnitTests.Resources;

using FullstackTemplate.Server.Resources.Extensions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Shouldly;

public class CallerCancellationHealthCheckTests
{
    [Test]
    public async Task check_that_runs_past_its_own_timeout_is_still_unhealthy()
    {
        // Arrange
        var services = new ServiceCollection().AddLogging();
        services.AddHealthChecks().AddAsyncCheck("slow", async cancellationToken =>
        {
            // Like the EF Core DbContext check, it reports each exception as Unhealthy
            try
            {
                await Task.Delay(Timeout.Infinite, cancellationToken);
                return HealthCheckResult.Healthy();
            }
            catch (Exception exception)
            {
                return HealthCheckResult.Unhealthy(exception.Message, exception);
            }
        }, timeout: TimeSpan.FromMilliseconds(50));
        services.PostConfigure<HealthCheckServiceOptions>(CallerCancellationHealthCheck.WrapEachCheck);
        await using var provider = services.BuildServiceProvider();

        // Act
        var report = await provider.GetRequiredService<HealthCheckService>().CheckHealthAsync();

        // Assert
        report.Entries["slow"].Status.ShouldBe(HealthStatus.Unhealthy);
        report.Entries["slow"].Description.ShouldBe("A timeout occurred while running check.");
    }
}
