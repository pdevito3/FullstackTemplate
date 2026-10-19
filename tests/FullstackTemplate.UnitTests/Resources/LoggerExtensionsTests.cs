namespace FullstackTemplate.UnitTests.Resources;

using FullstackTemplate.Server.Resources;
using Serilog;
using Serilog.Core;
using Serilog.Events;
using Shouldly;

public class LoggerExtensionsTests
{
    [Test]
    public void here_adds_the_call_site_to_the_log_event()
    {
        // Arrange
        var sink = new CollectingSink();
        using var logger = new LoggerConfiguration().WriteTo.Sink(sink).CreateLogger();

        // Act
        logger.Here().Information("Created tenant {TenantId}", 42);

        // Assert
        var logEvent = sink.Events.ShouldHaveSingleItem();
        var memberName = nameof(here_adds_the_call_site_to_the_log_event);
        Scalar(logEvent, "CallSite").ShouldBeOfType<string>()
            .ShouldStartWith($"LoggerExtensionsTests.cs:{memberName}@");
        Scalar(logEvent, "code.function").ShouldBe(memberName);
        Scalar(logEvent, "code.file.path").ShouldBeOfType<string>().ShouldEndWith("LoggerExtensionsTests.cs");
        Scalar(logEvent, "code.line.number").ShouldBeOfType<int>().ShouldBeGreaterThan(0);
        Scalar(logEvent, "TenantId").ShouldBe(42);
    }

    [Test]
    public void a_log_event_without_here_has_no_call_site()
    {
        // Arrange
        var sink = new CollectingSink();
        using var logger = new LoggerConfiguration().WriteTo.Sink(sink).CreateLogger();

        // Act
        logger.Information("No call site");

        // Assert
        sink.Events.ShouldHaveSingleItem().Properties.ShouldNotContainKey("CallSite");
    }

    private static object? Scalar(LogEvent logEvent, string name) =>
        logEvent.Properties[name].ShouldBeOfType<ScalarValue>().Value;

    private sealed class CollectingSink : ILogEventSink
    {
        public List<LogEvent> Events { get; } = [];

        public void Emit(LogEvent logEvent) => Events.Add(logEvent);
    }
}
