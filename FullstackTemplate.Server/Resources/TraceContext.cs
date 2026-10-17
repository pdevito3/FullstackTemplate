namespace FullstackTemplate.Server.Resources;

/// <summary>
/// A W3C trace context in serializable form, so a trace can survive a hop that the OpenTelemetry
/// propagator cannot cover: a queued payload, a persisted row, or work a client ran out of band and
/// uploads later. On a live HTTP call the <c>traceparent</c> header already does this job, and the
/// ASP.NET Core instrumentation reads it - use this record only when the context must be carried in
/// data rather than in headers.
/// </summary>
/// <param name="TraceParent">The W3C <c>traceparent</c> value, or null when no activity was current.</param>
/// <param name="TraceState">The W3C <c>tracestate</c> value, if the producer had one.</param>
public sealed record TraceContext(string? TraceParent, string? TraceState);
