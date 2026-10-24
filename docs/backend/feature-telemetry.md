# Feature telemetry

Each backend feature must give the on-call person enough data to answer questions about production. The data comes from traces, logs, and metrics only. The on-call person has no debugger, no breakpoints, and no time to deploy a new log line during an incident.

This doc gives the rules for what a feature emits. [telemetry.md](../telemetry.md) describes the helpers (`Telemetry.Trace`, `Here()`, `TraceRoot`, and more) and the host registration. Read both before you add or change a feature.

## What a feature gets with no code

The pipeline gives each feature these signals:

| Signal | From | Content |
|---|---|---|
| HTTP, gRPC, or MCP span | ASP.NET Core instrumentation | Route, status code, duration |
| Feature slice span, for example `UpdateTenant.Command` | `RequestTracingBehavior` | Duration, `error.type`, the `Error` status for a server error |
| Caller tags on the feature span | `CallerIdentityBehavior` | `app.caller.kind`, and the `app.caller.subject`, `app.caller.client_id`, `app.caller.agent_id`, and `app.caller.tenant_id` that the caller has |
| Caller properties on each log of the handler | `CallerIdentityBehavior` | `CallerKind`, `CallerSubject`, `CallerClientId`, `CallerAgentId`, and `CallerTenantId` |
| `app.request.duration` | `RequestTracingBehavior` | A histogram for each feature, tagged with `app.request.name` and `error.type` |
| `app.errors` | `Telemetry.RecordError` | A counter of server errors, tagged with `exception.type` |
| `domain_event.handle` span | `DomainEventDispatcher` | One span for each domain event handler |
| Request log | `UseSerilogRequestLogging` | One log for each request, at `Error` for a status of 500 or more |
| SQL span | Npgsql instrumentation | One span for each query |

These signals tell that a request was slow or failed, and who sent it. They do not tell which entity the request changed, which query was slow, or what the handler decided. The rules below add that data.

A SQL span with no tag gives no help. Compare one trace before and after the rules:

```
Before:
PUT api/v{version}/users/{id}/role
└─ UpdateUserRole.Command            430ms   app.caller.kind=user  app.caller.tenant_id=ten_01J...
   ├─ <database>                     412ms
   └─ <database>                       8ms

After:
PUT api/v{version}/users/{id}/role
└─ UpdateUserRole.Command            430ms   app.caller.kind=user  app.caller.tenant_id=ten_01J...
                                              app.user_id=usr_01J...  app.user.role=Admin
   ├─ SQL: users.update_role.load    412ms   code.file.path=.../UpdateUserRole.cs  code.line.number=32
   └─ SQL: <the save>                  8ms
Log: Changed the role of user usr_01J... to Admin   CallSite=UpdateUserRole.cs:Handle@66  CallerTenantId=ten_01J...
```

## The support questions

Write telemetry to answer a question that a person asks during an incident. If no person will ask the question, do not emit the signal.

| Question | Signal that answers it | What the feature must do |
|---|---|---|
| Why did this request fail? | The trace of the request | Nothing for a thrown exception. If you catch an exception, see rule 6. |
| Which line of code failed or was slow? | `code.file.path` and `code.line.number` | Tag each query (rule 1). Write each log through `Here()`. |
| Which tenants, users, or agents had this error? | The `app.caller.*` tags | Nothing. `CallerIdentityBehavior` adds them. |
| Which entities had this error? | Span tags such as `app.user_id` | Tag the feature span with the ids (rule 2). |
| What did tenant X do in the last hour? | A search for the tenant id: `dotnet aspire otel spans server --search <id>` | Nothing for the caller tags. Tag the entity ids (rule 2). |
| What happened to entity X yesterday? | Logs with the id as a property | Log each state change (rule 3). |
| Did the code take a fallback, skip an item, or return a partial result? | `Warning` log, span event | Log each degraded path (rule 4). |
| Did the rate of an outcome change after a deploy? | A metric, compared by `service.version` | Add a business metric (rule 8). |
| Did the background job run, and what did it do? | A `TraceRoot` trace and its logs | Give the job its own trace (rule 9). |

## Rules

1. Tag each EF query with `TagWithOperationCallSite`.
   - Put the tag directly after the `DbSet`. If you build the query in parts, tag the first part.
   - The tag must be on the `IQueryable`, before `ToListAsync` or `FirstOrDefaultAsync`.
   - Use a constant name in the form `area.operation`, for example `users.update_role.load`. Never put an id or a value in the name.
   - Before you add a name, search the code for it. Each name identifies one query.
2. Tag the feature span with the ids of the entities that the feature reads or changes.
   - Do not tag the caller. `CallerIdentityBehavior` already adds the `app.caller.*` tags and the `Caller*` log properties.
   - Call `Activity.Current?.SetTag("app.user_id", user.Id)` in the handler. In a handler, `Activity.Current` is the feature slice span.
   - Set the tags before you start a child span. Inside a child span, `Activity.Current` is the child.
   - Tag the id as soon as the handler knows it. Then a later failure also has the id.
   - Also tag a value from a fixed set that changes the path of the code, for example `app.user.role`.
