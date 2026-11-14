# Jobs dashboard

An operator opens the BackWave dashboard of the background jobs at `/backwave`. It is a page of the Server, not of the SPA. The BFF forwards `/backwave` to the Server with the access token of the session, and the Server lets in only a user whose `sub` is in `BackgroundJobs:OperatorSubjects`. In a local run, the AppHost makes `admin@example.com` of ZeroFactor and Keycloak an operator. The SPA shows the sidebar link `Background jobs` to an operator only. A live page of the dashboard keeps a server-sent events stream open, and it shows the new state of the jobs with no reload.

## Sub-features

- `jobs-login` sends a visitor with no session from `/backwave` to the login of the IdP, and the login comes back to `/backwave`.
- `jobs-cross-site` opens the dashboard with the session from a link on another site. The browser does not send the `SameSite=Strict` session cookie on a cross-site navigation, so the BFF answers with a page that loads the same URL again from the app.
- `jobs-tenant-job` shows the `delete-tenant-objects` job of a deleted tenant as `Succeeded`, found by the `tenant` tag, and its job page gives its state and its tenant.
- `jobs-link` shows `Background jobs` in the `Main` navigation to an operator. It is a link to `/backwave` in the same tab. `GET /api/v1/users/me` gives `canOpenJobDashboard` true.
- `jobs-live` opens the stream `/backwave?live=1` with the content type `text/event-stream`.
- `jobs-queues` lists the queue `object-deletes` on `/backwave/queues`, with no pause button.
- `jobs-member` shows no `Background jobs` link to a user who is not an operator. `canOpenJobDashboard` is false, and `GET /backwave` gives 403.

## How to get to it (user POV)

- As an operator, choose `Background jobs` in the sidebar.
- Open `/backwave` of the app directly. With no session, the login of the IdP shows first.

## Driving it with browse.mjs

Preconditions:

- `doctor.sh` exits with 0, and the stack uses ZeroFactor. The recipe signs in on the ZeroFactor page itself, because `login()` expects the SPA after the login.
- The Server has the role `All`, the default. Only a role that serves requests serves the dashboard.

- **Login.** Run `page.goto('/backwave', { waitUntil: 'commit' })`. The browser goes to the IdP. Click `[data-testid="user-admin@example.com"]`. The browser comes back to `/backwave`, and the heading `Overview` shows.
- **Live.** Wait for the response of `/backwave?live=1` before the click of the login. It gives 200 with `text/event-stream`.
- **Cross-site.** Fulfill a page of another site with `page.route('http://mail.example.test/**', ...)`, with a link to `<web>/backwave/queues`. Click the link. The first load of `/backwave/queues` has no `__Host-bff` cookie and gets 200. The second load has the cookie, and the heading `Queues` shows. Read the cookie of each load from `request.allHeaders()`, because a response body is gone after the next navigation.
- **Tenant job.** Open `/`, follow `Start a tenant` in the `No tenant` region, and submit the `Start a tenant` form. `POST /api/v1/users/initiate` gives 201 with the `tenant`. Then `DELETE /api/v1/tenants/<id>` with `X-CSRF: 1` gives 204. Poll `/backwave/jobs?wire=delete-tenant-objects&state=Succeeded&tk=tenant&tv=<id>` until it has one `tbody tr`. Click the job link of the row. The heading starts with `Job `, and the page shows `Succeeded` and the tenant ID.
- **Link.** Open `/`. The `Main` navigation has the link `Background jobs` with the `href` `/backwave` and no `target`. Click it. The heading `Overview` shows.
- **Queues.** Open `/backwave/queues`. The heading `Queues` and the queue `object-deletes` show. No button names a pause. The page lists a queue only after a job was on it, so this step comes after the tenant job.
- **Member.** Open `/` and run `logout()`, because `logout()` uses the SPA. Then run `login('user@example.com')`. The `Main` navigation has `Home` and no `Background jobs`. `page.request.get('/backwave')` gives 403.
- **Proof.** Run `node $S/scripts/browse.mjs $S/recipes/jobs-dashboard.mjs`. The captures `dashboard-overview`, `cross-site-queues`, `tenant-job-succeeded`, `tenant-job`, `operator-sidebar`, `dashboard-queues`, and `member-sidebar` show the states.

## Gotchas

- The dashboard is not a route of the SPA. The SPA must load it with a document navigation, so `Background jobs` is a document link, and the Vite dev server must send `/backwave` to the BFF (`bffPaths` in `vite.config.ts`).
- Only a user who signs in through the BFF can be an operator. An agent or a client for all tenants is never an operator.
- In a dev stack, the IdP and the Vite origin are both `http://localhost`, so the browser sends the session cookie on the redirect after the login. Only the cross-site step shows the page that loads the URL again. With `launch.sh --preview`, the app is on the https origin of the BFF, so the redirect after the login is cross-site and also gets that page.
- BackWave makes the pages of the dashboard, so the recipe gives each capture of a dashboard page `{ sweep: false }`. The sweep still measures the sidebar of the SPA.
- The metrics panel of the dashboard shows the jobs that the process of the page ran. In a stack with one Server of the role `All`, that is each job.
- The restore of `browse.mjs` skips the job store, so the dashboard keeps the jobs of each earlier run. Find the jobs of a recipe by the `tenant` tag of a tenant that the recipe made: `/backwave/jobs?tk=tenant&tv=<tenant ID>`. Do not count all the jobs of a queue.
