namespace FullstackTemplate.UnitTests.Resources;

using FullstackTemplate.Server.Domain;
using FullstackTemplate.Server.Domain.Tenants;
using System.Diagnostics;
using System.Diagnostics.Metrics;
using FullstackTemplate.Server.Domain.Tenants.Features;
using FullstackTemplate.Server.Exceptions;
using FullstackTemplate.Server.Resources;
using MediatR;
using Shouldly;

public class RequestTracingBehaviorTests : IDisposable
{
    private readonly ActivitySource _testSource = new("FullstackTemplate.UnitTests.RequestTracing");
    private readonly List<Activity> _stopped = [];
    private readonly ActivityListener _listener;
    private readonly MeterListener _meterListener = new();
    private readonly List<Measurement> _measurements = [];

    public RequestTracingBehaviorTests()
    {
        _meterListener.InstrumentPublished = (instrument, listener) =>
        {
            if (instrument.Meter.Name == Telemetry.ServiceName)
                listener.EnableMeasurementEvents(instrument);
        };
        _meterListener.SetMeasurementEventCallback<double>((instrument, value, tags, _) => Record(instrument, value, tags));
        _meterListener.SetMeasurementEventCallback<long>((instrument, value, tags, _) => Record(instrument, value, tags));
        _meterListener.Start();

        _listener = new ActivityListener
        {
            ShouldListenTo = source => source.Name is Telemetry.ServiceName or "FullstackTemplate.UnitTests.RequestTracing",
            Sample = (ref ActivityCreationOptions<ActivityContext> _) => ActivitySamplingResult.AllDataAndRecorded,
            ActivityStopped = activity =>
            {
                lock (_stopped)
                    _stopped.Add(activity);
            },
        };
        ActivitySource.AddActivityListener(_listener);
    }

    public void Dispose()
    {
        _listener.Dispose();
        _meterListener.Dispose();
        _testSource.Dispose();
        GC.SuppressFinalize(this);
    }

    [Fact]
    public async Task span_is_named_after_the_feature_slice()
    {
        // Arrange
        using var test = _testSource.StartActivity("test").ShouldNotBeNull();
        var behavior = new RequestTracingBehavior<GetTenant.Query, string>();

        // Act
        await behavior.Handle(new GetTenant.Query(EntityId.New(Tenant.IdPrefix)), _ => Task.FromResult("ok"), CancellationToken.None);

        // Assert
        var span = SingleSpan(test, "GetTenant.Query");
        span.GetTagItem("app.request.name").ShouldBe("GetTenant.Query");
        span.Status.ShouldBe(ActivityStatusCode.Unset);
    }

    [Fact]
    public async Task validation_error_is_tagged_but_not_marked_failed()
    {
        // Arrange
        using var test = _testSource.StartActivity("test").ShouldNotBeNull();
        var behavior = new RequestTracingBehavior<GetTenant.Query, string>();

        // Act
        await Should.ThrowAsync<ValidationException>(() => behavior.Handle(
            new GetTenant.Query(EntityId.New(Tenant.IdPrefix)),
            _ => throw new ValidationException("Please provide a tenant name."),
            CancellationToken.None));

        // Assert
        var span = SingleSpan(test, "GetTenant.Query");
        span.GetTagItem("error.type").ShouldBe(typeof(ValidationException).FullName);
        span.Status.ShouldBe(ActivityStatusCode.Unset);
    }

    [Fact]
    public async Task not_found_error_is_tagged_but_not_marked_failed()
    {
        // Arrange
        using var test = _testSource.StartActivity("test").ShouldNotBeNull();
        var behavior = new RequestTracingBehavior<GetTenant.Query, string>();

        // Act
        await Should.ThrowAsync<NotFoundException>(() => behavior.Handle(
            new GetTenant.Query(EntityId.New(Tenant.IdPrefix)),
            _ => throw new NotFoundException("Tenant was not found."),
            CancellationToken.None));

        // Assert
        var span = SingleSpan(test, "GetTenant.Query");
        span.GetTagItem("error.type").ShouldBe(typeof(NotFoundException).FullName);
        span.Status.ShouldBe(ActivityStatusCode.Unset);
    }

