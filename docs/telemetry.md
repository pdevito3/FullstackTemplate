# Telemetry

The server and the BFF export traces, metrics, and logs with OpenTelemetry. In development, the Aspire dashboard shows them at https://localhost:17199 (the `aspire run` output gives the login link).

## Privacy rule

A span carries names, identifiers, and counts. It never carries user-entered content or personal data, such as names or email addresses. Every helper below takes its tags from the caller, so each call site must obey this rule.

## What a trace looks like

A request through the BFF gives one trace with this shape:

```
BFF   GET /api/v1/users
└─ Server   GET api/v{version}/users
   └─ GetUserList.Query                (RequestTracingBehavior)
      └─ SQL: users.list               (Npgsql span, tagged query)
```

## Where the code is

| File | Purpose |
|------|---------|
| `FullstackTemplate.Server/Resources/Telemetry.cs` | The `ActivitySource`, the `Meter`, the shared metrics, `TraceRoot`, and the `TraceContext` helpers |
| `FullstackTemplate.Server/Resources/RequestTracingBehavior.cs` | MediatR behavior. Gives each feature slice its own span |
| `FullstackTemplate.Server/Databases/QueryTagging.cs` | `TagWithOperationCallSite`, which names an EF query |
| `FullstackTemplate.Server/Databases/QueryTagEnrichment.cs` | Copies the query tags into the Npgsql span |
| `FullstackTemplate.Server/Resources/Extensions/HostExtensions.cs` | OpenTelemetry and Serilog registration |
| `FullstackTemplate.Bff/Telemetry.cs`, `FullstackTemplate.Bff/LoginTraceLink.cs` | BFF telemetry and the login trace link |

## Custom spans and metrics

```csharp
using var activity = Telemetry.Source.StartActivity("operation-name");
activity?.SetTag("app.tenant_id", tenantId);

Telemetry.Requests.Add(1, new("endpoint", "/api/weather"));
Telemetry.RequestDuration.Record(elapsed.TotalMilliseconds, new("endpoint", "/api/weather"));

Telemetry.RecordError(activity, exception);
```

The shared metrics are:

- `app.requests` - a counter of processed requests.
- `app.errors` - a counter of errors. `RecordError` increments it.
- `app.request.duration` - a histogram of request duration, in ms.

## Feature slice spans

`RequestTracingBehavior` wraps each MediatR request in a span. The span name is the feature class plus the request type, for example `GetUser.Query`. You do not have to add code to a new slice to get this span.

The behavior tags an exception type as `error.type`. A client error does not mark the span as failed. A client error is an exception that `DomainProblemDetails` maps to a 4xx response (`ValidationException`, `FluentValidation.ValidationException`, and `NotFoundException`). All other exceptions mark the span with the `Error` status.

## Named queries

Tag each EF query in a feature slice with `TagWithOperationCallSite`:

```csharp
var tenant = await dbContext.Tenants
    .TagWithOperationCallSite("tenants.get_by_id")
    .GetById(request.Id, cancellationToken);
```

The Npgsql span then has these properties:

- The name is `SQL: tenants.get_by_id`.
- `db.operation.name` is the operation name.
- `code.function`, `code.file.path`, and `code.line.number` give the C# call site.

Use a stable dotted name in the form `area.operation`. Never put an id or other data in the name. An untagged query keeps the default Npgsql span name.

## Queries with no parent span

The Npgsql command filter exports a query span only when a recorded activity is current. This rule drops two types of orphan traces:

- Queries that run with no activity at all.
- Queries under a request that the ASP.NET Core filter dropped, such as `/health`.

As a result, background work that does queries must start its own trace with `TraceRoot`.

## TraceRoot

`Telemetry.TraceRoot` starts a span that is the root of a new trace. The span links back to the activity that was current before it. Use it for work that has no request, or for work that is much larger than the request that started it.

```csharp
using var trace = Telemetry.TraceRoot(
    [("app.db_context", typeof(TDbContext).Name)],
    "database.migrate");
```

`MigrationHostedService` uses this helper, so a startup migration is one `database.migrate` trace. The scope puts the previous activity back when you dispose it. The span gets the same `code.*` tags as a tagged query.

## TraceContext

`TraceContext` carries a trace context in data, for a hop where no `traceparent` header is available. Examples are a message payload or a redirect.

```csharp
var captured = Telemetry.CaptureCurrentTraceContext();
// ... later, on the other side of the hop
ActivityLink[] links = Telemetry.TryParseTraceContext(captured, out var parent) ? [new(parent)] : [];
using var trace = Telemetry.TraceRoot([], "work.run", links);
```

`TryParseTraceContext` returns false for a null, empty, or malformed context. The receiver then continues with no link.

## Login trace link

A login is two requests with a browser redirect through the identity provider between them. `LoginTraceLink` stores the trace context of `/bff/login` in the OIDC authentication properties. These properties travel in the data-protected `state` parameter. The `/signin-oidc` callback reads the context and adds a span link to the login trace.

The BFF names the OIDC callback spans `GET /signin-oidc`, `GET /signout-callback-oidc`, and `GET /signout-oidc`. No route handles these paths, so without this rule the span name is only `GET`.

## Logs

Serilog is the logging provider. The OTLP sink sends logs to the Aspire dashboard when `OTEL_EXPORTER_OTLP_ENDPOINT` is set. The sink must stay inside the `AddSerilog` callback in `HostExtensions.cs`. The host replaces a logger that you assign to `Log.Logger` in a different location, so its sinks never run.

## Tests

- Unit tests use an `ActivityListener` and filter spans by the trace id of the test. Test classes run in parallel, so the listener sees spans from other tests. See `tests/FullstackTemplate.UnitTests/Resources/TelemetryTests.cs`.
- Functional tests use the OpenTelemetry in-memory exporter. `TestBase.ExportedSpans()` returns the spans that the current test exported. `TestBase.AllExportedSpans()` also returns startup spans, such as `database.migrate`. See `tests/FullstackTemplate.FunctionalTests/FunctionalTests/Telemetry/TracingTests.cs`.
