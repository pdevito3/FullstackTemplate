namespace FullstackTemplate.UnitTests.Resources;

using System.Diagnostics;
using FullstackTemplate.Server.Resources.Extensions;
using OpenTelemetry.Trace;
using Shouldly;

public class ParentlessClientSpanSamplerTests
{
    // The sampler of the host: a span with a parent follows its parent.
    private static readonly Sampler HostSampler = new ParentBasedSampler(new ParentlessClientSpanSampler());

    private static readonly ActivityContext SampledParent = new(ActivityTraceId.CreateRandom(),
        ActivitySpanId.CreateRandom(), ActivityTraceFlags.Recorded, isRemote: false);

    [Test]
    public void client_span_with_no_parent_is_dropped()
    {
        // Arrange
        var span = new SamplingParameters(default, ActivityTraceId.CreateRandom(), "backwave.receive", ActivityKind.Client);

        // Act
        var result = HostSampler.ShouldSample(span);

        // Assert
        result.Decision.ShouldBe(SamplingDecision.Drop);
    }

    [Test]
    [Arguments(ActivityKind.Server)]
    [Arguments(ActivityKind.Consumer)]
    [Arguments(ActivityKind.Internal)]
    [Arguments(ActivityKind.Producer)]
    public void other_span_with_no_parent_is_kept(ActivityKind kind)
    {
        // Arrange
        var span = new SamplingParameters(default, ActivityTraceId.CreateRandom(), "root", kind);

        // Act
        var result = HostSampler.ShouldSample(span);

        // Assert
        result.Decision.ShouldBe(SamplingDecision.RecordAndSample);
    }

    [Test]
    public void client_span_with_a_sampled_parent_is_kept()
    {
        // Arrange
        var span = new SamplingParameters(SampledParent, SampledParent.TraceId, "SELECT", ActivityKind.Client);

        // Act
        var result = HostSampler.ShouldSample(span);

        // Assert
        result.Decision.ShouldBe(SamplingDecision.RecordAndSample);
    }
}
