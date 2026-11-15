namespace FullstackTemplate.FunctionalTests.FunctionalTests.Telemetry;

using System.Diagnostics;
using FullstackTemplate.Server.Domain.Tenants.Features;
using FullstackTemplate.Server.Resources;
using FullstackTemplate.SharedTestHelpers.EntityGraphs;
using Microsoft.Extensions.DependencyInjection;
using OpenTelemetry.Metrics;
using Shouldly;

public class MetricsTests : TestBase
{
    [Test]
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
        var exported = factory.ExportedMetrics.Snapshot();

        var duration = exported.Last(m => m.Name == "app.request.duration" && m.MeterName == "FullstackTemplate.Server");
        exported.ShouldContain(m => m.MeterName == "Microsoft.EntityFrameworkCore");

        // The SDK default buckets are for milliseconds: 0, 5, 10, 25, and up. In seconds, they put each request in one bucket.
        var bounds = new List<double>();
        foreach (var bucket in duration.MetricPoints[0].GetHistogramBuckets())
            bounds.Add(bucket.ExplicitBound);
        bounds.ShouldBe([0.005, 0.01, 0.025, 0.05, 0.075, 0.1, 0.25, 0.5, 0.75, 1, 2.5, 5, 7.5, 10, double.PositiveInfinity]);
    }

    // A histogram keeps one exemplar for each bucket, so a request of another test in the same bucket can replace ours.
    [Test, NotInParallel]
    public async Task feature_slice_duration_has_an_exemplar_that_leads_to_the_trace_of_the_request()
    {
        // Arrange
        TestGraph.Start(out _).AddTenant(out var tenant);
        await AddAsync(tenant);
        var factory = TestingWebApplicationFactory.Instance;
        var meterProvider = factory.Services.GetRequiredService<MeterProvider>();
        meterProvider.ForceFlush().ShouldBeTrue();
        var firstMetric = factory.ExportedMetrics.Count;

        // Act
        using var parent = Telemetry.Source.StartActivity("test.parent").ShouldNotBeNull();
        await SendAsync(new GetTenant.Query(tenant.Id));
        meterProvider.ForceFlush().ShouldBeTrue();

        // Assert
        var featureSpan = ExportedSpans().Single(a => a.TraceId == parent.TraceId && a.DisplayName == "GetTenant.Query");
        var exemplarSpans = new List<(ActivityTraceId TraceId, ActivitySpanId SpanId)>();
        foreach (var metric in factory.ExportedMetrics.Snapshot().Skip(firstMetric).Where(m => m.Name == "app.request.duration"))
        {
            foreach (var point in metric.MetricPoints)
            {
                if (!point.TryGetExemplars(out var exemplars))
                    continue;
                foreach (var exemplar in exemplars)
                    exemplarSpans.Add((exemplar.TraceId, exemplar.SpanId));
            }
        }

        exemplarSpans.ShouldContain((featureSpan.TraceId, featureSpan.SpanId));
    }
}
