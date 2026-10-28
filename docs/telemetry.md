# Telemetry

The server and the BFF export traces, metrics, and logs with OpenTelemetry. In development, the Aspire dashboard shows them at https://localhost:17199 (the `aspire run` output gives the login link). An isolated run (`--isolated`) uses a random port. `dotnet aspire ps` gives its URL.

## Privacy rule

A span carries names, identifiers, and counts. It never carries user-entered content or personal data, such as names or email addresses. Every helper below takes its tags from the caller, so each call site must obey this rule.

Each Npgsql span copies the SQL text into `db.query.text`. Thus a value from the caller must go to the database as a parameter, never as a literal in the SQL text. `CustomQueryKitConfiguration` sets `ParameterizeFilterValues`, so the SQL text of a list query has a parameter name in place of each filter value.

The request spans of the server and the BFF keep each query key in `url.query` and replace each value with `Redacted`, for example `?filters=Redacted&pageSize=Redacted`. The ASP.NET Core instrumentation does this by default, but Aspire turns it off in development. Thus `HostExtensions.cs` and the BFF `Extensions.cs` set `url.query` again.

## What a trace looks like

A request through the BFF gives one trace with this shape:

```
BFF   GET /api/v1/users
└─ Server   GET api/v{version}/users
   └─ GetUserList.Query                (RequestTracingBehavior)
      ├─ SQL: users.list.count         (Npgsql span, the count of the paged list)
      └─ SQL: users.list               (Npgsql span, tagged query)
```

A command whose save runs a domain event handler has one more level. The handler span is a child of the feature slice span:

```
Server   PUT api/v{version}/tenants/{id}
└─ UpdateTenant.Command                (RequestTracingBehavior)
   ├─ SQL: tenants.update.load
   ├─ domain_event.handle              (one span for each handler)
   │  └─ SQL: <the queries of the handler>
   └─ SQL: <the save>
```

## Where the code is

| File | Purpose |
|------|---------|
| `FullstackTemplate.Server/Resources/Telemetry.cs` | The `ActivitySource`, the `Meter`, the shared metrics, `Trace`, `AddEvent`, `TraceRoot`, and the `TraceContext` helpers |
| `FullstackTemplate.Server/Resources/RequestTracingBehavior.cs` | MediatR behavior. Gives each feature slice its own span and its duration metric |
| `FullstackTemplate.Server/Resources/LoggerExtensions.cs` | `Here()`, which adds the call site to a log event |
| `FullstackTemplate.Server/Databases/QueryTagging.cs` | `TagWithOperationCallSite`, which names an EF query |
| `FullstackTemplate.Server/Databases/QueryTagEnrichment.cs` | Copies the query tags into the Npgsql span |
| `FullstackTemplate.Server/Databases/DomainEventDispatcher.cs` | Runs each domain event handler in its `domain_event.handle` span |
| `FullstackTemplate.Server/Resources/Extensions/HostExtensions.cs` | OpenTelemetry and Serilog registration, resource attributes, framework meters, request log level |
| `FullstackTemplate.Bff/Telemetry.cs`, `FullstackTemplate.Bff/LoginTraceLink.cs` | BFF telemetry and the login trace link |
| `FullstackTemplate.Bff/LoggerExtensions.cs`, `FullstackTemplate.Bff/Extensions.cs` | The BFF copies of `Here()` and of the registration |

## Log, span, or event

For the rules on what each backend feature must emit, see [backend/feature-telemetry.md](backend/feature-telemetry.md).

Each signal answers a different question. Pick the signal from the question:

| Question | Signal | Helper |
|---|---|---|
| How long did this unit of work take, and what did it call? | Span | `Telemetry.Trace` |
| When did a point inside a span occur? | Span event | `Telemetry.AddEvent` |
| What did the app decide, and why? | Log | `logger.Here().Information(...)` |
| How many, and how fast, over time? | Metric | `Telemetry.Meter` |

The feature slice span, the HTTP span, and the Npgsql span already exist for each request. Add a child span only for work that has its own duration, such as a call to an external service or a long calculation. Write a log for a state change or a decision that a person can need later, such as "Created tenant 42 for new user 7". Do not write a log that only repeats a span, such as "Handling GetUser".

## Custom spans and events

`Telemetry.Trace` starts a child span of the current activity. `Telemetry.AddEvent` adds an event to the current activity. Both add the `code.function`, `code.file.path`, and `code.line.number` tags of the caller, so each span and each event leads to its code.

