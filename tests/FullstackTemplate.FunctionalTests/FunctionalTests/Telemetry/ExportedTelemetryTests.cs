namespace FullstackTemplate.FunctionalTests.FunctionalTests.Telemetry;

using Microsoft.Extensions.DependencyInjection;
using OpenTelemetry.Metrics;
using Shouldly;
using ServerTelemetry = FullstackTemplate.Server.Resources.Telemetry;

// The exporters add on the thread that ends a span or flushes the metrics, so each test reads while another
// thread exports
[NotInParallel]
public class ExportedTelemetryTests : TestBase
{
    [Test]
    public async Task a_span_read_is_whole_while_the_server_exports_spans()
    {
        // Arrange
        const int writers = 4;
        const int spansPerWriter = 1_000;
        var before = AllExportedSpans().Count;
        using var start = new Barrier(writers + 1);
        var exports = Enumerable.Range(0, writers).Select(_ => Task.Run(() =>
        {
            start.SignalAndWait();
            for (var i = 0; i < spansPerWriter; i++)
                ServerTelemetry.Source.StartActivity("exported_telemetry.probe")?.Dispose();
        })).ToArray();

        // Act
        start.SignalAndWait();
        while (!exports.All(t => t.IsCompleted))
        {
            AllExportedSpans().ShouldAllBe(span => span != null);
            ExportedSpans().ShouldAllBe(span => span != null);
        }
        await Task.WhenAll(exports);

        // Assert
        AllExportedSpans().Count(span => span.DisplayName == "exported_telemetry.probe")
            .ShouldBeGreaterThanOrEqualTo(writers * spansPerWriter);
        AllExportedSpans().Count.ShouldBeGreaterThanOrEqualTo(before + writers * spansPerWriter);
    }

    [Test]
    public async Task a_metric_read_is_whole_while_the_server_exports_metrics()
    {
        // Arrange
        var factory = TestingWebApplicationFactory.Instance;
        var meterProvider = factory.Services.GetRequiredService<MeterProvider>();
        var before = factory.ExportedMetrics.Count;
        using var start = new Barrier(2);
        var flushes = Task.Run(() =>
        {
            start.SignalAndWait();
            for (var i = 0; i < 100; i++)
                meterProvider.ForceFlush().ShouldBeTrue();
        });

        // Act
        start.SignalAndWait();
        while (!flushes.IsCompleted)
        {
            factory.ExportedMetrics.Snapshot().ShouldAllBe(metric => metric != null);
        }
        await flushes;

        // Assert
        factory.ExportedMetrics.Count.ShouldBeGreaterThan(before);
    }
}