3. Write an `Information` log for each state change or business decision that a person can need later.
   - Write one log for each outcome, not one log for each step.
   - Put the ids in named properties: `_logger.Here().Information("Changed the role of user {UserId} to {Role}", user.Id, role)`.
   - Use a message template. Do not use string interpolation.
   - Do not write a log that only repeats a span, such as "Handling UpdateUser" or "Query done".
4. Write a `Warning` log for each degraded path. Examples are a fallback, a skipped item, a partial result, and a retry that gave up.
   - For a normal branch inside a span, such as a cache miss or a lock that was acquired, add a span event with `Telemetry.AddEvent`. Do not write a log.
5. Do not log a client error. A client error is a domain exception for which `DomainError.IsClientError` is true, such as `NotFoundException` or `ValidationException`. The feature slice span already tags `error.type`, and the request log has the status code.
6. Let a server error propagate. `RequestTracingBehavior` records it. If you catch an exception and continue, do all of these steps:
   - Call `Telemetry.RecordError(activity, exception)`. If an outer span records the same exception, call `Telemetry.MarkFailed` instead, so that `app.errors` counts it one time.
   - Write an `Error` log with the exception as the first argument: `_logger.Here().Error(exception, "...")`.
7. Start a child span with `Telemetry.Trace` only for work that has its own duration. Examples are a group of external calls that make one unit, a batch loop, and a long calculation. One `HttpClient` call already has its own span.
8. Add a business metric when a person needs a rate or a trend, not a single event.
   - Examples: the tenants created, the logins rejected for each reason, and the items that a job processed.
   - Declare the instrument in `Domain/[EntityName]s/[EntityName]Metrics.cs`, on `Telemetry.Meter`. The host already exports this meter.
   - Name it `app.[entities].[past-tense verb]`, for example `app.users.role_changed`. Give the unit in braces, for example `{user}`.
   - Give it tags from a small fixed set of values only, such as a role, a reason, or an outcome.
9. Give background work its own trace with `Telemetry.TraceRoot`.
   - Write one `Information` log with the result and the counts, for example "Deleted {Count} expired proofs".
   - Catch the exception at the top of the job. Call `Telemetry.RecordError` and write an `Error` log. A background job has no `RequestTracingBehavior` to record it.
   - See `MigrationHostedService.cs` and the DPoP replay cache cleanup in `DPoPAuthentication.cs`.
10. If a trace must cross a hop that has no `traceparent` header, carry it with `TraceContext`. Examples are a queued message, a stored job row, and a redirect.
    - If the work on the other side continues the same story, make the context the parent of the new span.
    - If the work is a different story with a cause between the two, add the context as a link. `LoginTraceLink.cs` in the BFF uses a link.
11. Obey the privacy rule. Telemetry carries ids, operation names, counts, and values from a fixed set.
    - Never put personal data or user-entered text in a tag, a log property, or a metric tag. Examples are names, email addresses, and free text.
    - If a log property comes from the caller, such as a path or a header, clean it with `LogValue.Clean`.

## Cardinality

A span tag or a log property can hold an id. A metric tag must never hold an id. Each new value of a metric tag starts a new time series, and the metrics backend stores each time series. A tag with 100,000 user ids makes 100,000 time series.

The same rule applies to the names of spans, events, and queries. Each new name is a new group in the trace backend. For this reason, a name is constant and the id goes in a tag.

| Item | Ids | Example |
|---|---|---|
| Span name, event name, query name | Never | `tenant_lookup.removed`, `users.get_by_id` |
| Span tag, event tag | Yes | `app.tenant_id` |
| Log property | Yes | `{TenantId}` |
| Metric tag | Never | `reason`, `app.user.role` |

## Names

| Item | Form | Example |
|---|---|---|
| Span, event, query | Lower case, dotted, `area.operation` | `invoice.render`, `migration.lock_acquired` |
| Id tag | `app.[entity]_id` | `app.tenant_id`, `app.user_id`, `app.agent_id` |
| Caller tag | `app.caller.[name]`. `CallerIdentityBehavior` owns these tags. | `app.caller.tenant_id` |
| Other tag | `app.[area].[name]` | `app.user.role`, `app.tenant_lookup.removed_count` |
| Metric | `app.[entities].[past-tense verb]` | `app.users.role_changed` |
| Log property | PascalCase | `{UserId}`, `{Count}` |

Before you add a tag name, search the code for a tag with the same meaning. Use one name for one concept in all features.

## Log levels

| Level | Use it for | Example |
|---|---|---|
| `Error` | A server error that the code caught, or a failure that needs a person | "Could not apply the migrations of {DbContext}" |
| `Warning` | A degraded path. The request succeeded, but not in the normal way. | "Skipped {Count} rows with no tenant" |
| `Information` | A state change or a business decision | "Created tenant {TenantId} for new user {UserId}" |
| `Debug` | Data for a developer only. The default minimum level drops it. | "Sent the request again with the DPoP nonce" |

