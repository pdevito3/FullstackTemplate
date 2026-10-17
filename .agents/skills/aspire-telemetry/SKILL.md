---
name: aspire-telemetry
description: Queries and writes the app's OpenTelemetry logs, spans, traces, and metrics. Use when you debug a failure or a slow request with the Aspire dashboard or the `aspire otel` CLI, when you go from a log or a span to its code, or when you add a span, a span event, a log, or a metric.
---

# Aspire Telemetry

The server and the BFF send logs, traces, and metrics to the Aspire dashboard. Each log written with `Here()`, and each span from the telemetry helpers, has a call site. Use the call site to go from the telemetry to the code.

The design is in `docs/telemetry.md`. Read it before you change a helper or the registration.

## Read telemetry

The AppHost must be running (`aspire run` in `FullstackTemplate.AppHost`). The commands find it from the current directory. If you are not under the repository, add `--apphost <path>`.

| You need | Command |
|---|---|
| Errors of one resource | `aspire otel logs server --severity Error` |
| Failed traces | `aspire otel traces server --has-error true` |
| All signals of one request | `aspire otel logs --trace-id <id>` and `aspire otel spans --trace-id <id>` |
| A text or attribute search | `aspire otel logs --search "<text>"` |
| Fields for a script | Add `--format Json` |
| Console output (stdout) of a resource | `aspire logs server` |

Resource names are the AppHost names, for example `server` and `bff`. Run `aspire otel logs --help` for the full flag list.

The Aspire MCP tools give the same data: `list_structured_logs`, `list_traces`, `list_trace_structured_logs`, and `list_console_logs`.

For a dashboard with no AppHost, such as the standalone `aspire-dashboard` container, add `--dashboard-url http://localhost:18888`.

### From telemetry to code

1. Find the failed log or span.
2. Read its `code.file.path` and `code.line.number` attributes. A log also has `CallSite`, for example `MigrationHostedService.cs:StartAsync@31`.
3. Open the file at that line.
4. If the item has no call site, get its trace id. Then read the other spans and logs of the trace to find an item that has one.

These spans have a call site: the spans from `Telemetry.Trace` and `Telemetry.TraceRoot`, the events from `Telemetry.AddEvent`, and the Npgsql spans of queries tagged with `TagWithOperationCallSite`. A feature slice span has no call site. Its name (for example `GetUser.Query`) is the feature class.

## Write telemetry

Pick the signal from the question that it answers:

| Question | Write |
|---|---|
| How long did this work take? | `using var activity = Telemetry.Trace([("app.tenant_id", id)], "area.operation");` |
| When did a point inside a span occur? | `Telemetry.AddEvent([], "area.event");` |
| What did the app decide, and why? | `_logger.Here().Information("Created tenant {TenantId}", tenant.Id);` |
| Work with no request (startup, a background job) | `using var trace = Telemetry.TraceRoot([], "area.operation");` |
| Which code ran this query? | `.TagWithOperationCallSite("area.operation")` on the EF query |
| An exception that is a server error | `Telemetry.RecordError(activity, exception);` |

Each feature slice already has a span and the `app.request.duration` metric from `RequestTracingBehavior`. Add a child span only for work with its own duration.

Obey these rules:

- Tags and log properties carry ids, names of operations, and counts. User-entered content and personal data, such as names and email addresses, stay out of all telemetry.
- Span names, event names, and operation names are stable dotted names with no ids in them.
- Log messages use message templates with named properties, not string interpolation.
- Each log call goes through `Here()`, so that the log has a call site.

## Make sure that it works

1. Run the request or the startup work.
2. Find the new span, event, or log with `aspire otel spans --search "<name>"` or `aspire otel logs --search "<text>"`.
3. Make sure that it has the `code.*` attributes and that it is in the correct trace.
4. For a test, read `docs/telemetry.md` (section "Tests") for the listener and exporter fixtures.
