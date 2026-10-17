namespace FullstackTemplate.UnitTests.Resources;

using System.Diagnostics;
using FullstackTemplate.Server.Domain.Tenants.Features;
using FullstackTemplate.Server.Exceptions;
using FullstackTemplate.Server.Resources;
using Shouldly;

public class RequestTracingBehaviorTests : IDisposable
{
    private readonly ActivitySource _testSource = new("FullstackTemplate.UnitTests.RequestTracing");
    private readonly List<Activity> _stopped = [];
    private readonly ActivityListener _listener;

    public RequestTracingBehaviorTests()
    {
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
        await behavior.Handle(new GetTenant.Query(Guid.NewGuid()), _ => Task.FromResult("ok"), CancellationToken.None);

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
            new GetTenant.Query(Guid.NewGuid()),
            _ => throw new ValidationException("Please provide a tenant name."),
            CancellationToken.None));

        // Assert
        var span = SingleSpan(test, "GetTenant.Query");
        span.GetTagItem("error.type").ShouldBe(typeof(ValidationException).FullName);
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
            new GetTenant.Query(Guid.NewGuid()),
            _ => throw new InvalidOperationException("boom"),
            CancellationToken.None));

        // Assert
        var span = SingleSpan(test, "GetTenant.Query");
        span.GetTagItem("error.type").ShouldBe(typeof(InvalidOperationException).FullName);
        span.Status.ShouldBe(ActivityStatusCode.Error);
        span.StatusDescription.ShouldBe("boom");
    }

    // The listener is process-wide and test classes run in parallel, so match on the test's own trace.
    private Activity SingleSpan(Activity test, string name)
    {
        lock (_stopped)
            return _stopped.Where(a => a.TraceId == test.TraceId && a.DisplayName == name)
                .ShouldHaveSingleItem();
    }
}