Production keeps `Information` and higher (`appsettings.json`). Thus a `Debug` log is not available during an incident.

## Example

`UpdateUserRole` has this telemetry. A role change gives or removes access, so support needs to find it later.

`Domain/Users/UserMetrics.cs`:

```csharp
namespace FullstackTemplate.Server.Domain.Users;

using System.Diagnostics.Metrics;
using Resources;

public static class UserMetrics
{
    public static readonly Counter<long> RoleChanged = Telemetry.Meter.CreateCounter<long>(
        "app.users.role_changed",
        unit: "{user}",
        description: "Number of role changes, tagged with the new role");
}
```

`Domain/Users/Features/UpdateUserRole/UpdateUserRole.cs`, the telemetry lines and the lines around them:

```csharp
private readonly ILogger _logger = Log.ForContext<Handler>();

public async Task<UserDto> Handle(Command request, CancellationToken cancellationToken)
{
    Activity.Current?.SetTag("app.user_id", request.Id);

    var caller = await permissionGuard.GetCallerAsync(cancellationToken);
    caller.Require(Permission.ManageUserAccess());
    var newRole = UserRole.Of(request.Role);
    Activity.Current?.SetTag("app.user.role", newRole.Value);

    // ... the permission checks, the load, the update, and the save, as before

    _logger.Here().Information("Changed the role of user {UserId} to {Role}", user.Id, newRole.Value);
    UserMetrics.RoleChanged.Add(1, new KeyValuePair<string, object?>("app.user.role", newRole.Value));

    return user.ToUserDto();
}
```

The example obeys these rules:

- The id is on the span, and the log has it as a property. The metric has the role only.
- The role is a value from a fixed set, so it is safe as a metric tag.
- The log and the metric come after the save. Thus they record only a change that the database kept.

## Test the telemetry

If an alert, a dashboard, or a runbook uses a signal, write a test for it. A test stops a rename that breaks the alert. A log with no consumer needs no test.

- For a span tag, use a functional test. Start a parent activity, send the request, and filter `ExportedSpans()` by the trace id of the parent. See `tests/FullstackTemplate.FunctionalTests/FunctionalTests/Telemetry/TracingTests.cs`.
- For a metric, call `MeterProvider.ForceFlush()`, then read `TestingWebApplicationFactory.ExportedMetrics`. See `MetricsTests.cs` in the same folder.
- A test class that reads all the exported spans must have `[NotInParallel]`.

## Make sure that it works in the stack

Do these steps after each change to the telemetry of a feature. The `aspire-telemetry` skill gives the commands.

1. Start the stack with the `aspire-cli` skill.
2. Send the request through the UI or the API. Also send one request that fails.
3. Find the trace with `dotnet aspire otel traces server`. Get its trace id.
4. Read the spans with `dotnet aspire otel spans --trace-id <id>`. Make sure that the feature span has the id tags, and that each SQL span has a `SQL:` name.
5. Read the logs with `dotnet aspire otel logs --trace-id <id>`. Make sure that each log has a `CallSite` and has no personal data.
6. For a metric, open the Metrics page of the Aspire dashboard. The `dotnet aspire otel` CLI has no metrics command.

## Pitfalls

| Symptom | Cause | Fix |
|---|---|---|
| A SQL span has the name of the database, not `SQL: ...` | The query has no tag, or the tag is after the materialization | Put `TagWithOperationCallSite` on the `IQueryable`, directly after the `DbSet` |
| Each span or query has a different name | The name has an id or a value in it | Use a constant name. Put the id in a tag. |
| The id tag is on a child span, not on the feature span | `SetTag` ran inside a `Telemetry.Trace` scope | Tag `Activity.Current` before you start the child span |
| A background job has no spans for its queries | No activity is current, so the Npgsql filter drops the queries | Start the job with `Telemetry.TraceRoot` |
| A log has no `CallSite` | The log did not go through `Here()` | Write `_logger.Here().Information(...)` |
| A 404 shows as a server error | The code caught a client error and called `RecordError` | Let the domain exception propagate |
| `app.errors` counts one failure two times | Two spans called `RecordError` for one exception | Call `MarkFailed` on the inner span |
| The metric has too many time series | A metric tag has an id | Remove the id. Put it on the span. |
| A log line is in the dashboard but not in production | The log is at `Debug` | Use `Information` for a state change |

## Cost

The trace backend and the log backend charge for each span, each log, or each gigabyte. Three controls keep the cost low:

- Constant names for spans, events, and queries.
- No log that repeats a span.
- Sampling. The host sets no sampler, so it exports each span. A production deployment can add head sampling in the SDK, or tail sampling in an OpenTelemetry Collector. Neither change needs a change to feature code.
