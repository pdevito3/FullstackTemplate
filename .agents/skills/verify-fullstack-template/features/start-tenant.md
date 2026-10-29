# Start a tenant

A new database has no user records. A user who signs in with no user record sees a notice on each page. The notice links to the page `Start a tenant`. The user gives a tenant name and starts the tenant. The user becomes its admin, with the permissions of the `Admin` role.

## Sub-features

- `notice` shows the region `No tenant` on each page except `/start-tenant`, for a signed-in user with no user record.
- `form` shows the fields `Tenant name`, `First name`, `Last name`, `Email`, and `Username`. `Tenant name` has the focus. The name, email, and username come from the claims of the identity provider.
- `required` refuses an empty field in the form, with no request.
- `start` sends `POST /api/v1/users/initiate` with the `sub` of the session and the role `Admin`. The page shows `You started <name>. You are its admin, ...`, and the link `Go to the home page` has the focus. The notice does not show again.
- `refused` shows the refusal of the Server next to its field, for example a tenant name in use.
- `member` tells a user with a user record that they already have a tenant.
- `signed-out` asks an anonymous visitor to log in.

## How to get to it (user POV)

- Log in on a new database, then choose `Start a tenant` in the notice.
- Open `/start-tenant` directly.

## Driving it with browse.mjs

Preconditions:

- `doctor.sh` exits with 0.
- The user of `login()` has no user record. This is true on a new stack.

- **Notice.** Run `page.goto('/')`, then `login()`. `page.getByRole('region', { name: 'No tenant' })` contains `You are not a member of a tenant.`.
- **Form.** Run `notice.getByRole('link', { name: 'Start a tenant' }).click()`. The heading `Start a tenant` is visible. `First name`, `Last name`, and `Email` have the values of the `given_name`, `family_name`, and `email` claims of `GET /bff/user`.
- **Start.** Fill `Tenant name` with a unique name, then click `Start tenant` after `waitForResponse` on `POST /api/v1/users/initiate`. The response is 201. `user.role` is `Admin` and `user.identifier` is the `sub` claim. The page names the tenant of the response, and `Go to the home page` has the focus. Choose it. The notice has count 0.
- **Permissions.** `fetch('/api/v1/users')` in the page gives 200 with the new admin.
- **Proof.** Run `node $S/scripts/browse.mjs $S/recipes/start-tenant.mjs`. The captures `home-with-notice`, `start-tenant-form`, `started`, and `home-after-start` show the four states.

## Gotchas

- A user belongs to one tenant only. After a start, the same user sees `You are already a member of a tenant.` on `/start-tenant`.
- The unique index on the tenant name also covers the deleted tenants. Give each run a new name.
- The Server caches the tenant of each user. The restore of `browse.mjs` deletes rows behind that cache, so the recipe deletes its tenant through `DELETE /api/v1/tenants/{id}` at the end. If a run stops before that step, run `dotnet aspire resource server restart --apphost FullstackTemplate.AppHost` before the next run.
