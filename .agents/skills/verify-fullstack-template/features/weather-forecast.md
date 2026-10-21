# Weather forecast

The home page shows a forecast of five days by default from the public weather API. The user can choose 1 to 14 days with the Days picker. Each card shows a date, a summary, and a temperature. The user can change the unit between Fahrenheit and Celsius, and can get new data with Refresh. No login is necessary.

## Sub-features

- `weather-list` shows five cards from `GET /api/v1/weather?days=5`, each with a date, a summary, and a temperature.
- `weather-unit` changes all cards between °F (default) and °C.
- `weather-days` changes the number of cards with the Days picker. The count is the `days` search param of `/`, from 1 to 14, and a bad value gives 5. The picker is hidden in the Secure API view, which always shows five days.
- `weather-refresh` sends a new request and shows the new data.
- `weather-states` shows skeleton cards while the data loads, an error box if the request fails, and "No weather data available" with a `Try Again` button for an empty list.

## How to get to it (user POV)

- Open the app root `/`.
- Choose `Home` in the sidebar, or the `FS Fullstack Template` logo link.

## Driving it with browse.mjs

Preconditions:

- `doctor.sh` exits with 0.
- Nobody is logged in.

- **Open the page.** Open `/` and wait for the API response. Run `page.waitForResponse(r => new URL(r.url()).pathname === '/api/v1/weather')`, then `page.goto('/')`. The response is 200 with five rows. The heading `Weather Forecast` (level 1) and the button `Login` are visible.
- **Match the data.** For each API row, find its values on the screen. Run `page.getByText(row.summary, { exact: true })` and `page.getByText(`${row.temperatureF}°`, { exact: true })`. Each is visible. `getByText('Fahrenheit', { exact: true })` has count 5.
- **Change to Celsius.** Choose `°C`. Run `page.getByRole('button', { name: '°C' }).click()`. `getByText('Celsius', { exact: true })` has count 5, and each `${row.temperatureC}°` is visible.
- **Refresh.** Choose `Refresh`. Run `page.getByRole('button', { name: 'Refresh' }).click()` after `waitForResponse`. A new `GET /api/v1/weather` is 200. The cards show its values, and the unit stays Celsius.
- **Change the days.** Open the picker, choose a count, and read the URL and the cards. Run `page.getByRole('button', { name: /Days/ }).click()` and `page.getByRole('option', { name: '3 days', exact: true }).click()` with `waitForResponse` in `Promise.all`. The names need `exact` because `3 days` is also a part of `13 days`. A new `GET /api/v1/weather?days=3` is 200 with three rows, the cards show its summaries and temperatures, and the URL is `/?days=3`. Do the same for `14 days`. After `page.reload()` the URL and the 14 cards stay. Choose `5 days` and the URL is `/` again. Open `/?days=99` and there are 5 cards and the URL is `/`.
- **Proof.** Run `node $S/scripts/browse.mjs $S/recipes/weather-forecast.mjs`. The captures `fahrenheit`, `celsius`, `refreshed`, `days-open`, `three-days`, `fourteen-days`, `after-reload`, and `five-days-again` show the page for each state.

## Gotchas

- The data is random at each request. Do not assert a fixed summary or temperature. Compare with the response of the same run.
- Two cards can have the same summary or temperature. Use `.first()` for each value, and count the unit labels for the card count.
- The number of days is in the URL, so a reload keeps it. The default of 5 is not in the URL: `/` stays `/`. The request URL has a `days` query, so match the pathname and not the end of the URL.
- The unit is local React state. A reload sets it back to °F.
- The date on the card uses the browser locale (`Mon, Oct 19` in en-US). The API sends `2026-10-19`.
- `Refresh` is disabled while a request runs. Wait for the response before you choose it again.
- The error and empty states need a failed or empty API. The recipe does not cover them. Do not report them as verified.
