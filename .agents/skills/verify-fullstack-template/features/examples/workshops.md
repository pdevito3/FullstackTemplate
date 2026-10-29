# Workshops

A user with the `manage_workshops` permission adds a venue and a workshop in it, edits the workshop, and moves it through its statuses. The list at `/workshops` has a search and a pager in the URL. The detail page shows a button only for each action that the API allows. A save that sends an old version gets a 409, and the form keeps the user on the page with a reload.

## Sub-features

- `workshops-onboard` gives the signed-in user a Server user with the role `Admin`, so the user has `manage_workshops`.
- `workshops-empty` shows `No workshops yet. Add one with New workshop.` on a new tenant.
- `venue-create` adds a venue at `/venues/new` and opens `/workshops/new?venueId=<id>` with the venue picked.
- `workshop-create` adds a workshop and opens its detail page with the status `Draft`.
- `workshop-detail` shows the start in the time zone of the venue, with the zone, the capacity, and the venue name of `GET /api/v1/workshops/{id}`.
- `workshop-edit` saves a new title with the loaded version. The detail page shows the title.
- `workshop-stale` refuses a save after another tab saved: the alert `Someone else saved this workshop after you opened it.` shows, and `Load the latest` loads the saved workshop into the form on the same URL.
- `workshop-actions` shows one button for each entry of `allowedActions`. `Schedule` moves the workshop to `Scheduled`.
- `workshop-search` puts the search in the URL (`?search=`) and lists the workshops whose title contains it.

## How to get to it (user POV)

- Examples, Workshops in the sidebar opens `/workshops`. Examples, New venue opens `/venues/new`.
- On `/workshops`, the links `New venue` and `New workshop`, and the title link of each row.
- On a detail page, the group `Actions for <title>` has the `Edit` link and the action buttons.

## Driving it with browse.mjs

Preconditions:

- `doctor.sh` exits with 0.
- The database is empty, so the signed-in user has no Server user. The SPA has no onboarding page. The recipe calls `POST /api/v1/users/initiate` in the page, as the BFF client of the user, with `identifier` from `GET /api/v1/users/me` and `role` = `Admin`. The role `Admin` has each permission. The role `User` has none, so a `User` sees no action buttons.
- On ZeroFactor, `admin@example.com` (`Admin User`), the user that `login()` signs in by default, gets `manage_workshops` this way. `user@example.com` gets it only after the same call with `role` = `Admin`.

- **Onboard.** Run `const { name } = await login()`. Then in the page, `fetch('/api/v1/users/me')` and `fetch('/api/v1/users/initiate', { method: 'POST' })` with the `X-CSRF: 1` header. The answer is 201, or 409 if this stack already has the user.
- **Empty list.** Click `Examples`, then `Workshops`. The heading `Workshops` and the empty text show.
- **Add a venue.** Click `New venue`. Fill `Name`. Fill `Time zone` with `Pacific/Pago_Pago`, a zone that is not the zone of the browser. Click `Add venue`. `POST /api/v1/venues` gives 201. The URL is `/workshops/new?venueId=<id>`, and the combobox `Venue` has the name of the venue.
- **Add a workshop.** Fill `Title`. Click the month segment of the group `Starts at` and type a date 30 days after today at the venue, at 09:30 AM. The `POST` sends `startsAt` as 20:30 UTC of that day. Fill `Capacity`. Click `Add workshop`. `POST /api/v1/workshops` gives 201. The page is the detail page: the heading is the title, and the status is `Draft`.
- **Detail.** The region `Details` shows the start as `toLocaleString(undefined, { month: 'short', day: 'numeric', year: 'numeric', hour: 'numeric', minute: '2-digit', timeZone: 'Pacific/Pago_Pago', timeZoneName: 'short' })` of `startsAt`, for example `Aug 15, 2026, 9:30 AM GMT-11`, the capacity, and the venue name of the same run.
- **Edit.** Click `Edit` in the group `Actions for <title>`. Fill `Title`, click `Save`. `PUT /api/v1/workshops/{id}` gives 200 with a `version` that is larger than the loaded one. The detail page shows the new title.
- **Stale save.** Open the edit page in this tab and in a second tab. Save a title in the second tab. Save another title in the first tab. `PUT` gives 409 with the type `urn:fullstacktemplate:problem:stale-version`. The alert shows, and `Title` keeps the text of the first tab. Click `Load the latest`. The URL stays `/workshops/<id>/edit`, and `Title` has the title of the second tab.
- **Actions.** On the detail page, the buttons equal `allowedActions` of the last `GET` (`Update` is the `Edit` link). Click `Schedule`. `POST /api/v1/workshops/{id}/schedule` gives 200 with the status `Scheduled`. The page shows `Scheduled`, `Cancel workshop`, and `Mark completed`. `Schedule` and `Edit` are gone.
- **Search.** Open `/workshops`, type a word of the title into the searchbox `Search workshops`. The URL gets `?search=<word>`, and `GET /api/v1/workshops` has the same `search`. The row of the workshop shows, with the start in the zone of the venue.
- **Proof.** Run `node $S/scripts/browse.mjs $S/recipes/examples/workshops.mjs`. The captures `empty-list`, `venue-picked`, `detail-draft`, `stale-alert`, `latest-loaded`, `scheduled`, and `search` show the states.

## Gotchas

- Without the onboard step, each workshop request gives 403, because the Server has no user for the token.
- The `initiate` call needs a tenant name and an identifier that no other user has. A second run on the same stack gets 409 for the identifier. The user from the first run is still an `Admin`, so the recipe goes on.
- The page shows the start in the time zone of the venue, not in the zone of the browser. A check that formats `startsAt` with no `timeZone` fails when the two zones differ.
- A `version` is a database value. Check that it changed, not that it went up by one.
- A click on an option of the `Venue` combobox can scroll the page, and a scroll closes the list. Call `settle()` before the click, or pick with ArrowDown and Enter.
- The search box writes to the URL 300 ms after the last key press. Wait for the URL or the response, not for a fixed time.
- A save error shows on the form only. The save sends no toast.
