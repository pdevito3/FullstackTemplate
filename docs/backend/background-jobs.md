# Background jobs

A job is work that runs after the commit of the save that asks for it. Examples are a delete of an object in storage, an email, and a call to another system. The Server runs the jobs with BackWave. [ADR 0013](../adr/0013-background-jobs-on-backwave.md) gives the reasons for this design.

BackWave keeps the jobs in the `backwave` schema of `appdb`. It runs each job at least one time, after the commit, in a new DI scope for each attempt. The job runs as the tenant in its payload.

## The guarantee

A job commits with the save that queues it, or it does not exist.

- If the save fails, for example on a 409 conflict or a handler exception, the save writes no job.
- If the reply of the commit is lost, the save writes each job one time.
- BackWave gives a job to one worker at a time. A job can run more than one time: after a failed attempt, after a lost lease, or after a requeue by an operator. So make each handler idempotent (rule 9).

## Rules

1. Make each job a `sealed record` with `[Job("wire-name")]` that implements `ITenantJob`. The `TenantId` of the job is the tenant that the job runs as.
2. Put IDs and small values in a job, not entities and not personal data. The payload is stored, and an operator can read it in the dashboard.
3. Do not change the wire name of a job after you deploy it. BackWave stores the name with each job, and a stored job with an unknown name goes to the quarantine. A new payload must also decode the stored jobs of the old payload. If it cannot, add a job with a new wire name, and keep the old job until no stored job has its name.
4. Make each handler a `sealed class` that derives from `JobHandler<TJob>`, and put the work in `RunAsync`. Take `JobScope` in the constructor and give it to the base. The source generator of BackWave finds the job and its handler, so they need no registration.
5. Put a job and its handler in one file, in the folder of the domain that owns the work. `Domain/ObjectDeletes/DeleteObject.cs` is an example.
6. Queue a job with `dbContext.QueueJob(job)`, in a feature handler or a domain event handler. Then call `SaveChangesAsync`. Do not use `BackWaveClient`. The build bans it (RS0030), because its jobs do not commit with the save.
7. Queue a job only for a tenant that the caller can see. `QueueJob` refuses a job of another tenant. In a domain event handler, the scope is the tenant of the event.
8. Save each change that a handler makes. `JobHandler` throws when the handler leaves a change, an audit action, or a queued job unsaved. A request that queues a job and does not save also throws.
9. Make each handler safe to run two times. For an external call, send an idempotency key that comes from the effect, for example the storage key or the ID of the user. Do not use a new GUID, because each attempt gets a new one.
10. Let a failure throw. Do not catch it and log it. BackWave records the exception, runs the job again after a delay, and dead-letters it after the last attempt.
11. Inject what the handler needs, such as `AppDbContext` or `ObjectStorage`. Each attempt gets a new DI scope, so a scoped service does not keep state from an earlier attempt.

<!-- example:start -->
The welcome email in `FullstackTemplate.Server/Examples/WelcomeEmails/` shows the rules. A domain event handler of `UserCreated` queues `SendWelcomeEmail`. The handler of the job sends one email with the key `welcome-email/{userId}`, and a user that was deleted before the job ran gets no email.
<!-- example:end -->

## What a handler can do

`JobHandler` gives each attempt the caller `TenantJob` of the tenant in the payload, before the first query.

- It sees the rows of its tenant only. The tenant filter applies as for a user of that tenant.
- It holds each permission in its tenant. It cannot manage all tenants.
- It can save. Each save of the job has the name of the job as its audit request. The audit actor is null, as for other work with no token.
- It can queue another job of its tenant.

## Email

`IEmailSender` in `Resources/Email/` sends an email. The Server registers `LogEmailSender`, which sends nothing. It writes the idempotency key of the message to the log, with no address and no text, because they are personal data.

To send real email, write a sender for your email provider, and register it in place of `LogEmailSender` in `WebAppServiceConfiguration`. Give the idempotency key of the message to the provider, so that the provider sends each key one time. Send an email only from a job, so that a failed save sends no email.

## Failures, retries, and dead jobs

