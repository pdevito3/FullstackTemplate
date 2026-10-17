namespace FullstackTemplate.FunctionalTests.FunctionalTests.Telemetry;

using FullstackTemplate.Server.Domain.Tenants.Features;
using FullstackTemplate.SharedTestHelpers.EntityGraphs;
using Microsoft.Extensions.DependencyInjection;
using OpenTelemetry.Metrics;
using Shouldly;

public class MetricsTests : TestBase
{
    [Fact]
    public async Task feature_slice_duration_and_framework_meters_are_exported()
    {
        // Arrange
        TestGraph.Start(out _).AddTenant(out var tenant);
        await AddAsync(tenant);
        await SendAsync(new GetTenant.Query(tenant.Id));

        // Act
        var factory = TestingWebApplicationFactory.Instance;
        factory.Services.GetRequiredService<MeterProvider>().ForceFlush().ShouldBeTrue();

        // Assert
        List<MetricSnapshot> exported;
        lock (factory.ExportedMetrics)
            exported = [.. factory.ExportedMetrics];

        exported.ShouldContain(m => m.Name == "app.request.duration" && m.MeterName == "FullstackTemplate.Server");
        exported.ShouldContain(m => m.MeterName == "Microsoft.EntityFrameworkCore");
    }
}
