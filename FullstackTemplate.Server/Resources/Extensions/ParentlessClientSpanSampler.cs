namespace FullstackTemplate.Server.Resources.Extensions;

using System.Diagnostics;
using OpenTelemetry.Trace;

/// <summary>
/// The sampler of a span with no parent. It drops a Client span, because no request, job, or <c>TraceRoot</c> owns it,
/// so it would export as an orphan trace. Examples are each poll of a BackWave pump and each query on the connections
/// of the BackWave store, which have no command filter. It keeps each other span with no parent.
/// </summary>
/// <remarks>
/// Use it as the root sampler of a <see cref="ParentBasedSampler"/>, so a span with a parent follows its parent.
/// </remarks>
internal sealed class ParentlessClientSpanSampler : Sampler
{
    public override SamplingResult ShouldSample(in SamplingParameters samplingParameters)
        => new(samplingParameters.Kind == ActivityKind.Client ? SamplingDecision.Drop : SamplingDecision.RecordAndSample);
}