```csharp
using var activity = Telemetry.Trace([("app.tenant_id", tenantId)], "invoice.render");
// ... the work
Telemetry.AddEvent([("app.page_count", pages)], "invoice.paginated");

Telemetry.RecordError(activity, exception);
```

Use a stable dotted span name with no ids or data in it. `AddEvent` does nothing when no activity is current. In top-level statements, such as `Program.cs`, `code.function` is `<Main>$`. The file and the line are still correct.

## Metrics

The custom metrics are on the `FullstackTemplate.Server` meter:

- `app.request.duration` - a histogram of the duration of each feature slice request, in seconds. `RequestTracingBehavior` records it. The tags are `app.request.name` (for example `GetUser.Query`) and, when the request throws, `error.type`.
- `app.errors` - a counter of server errors. `RecordError` increments it with the `exception.type` tag. A client error (4xx) does not increment it.

The host also collects these framework meters:

- ASP.NET Core, Kestrel, `System.Net.Http`, and the .NET runtime (from the instrumentation packages).
- `Npgsql`, from the Aspire Npgsql integration.
- `Polly`, for the resilience handlers on `HttpClient`. The meter has data only after an outgoing call.
- `Microsoft.EntityFrameworkCore`. The server only.
- `Microsoft.AspNetCore.Authentication` and `Microsoft.AspNetCore.Authorization`.

The BFF has these custom metrics on the `FullstackTemplate.Bff` meter. Its HTTP, YARP, and auth meters measure each request. The BFF keeps `app.errors` for `RecordError`.

| Counter | Unit | Tags | Content |
|---|---|---|---|
| `bff.sessions.started` | `{session}` | none | The BFF sessions that a login created |
| `bff.sessions.ended` | `{session}` | `reason`: `logout`, `backchannel_logout`, `refresh_failure`, or `expiry` | The BFF sessions that ended |
| `bff.refresh.failures` | `{failure}` | `kind`: `permanent` or `temporary` | The failed token refreshes |

A `permanent` failure is an `invalid_grant`, or an ID token in the refresh response that fails the checks. The BFF then ends the BFF session with `reason=refresh_failure`. A `temporary` failure is any other failure, also a refresh lock that is not free within `Bff:Refresh:LockTimeout`. The BFF session then stays.

## Resource attributes

Traces, metrics, and logs share the same resource attributes: `service.name`, `service.version`, `service.instance.id`, `deployment.environment`, and `host.name`. Serilog exports logs with its own OTLP sink, not with the OpenTelemetry SDK. For this reason, `HostExtensions.ResourceAttributes` gives the one list to both. As a result, the dashboard shows one resource for all three signals.

The values in `OTEL_SERVICE_NAME` and `OTEL_RESOURCE_ATTRIBUTES` win over the defaults in the code. Aspire sets `service.name` to the resource name (`server` or `bff`) and `service.instance.id` to the replica id. The dashboard finds the telemetry of a resource by these two values. Thus `dotnet aspire otel traces server` finds the traces of the server. With no such variables, as in a deployment that does not set them, `service.name` is the application name, for example `FullstackTemplate.Server`.

## Feature slice spans

`RequestTracingBehavior` wraps each MediatR request in a span. The span name is the feature class plus the request type, for example `GetUser.Query`. You do not have to add code to a new slice to get this span or the `app.request.duration` metric.