    [Fact]
    public async Task unexpected_error_marks_the_span_failed()
    {
        // Arrange
        using var test = _testSource.StartActivity("test").ShouldNotBeNull();
        var behavior = new RequestTracingBehavior<GetTenant.Query, string>();

        // Act
        await Should.ThrowAsync<InvalidOperationException>(() => behavior.Handle(
            new GetTenant.Query(EntityId.New(Tenant.IdPrefix)),
            _ => throw new InvalidOperationException("boom"),
            CancellationToken.None));

        // Assert
        var span = SingleSpan(test, "GetTenant.Query");
        span.GetTagItem("error.type").ShouldBe(typeof(InvalidOperationException).FullName);
        span.Status.ShouldBe(ActivityStatusCode.Error);
        span.StatusDescription.ShouldBe("boom");
    }

    [Fact]
    public async Task duration_is_recorded_with_the_request_name()
    {
        // Arrange
        var behavior = new RequestTracingBehavior<MeteredFeature.Query, string>();

        // Act
        await behavior.Handle(new MeteredFeature.Query(), _ => Task.FromResult("ok"), CancellationToken.None);

        // Assert
        var duration = Measurements("app.request.duration", "app.request.name", "MeteredFeature.Query")
            .ShouldHaveSingleItem();
        duration.Value.ShouldBeGreaterThanOrEqualTo(0);
        duration.Tags.ShouldNotContainKey("error.type");
    }

    [Fact]
    public async Task client_error_duration_has_the_error_type_but_counts_no_error()
    {
        // Arrange
        var behavior = new RequestTracingBehavior<MeteredFeature.Query, string>();

        // Act
        await Should.ThrowAsync<MeteredClientFailure>(() => behavior.Handle(
            new MeteredFeature.Query(),
            _ => throw new MeteredClientFailure(),
            CancellationToken.None));

        // Assert
        Measurements("app.request.duration", "app.request.name", "MeteredFeature.Query")
            .ShouldHaveSingleItem().Tags["error.type"].ShouldBe(typeof(MeteredClientFailure).FullName);
        Measurements("app.errors", "exception.type", nameof(MeteredClientFailure)).ShouldBeEmpty();
    }

    [Fact]
    public async Task unexpected_error_is_counted()
    {
        // Arrange
        var behavior = new RequestTracingBehavior<MeteredFeature.Query, string>();

        // Act
        await Should.ThrowAsync<MeteredServerFailure>(() => behavior.Handle(
            new MeteredFeature.Query(),
            _ => throw new MeteredServerFailure(),
            CancellationToken.None));

        // Assert
        Measurements("app.errors", "exception.type", nameof(MeteredServerFailure))
            .ShouldHaveSingleItem().Value.ShouldBe(1);
        Measurements("app.request.duration", "app.request.name", "MeteredFeature.Query")
            .ShouldHaveSingleItem().Tags["error.type"].ShouldBe(typeof(MeteredServerFailure).FullName);
    }

    // The listener is process-wide and test classes run in parallel, so match on the test's own trace.
    private Activity SingleSpan(Activity test, string name)
    {
        lock (_stopped)
            return _stopped.Where(a => a.TraceId == test.TraceId && a.DisplayName == name)
                .ShouldHaveSingleItem();
    }

    // The meter is process-wide too. The test-only request and exception types keep these measurements apart.
    private List<Measurement> Measurements(string instrument, string tagKey, string tagValue)
    {
        lock (_measurements)
            return _measurements
                .Where(m => m.Instrument == instrument && Equals(m.Tags.GetValueOrDefault(tagKey), tagValue))
                .ToList();
    }

    private void Record(Instrument instrument, double value, ReadOnlySpan<KeyValuePair<string, object?>> tags)
    {
        var measurement = new Measurement(instrument.Name, value, tags.ToArray().ToDictionary(t => t.Key, t => t.Value));
        lock (_measurements)
            _measurements.Add(measurement);
    }

    private sealed record Measurement(string Instrument, double Value, Dictionary<string, object?> Tags);

    private static class MeteredFeature
    {
        public sealed record Query : IRequest<string>;
    }

    private sealed class MeteredClientFailure() : ValidationException("Please provide a value.");

    private sealed class MeteredServerFailure() : Exception("boom");
}