- A failed attempt waits, and then the job runs again. The default of a worker group is 10 attempts. The delay after attempt `n` is 2^`n` seconds, at most 5 minutes. The object deletes have their own settings (see [attachments.md](attachments.md#the-order-of-the-writes)).
- To give one job other attempts, put `[Retry(maxAttempts, backoffSeconds...)]` on the job. An attempt that stops on a lost lease, for example on a crash of the process, uses the policy of the group.
- After the last attempt, the job is dead-lettered, and no worker runs it again. A person corrects the cause and requeues the job in the dashboard. The requeue gives the job all its attempts again.
- A stored job with no handler, or with a payload that does not decode, goes to the quarantine. No handler runs. After a deploy that can run it, a person can requeue it.

## Queues and worker groups

Each job has a queue. Set it with `[Job("wire-name", Queue = "name")]`. A job with no queue is on the queue `default`.

The Server runs one worker group for each queue of the jobs (`JobGroups`). A job on a new queue gets its group with no code. A group runs at most 20 jobs at a time in each process, and it polls the store each second.

### Noisy neighbors

The jobs of one queue share the pool of its group. A slow job, or a burst of jobs, fills the pool, and the other jobs of the queue wait.

- Give a job its own queue when it can be slow, when it can come in a burst, or when it calls a system that can be down. Then a flood or an outage holds up only that queue. The object deletes have their own queue for this reason. When storage is down, only their queue waits.
- To change the settings of the group of a queue, such as its pool size or its retry policy, add an options class and a case in `JobGroups.For`. `ObjectDeleteOptions` is an example.
- BackWave has no fair share between tenants. A queue runs its jobs in the order of their due time. So a tenant that queues many jobs delays the jobs of the other tenants on that queue. When one request can queue many jobs, queue one job that does the batch, or put the job on its own queue.

## Roles

One setting tells a Server process what work to do: `Server:Role` (env `Server__Role`).

| Role | Serves the HTTP API, gRPC, MCP, and the jobs dashboard | Runs the worker groups |
|------|---------------------------------------------------------|------------------------|
| `All` (the default) | Yes | Yes |
| `Api` | Yes | No |
| `Worker` | No | Yes |

- Each role serves `/health` and `/alive`. In a role that runs jobs, `/health` also checks that no worker group stopped on a fault of the store.
- Each role queues the jobs of its saves, and each role migrates the database at its start. Processes can start at the same time on a new database. If the database does not exist, the first process creates it. The migration lock lets one process migrate at a time.
- The start log `Server role {ServerRole}` gives the role of the process.
- A value that is not a role stops the start with exit code 1. The fatal log `Application terminated unexpectedly` has the message `Server:Role is 'workr'. Use All, Api, or Worker.`

### Split a deploy

Run the same image as two groups of processes, to scale the requests and the jobs apart:

1. Run the processes that serve the requests with `Server__Role=Api`. Send the traffic of the BFF to these processes only.
2. Run the processes that run the jobs with `Server__Role=Worker`. They serve only `/health` and `/alive`, so they need no ingress.
3. Give each process the same settings: the connection string of `appdb`, the object storage, and `BackgroundJobs:OperatorSubjects`.

Each process that runs jobs runs a group for each queue. The keys of Data Protection are in the `data_protection` schema of `appdb`, so each Api process reads the dashboard tokens that another process wrote. The AppHost runs one Server with the role `All`.

## The jobs dashboard

The BackWave dashboard is at `/backwave` of the app. The BFF sends the page to the Server with the access token of the session.

- Only an operator can open it. `BackgroundJobs:OperatorSubjects` is the list of the `sub` claims of the operators. The default is empty, and then nobody can open the dashboard.
- An operator is a user who signs in through the BFF. An agent or a client for all tenants is never an operator, also with the same `sub`.
- The sidebar of the SPA shows **Background jobs** to an operator only (`canOpenJobDashboard` of `GET /api/v1/users/me`).
- A browser with no session goes to the login, and then comes back to the page. A link from another site, such as a link in an email, opens the page with the session of the browser. A user who is not an operator gets a 403.
- Each job has the tag `tenant` with its tenant ID, so an operator can find the jobs of one tenant.
- An operator can read the payloads and the failure details, requeue a job, and cancel a job. The dashboard refuses a pause of a queue, because a pause holds the jobs of all tenants. It also refuses a trigger of a schedule.
- In run mode, the AppHost makes `admin@example.com` of Keycloak and ZeroFactor an operator. For another provider, set `BackgroundJobs__OperatorSubjects__0` on the Server to the `sub` of your user.

Only a process that serves requests serves the dashboard. The metrics panel of the dashboard shows the jobs that the process of the page ran, not the jobs of all processes. An Api process runs no job, so its panel shows no job metrics. For a deploy with more than one process, read the `backwave.*` metrics in the telemetry backend.

## Telemetry

- Each attempt is a trace of its own. Its root is the `process {queue}` span of BackWave. The span links to the `send {queue}` span of the save that queued the job, which has the `code.*` tags of the `QueueJob` call.
- `JobScope` puts the caller on the process span and on each log of the job: `app.caller.kind` = `tenant_job` and the tenant ID.
- So a handler tags only the IDs of its entities, such as `app.user_id`, and tags each query with `TagWithOperationCallSite`. It writes one `Information` log with the result. It does not call `Telemetry.TraceRoot` or use `TraceContext`. See rules 9 and 10 of [feature-telemetry.md](feature-telemetry.md#rules).
- The build does not check a job handler for the ID tag and the log (FSTTEL002 checks MediatR handlers only), so add them yourself.
- BackWave counts the failed attempts in `backwave.jobs.failed` and the dead jobs in `backwave.jobs.dead_lettered`. `backwave.queue.depth` gives the jobs of each queue and state. It writes the logs `Retry scheduled after a failed attempt` and `Job dead-lettered after exhausting its retry ceiling.`.

## Test a job

- To check that a save commits a job, read the job with `StoredJobs.QueuedAsync<TJob>(tenantId)` in the integration tests. No worker runs it there. The tests run in parallel on one database, so add a new tenant for a test that counts jobs.
- To run a job, use `JobHarness`. It runs the job with the handlers of a test Server, on a store in memory and a clock that the test moves. Each attempt gets a new DI scope, as in the Server.
- Replace an external dependency in the test Server, for example `RecordingEmailSender` for `IEmailSender`. To make the first call fail after the effect, give it `lostReplies: 1`.
- A test seam that fails a job write, such as `FailAfterJobWrite`, fails only after `Arm()`. Arm it after the arrange, because a save of the arrange can write jobs too.
- `ObjectDeleteJobTests` is an example.
<!-- example:start -->
- `WelcomeEmailTests` is an example with a domain event handler and an email.
<!-- example:end -->