The behavior tags an exception type as `error.type`. A client error does not mark the span as failed. A client error is an exception for which `DomainError.IsClientError` is true. These are the domain exceptions that each entrypoint maps to a client error, for example a 4xx response (see the error table in [backend.md](backend.md#error-responses)). All other exceptions mark the span with the `Error` status and increment `app.errors`.

## Caller tags

`CallerIdentityBehavior` resolves the caller before the handler runs. It then tags the feature slice span with the caller, and adds the caller to the log context of the handler. Thus each log that the handler writes has the caller too. A value that the caller does not have is not added. The behavior runs before `RequestValidationBehavior`, so a request that fails validation also has the caller tags.

| Span tag | Log property | Value |
|---|---|---|
| `app.caller.kind` | `CallerKind` | `user`, `delegated_agent`, `machine_agent`, `all_tenants_client`, `unknown_client`, `anonymous`, or `system` |
| `app.caller.subject` | `CallerSubject` | The `sub` claim of a user, also the user for whom a delegated agent acts |
| `app.caller.client_id` | `CallerClientId` | The client of an agent or of a machine client |
| `app.caller.agent_id` | `CallerAgentId` | The id of the registered agent |
| `app.caller.tenant_id` | `CallerTenantId` | The tenant of the caller |

The values are ids only. An MCP tool call can fail before its feature runs, so `McpCallTelemetry` also puts the caller tags on the span of the HTTP request of each tool call. The request log of `UseSerilogRequestLogging` runs outside the MediatR pipeline, so it does not have the caller properties. Use its trace id to find the feature slice span.

## Domain event handler spans

`AppDbContext.SaveChangesAsync` runs each domain event handler in a `domain_event.handle` span. The span is a child of the current activity, which is the feature slice span in a request. The tags are `app.domain_event.type` and `app.domain_event.handler`. They have the type names only, never the data of the event.

The handler span tags the type of an exception as `error.type`, the same as the feature slice span. A server error also marks the span with the `Error` status and adds the exception. The handler span writes no log and does not increment `app.errors`. The exception fails the save, and the code that called the save counts it one time. In a request, the feature slice span counts it.

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

`PagedList.CreateAsync` runs two queries on the tagged query that it gets: the count and the page. It tags the count query with `TagAsCountQuery`, which adds the `count` suffix to the name. Thus the count of `users.list` is `SQL: users.list.count`, and the page is `SQL: users.list`.

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

The BFF names the OIDC callback spans `GET /signin-oidc` and `GET /signout-callback-oidc`. No route handles these paths, so without this rule the span name is only `GET`.

## Logs

Serilog is the logging provider. The OTLP sink sends logs to the Aspire dashboard when `OTEL_EXPORTER_OTLP_ENDPOINT` is set. The sink must stay inside the `AddSerilog` callback in `HostExtensions.cs`. The host replaces a logger that you assign to `Log.Logger` in a different location, so its sinks never run.

### Call site

Write each log through `Here()`:

```csharp
private readonly ILogger _logger = Log.ForContext<Handler>();

_logger.Here().Information("Created tenant {TenantId} for new user {UserId}", tenant.Id, user.Id);
```

`Here()` adds these properties to the log event:

- `CallSite` - the short form, for example `MigrationHostedService.cs:StartAsync@31`.
- `code.function`, `code.file.path`, and `code.line.number` - the same names as on a span.

Log only ids, names of operations, and counts. The privacy rule for spans also applies to logs.

### Console output

In Development, the console uses a `Serilog.Expressions` template. Each line shows the time, the level, `SourceContext`, `CallSite`, and the trace id. The message is on the next line. In other environments, the console writes `CompactJsonFormatter` JSON. `appsettings.json` gives the formatter as an object, so that `appsettings.Development.json` can replace its type.

### Request logs

`UseSerilogRequestLogging` writes one log for each request. `RequestLogLevel` sets its level:

- An exception or a status code of 500 or more gives `Error`.
- A health probe (`/health` or `/alive`) gives `Verbose`, so the default minimum level drops it.
- A health probe that the caller leaves during a check gives `Verbose`. `CallerCancellationHealthCheck` ends each check with the cancellation, so the exception handler gives 499, not 503. A check that runs past its own timeout still reports `Unhealthy` at `Error`.
- All other requests give `Information`.

## Tests

- Unit tests use an `ActivityListener` and filter spans by the trace id of the test. Test classes run in parallel, so the listener sees spans from other tests. See `tests/FullstackTemplate.UnitTests/Resources/TelemetryTests.cs`.
- Unit tests for metrics use a `MeterListener`. See `tests/FullstackTemplate.UnitTests/Resources/RequestTracingBehaviorTests.cs`.
- Functional tests use the OpenTelemetry in-memory exporter. `TestBase.ExportedSpans()` returns the spans that the current test exported. `TestBase.AllExportedSpans()` also returns startup spans, such as `database.migrate`. See `tests/FullstackTemplate.FunctionalTests/FunctionalTests/Telemetry/TracingTests.cs`.
- `TestingWebApplicationFactory.ExportedMetrics` collects the exported metrics. Call `MeterProvider.ForceFlush()`, then read them with `ExportedMetrics.Snapshot()`. See `tests/FullstackTemplate.FunctionalTests/FunctionalTests/Telemetry/MetricsTests.cs`.

## Read telemetry as an agent

The `aspire-telemetry` skill gives the commands that query logs, spans, and traces, and the steps from a log or a span to the code.
