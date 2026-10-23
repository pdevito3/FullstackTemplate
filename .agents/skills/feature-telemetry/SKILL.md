---
name: feature-telemetry
description: Adds the spans, span tags, logs, and metrics that a backend feature needs for production support and incident work. Use when you add or change a Server feature, a command or query handler, a domain event handler, a background job, or an external call, and before you call a backend change done. For the commands that query telemetry, use aspire-telemetry.
---

# Feature telemetry

A backend change is not done until the on-call person can answer questions about it from telemetry alone. The rules are in `docs/backend/feature-telemetry.md`. The helpers are in `docs/telemetry.md`. Read both before you start.

## Procedure

1. Read the handler from top to bottom. List each exit path: success, each client error, each server error, each fallback, and each skipped item.
2. For each path, write the question that a person asks during an incident. Use the table "The support questions" in `docs/backend/feature-telemetry.md`.
3. For each question, add the signal that answers it. Obey the rules in the doc. The minimum for each feature:
   - Each EF query has `TagWithOperationCallSite` with a constant `area.operation` name.
   - The feature span has the ids of the entities that the feature reads or changes, set with `Activity.Current?.SetTag`.
   - Each state change has one `Information` log through `Here()`, with the ids as named properties.
   - Each degraded path has a `Warning` log.
   - Each caught server error calls `Telemetry.RecordError` and writes an `Error` log with the exception.
4. If a person needs a rate or a trend, add a counter in `Domain/[EntityName]s/[EntityName]Metrics.cs` on `Telemetry.Meter`.
5. If the work has no request, wrap it in `Telemetry.TraceRoot`.
6. Search the code for each new tag name and metric name. If a name with the same meaning exists, use it.
7. If an alert, a dashboard, or a runbook uses a signal, write a functional test for it. Use the `writing-tests` skill and the section "Test the telemetry" of the doc.
8. Run the feature in the stack, then read its trace and logs. Do the steps in the section "Make sure that it works in the stack" of the doc.

## Review the result

Before you call the change done, make sure that each item is true:

- [ ] No span name, event name, query name, or metric tag has an id or a value from the caller.
- [ ] No tag, log property, or metric tag has personal data or user-entered text.
- [ ] Each log uses `Here()` and a message template.
- [ ] No log repeats a span, and no log records a client error.
- [ ] Each tag name matches the names in the section "Names" of the doc.
- [ ] The trace of the request in the running stack shows the id tags on the feature span and a `SQL:` name on each SQL span.
- [ ] The logs of that trace each have a `CallSite`.

## Do not

- Do not tag the caller (tenant, user, client, or agent) in a handler. `CallerIdentityBehavior` adds the `app.caller.*` tags and the `Caller*` log properties to each request.
- Do not catch an exception only to log it. `RequestTracingBehavior` records a server error that propagates.
- Do not call `RecordError` for a client error such as `NotFoundException`. It colors a 4xx as a server error.
- Do not add a child span around one query or one `HttpClient` call. Each one already has a span.
- Do not raise a log to `Error` for an expected outcome. An `Error` log tells a person that something needs a fix.
