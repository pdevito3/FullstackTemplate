# Spec: user management and invites

## 1. Scope

A tenant admin adds a colleague to the tenant with an invite link. The admin creates the invite in the SPA, copies the link, and sends it in any way. The colleague opens the link, signs in with the auth provider, and joins the tenant with the role of the invite. This flow works with each auth provider, because the app does not create the account in the provider. The colleague creates or uses their own account, and the claim binds its `sub` to a new user record.

The SPA also gets a users page. It lists the users of the tenant, changes the role of a user, and removes a user. These features use the user endpoints that exist.

This spec leaves out:

- Email delivery of an invite. A later slice can add it as an option that the configuration turns on, with Mailpit in the AppHost.
- The creation of an account in the auth provider (the `TODO handle IDP creation` in `AddUser`). Build it only when a real need appears.
- A change to 0factor.

## 2. Entities and states

| Entity | Root of | Fields that a user sets | States |
|---|---|---|---|
| `Invite` | None | role, email (optional), require email match | Pending, Claimed, Revoked |

An invite also has a tenant, a token hash, an expiry, and, after a claim, the ID of the user that claimed it. A pending invite after its expiry is expired. Expired is not a stored state. The entity computes it from the expiry and the current time.

| From | To | Method | Who |
|---|---|---|---|
| Pending | Claimed | `Invite.Claim` | A signed-in user of a first-party client with no live user record |
| Pending | Revoked | `Invite.Revoke` | A user with `manage_users` in the tenant of the invite |

## 3. Rules

| ID | Rule | Enforced by | Backstop |
|---|---|---|---|
| UM-1 | The users page lists the users of the tenant of the caller, with a pager. | `GetUserList` and its tenant filter | |
| UM-2 | The users page changes the role of a user. The page shows the control only to a caller who can change the role. | `UpdateUserRole` handler check | |
| UM-3 | The users page removes a user after the caller confirms. The page shows the control only to a caller who can remove the user. | `DeleteUser` handler check | |
| INV-1 (later) | Only a caller with `manage_users` and each default permission of the role can create an invite. A caller cannot invite with a role that has more permissions than the caller has. | `AddInvite` handler check | |
| INV-2 | The server stores only a hash of the token. The create response gives the link one time only. A list or a get of an invite never gives the token. | `Invite.Create` | |
| INV-3 (later) | The link puts the token in the URL fragment (`/join#token=...`), so the token is not in a server log, a BFF log, or a `Referer` header. | SPA `join` route | |
| INV-4 | An invite expires 7 days after its creation. A claim of an expired invite fails. | `Invite.Claim` | |
| INV-5 | An invite has one use. A claim of an invite that is claimed or revoked fails. | `Invite.Claim` | Version check of `Invite` |
| INV-6 (later) | Only a signed-in user of a first-party client with no live user record can claim. If the `sub` has a live user in any tenant, the claim fails, because a user belongs to one tenant only. | `ClaimInvite` handler check, `ConflictException` (409) | Unique index on the live `users.identifier` |
| INV-7 | A claim adds a user in the tenant of the invite, with the role of the invite and the `sub` of the caller. The claim and the user add are in one save, and the save takes the shared lock of the tenant. | `ClaimInvite` handler | Shared tenant lock |
| INV-8 (later) | The join page fills the first name, the last name, the email, and the username from the token claims of the caller. The user can change each value before the claim. | SPA `join` route, `UserForCreationDto` validation | |
| INV-9 | When an invite requires an email match, the claim fails if the email of the caller is not the email of the invite. The comparison is case-insensitive. | `Invite.Claim` | |
| INV-10 | A user with `manage_users` can revoke a pending invite of their tenant. | `Invite.Revoke` | |
| INV-11 (later) | A tenant delete deletes each invite of the tenant. | `TenantOwnedRows` | `TenantOwnedRowsTests` |
| INV-12 (later) | The audit log does not show the token hash. | Audit sensitivity of `Invite` (see [audit-log.md](../backend/audit-log.md)) | `AuditSensitivityTests` |

## 4. Permissions

None. The invite features use `manage_users`, which exists.

## 5. Endpoints and tools

| Feature | HTTP | gRPC | MCP tool | Permission |
|---|---|---|---|---|
| `AddInvite` | `POST /api/v1/invites` | | | `manage_users` |
| `GetInviteList` | `GET /api/v1/invites` | | | `manage_users` |
| `RevokeInvite` | `POST /api/v1/invites/{id}/revoke` | | | `manage_users` |
| `GetInvitePreview` | `POST /api/v1/invites/preview` | | | A signed-in user |
| `ClaimInvite` | `POST /api/v1/invites/claim` | | | A signed-in user with no user record |

`GetInvitePreview` and `ClaimInvite` take the token in the body, not in the path or the query, so the token is not in a log. The preview gives the tenant name and the role, so the join page can show what the user joins before the claim.

## 6. Messages

| Rule | Status | Message |
|---|---|---|
| INV-4 | 410 | This invite expired. Ask a tenant admin for a new invite. |
| INV-5 | 410 | This invite was used or revoked. Ask a tenant admin for a new invite. |
| INV-6 | 409 | Your account already belongs to a tenant. |
| INV-9 | 403 | This invite is for another email address. Sign in with the account of that email. |

## 7. Time zones

| Value | Stored as | Shown in the zone of |
|---|---|---|
| Invite expiry | `timestamptz` (UTC) | The browser of the user |

## 8. Open decisions

| Decision | Who decides | Rules that wait |
|---|---|---|
| The default of "require email match" is off. The link is the secret, and it has one use and a 7-day expiry. Providers handle `email_verified` differently, and the Duende Demo users have fixed emails. Change the default to on if a forwarded link is a risk. | The template owner | INV-9 |
| The expiry is 7 days, and it is not a setting. | The template owner | INV-4 |
| The Server lets an admin remove their own user, or change the role of the last admin. Thus a tenant can lose its last admin. A guard for the last admin is a Server rule outside this spec. | The template owner | None |
