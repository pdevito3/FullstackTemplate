using System.Diagnostics;
using Microsoft.AspNetCore.Authentication;

namespace FullstackTemplate.Bff;

/// <summary>
/// Links the <c>/signin-oidc</c> callback trace back to the <c>/bff/login</c> trace that started it.
/// <para>
/// A login is two requests with a browser redirect through the identity provider in between, so no
/// <c>traceparent</c> header connects them and they export as two unrelated traces. The login request
/// stores its trace context in the authentication properties. Those properties travel in the OIDC
/// <c>state</c> parameter, which the BFF data-protects, so the identity provider cannot read or change
/// them. The callback reads the context back and adds a span link to the original trace.
/// </para>
/// </summary>
public static class LoginTraceLink
{
    private const string TraceParentKey = "app.trace.parent";
    private const string TraceStateKey = "app.trace.state";

    /// <summary>
    /// Stores the current trace context in <paramref name="properties"/>. Call this before the
    /// redirect to the identity provider.
    /// </summary>
    public static void Capture(AuthenticationProperties properties)
    {
        var traceContext = Telemetry.CaptureCurrentTraceContext();
        if (traceContext.TraceParent is null)
            return;

        properties.Items[TraceParentKey] = traceContext.TraceParent;
        properties.Items[TraceStateKey] = traceContext.TraceState;
    }

    /// <summary>
    /// Links the current activity to the trace context that <see cref="Capture"/> stored. Does nothing
    /// when the properties carry no context, so a login that started untraced still completes.
    /// </summary>
    public static void LinkCurrentActivity(AuthenticationProperties? properties)
    {
        if (properties is null || Activity.Current is not { } current)
            return;

        properties.Items.TryGetValue(TraceParentKey, out var traceParent);
        properties.Items.TryGetValue(TraceStateKey, out var traceState);

        if (Telemetry.TryParseTraceContext(new TraceContext(traceParent, traceState), out var loginContext))
            current.AddLink(new ActivityLink(loginContext));
    }
}
