namespace FullstackTemplate.UnitTests.Resources;

using System.Diagnostics;
using FullstackTemplate.Server.Resources;
using Shouldly;

public class TelemetryTests : IDisposable
{
    private readonly ActivitySource _callerSource = new("FullstackTemplate.UnitTests.Caller");
    private readonly ActivityListener _listener;

    public TelemetryTests()
    {
        // Without a listener, StartActivity returns null and every assertion below is vacuous.
        _listener = new ActivityListener
        {
            ShouldListenTo = source => source.Name is Telemetry.ServiceName or "FullstackTemplate.UnitTests.Caller",
            Sample = (ref ActivityCreationOptions<ActivityContext> _) => ActivitySamplingResult.AllDataAndRecorded,
        };
        ActivitySource.AddActivityListener(_listener);
    }

    public void Dispose()
    {
        _listener.Dispose();
        _callerSource.Dispose();
        GC.SuppressFinalize(this);
    }

    [Fact]
    public void trace_root_starts_a_new_trace_linked_to_the_caller()
    {
        // Arrange
        using var caller = _callerSource.StartActivity("caller").ShouldNotBeNull();

        // Act
        using (var scope = Telemetry.TraceRoot([("app.test", "value")], "work.run"))
        {
            // Assert
            var root = scope.Activity.ShouldNotBeNull();
            root.DisplayName.ShouldBe("work.run");
            root.Parent.ShouldBeNull();
            root.TraceId.ShouldNotBe(caller.TraceId);
            root.Links.ShouldHaveSingleItem().Context.SpanId.ShouldBe(caller.SpanId);
            root.GetTagItem("app.test").ShouldBe("value");
            root.GetTagItem("code.function").ShouldBe(nameof(trace_root_starts_a_new_trace_linked_to_the_caller));
            (root.GetTagItem("code.file.path") as string).ShouldNotBeNull().ShouldEndWith("TelemetryTests.cs");
            Activity.Current.ShouldBe(root);
        }

        Activity.Current.ShouldBe(caller);
    }

    [Fact]
    public void trace_root_without_a_caller_has_no_links()
    {
        // Arrange
        Activity.Current = null;

        // Act
        using var scope = Telemetry.TraceRoot([], "work.run");

        // Assert
        var root = scope.Activity.ShouldNotBeNull();
        root.Parent.ShouldBeNull();
        root.Links.ShouldBeEmpty();
    }

    [Fact]
    public void trace_root_adds_additional_links()
    {
        // Arrange
        var clientContext = new ActivityContext(
            ActivityTraceId.CreateRandom(), ActivitySpanId.CreateRandom(), ActivityTraceFlags.Recorded);

        // Act
        using var scope = Telemetry.TraceRoot([], "work.run", [new ActivityLink(clientContext)]);

        // Assert
        scope.Activity.ShouldNotBeNull().Links
            .ShouldContain(link => link.Context.SpanId == clientContext.SpanId);
    }

    [Fact]
    public void trace_context_round_trips_the_current_activity()
    {
        // Arrange
        using var caller = _callerSource.StartActivity("caller").ShouldNotBeNull();
        caller.TraceStateString = "vendor=value";

        // Act
        var captured = Telemetry.CaptureCurrentTraceContext();
        var parsed = Telemetry.TryParseTraceContext(captured, out var context);

        // Assert
        parsed.ShouldBeTrue();
        context.TraceId.ShouldBe(caller.TraceId);
        context.SpanId.ShouldBe(caller.SpanId);
        context.TraceState.ShouldBe("vendor=value");
        context.IsRemote.ShouldBeTrue();
    }

    [Fact]
    public void trace_context_capture_without_an_activity_has_no_trace_parent()
    {
        // Arrange
        Activity.Current = null;

        // Act
        var captured = Telemetry.CaptureCurrentTraceContext();

        // Assert
        captured.TraceParent.ShouldBeNull();
        Telemetry.TryParseTraceContext(captured, out _).ShouldBeFalse();
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("not-a-traceparent")]
    public void trace_context_parse_rejects_a_missing_or_malformed_trace_parent(string? traceParent)
    {
        // Act
        var parsed = Telemetry.TryParseTraceContext(new TraceContext(traceParent, null), out var context);

        // Assert
        parsed.ShouldBeFalse();
        context.ShouldBe(default);
    }

    [Fact]
    public void trace_context_parse_rejects_a_null_context()
    {
        Telemetry.TryParseTraceContext(null, out _).ShouldBeFalse();
    }
}
