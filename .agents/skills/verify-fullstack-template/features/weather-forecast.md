# Weather forecast

The home page shows a forecast of five days from the public weather API. Each card shows a date, a summary, and a temperature. The user can change the unit between Fahrenheit and Celsius, and can get new data with Refresh. No login is necessary.

## Sub-features

- `weather-list` shows five cards from `GET /api/v1/weather`, each with a date, a summary, and a temperature.
- `weather-unit` changes all cards between °F (default) and °C. The choice is in `localStorage` key `weather-temperature-unit`, so a reload keeps it.
- `weather-refresh` sends a new request and shows the new data.
- `weather-states` shows skeleton cards while the data loads, an error box if the request fails, and "No weather data available" with a `Try Again` button for an empty list.

## How to get to it (user POV)

- Open the app root `/`.
- Choose `Home` in the sidebar, or the `FS Fullstack Template` logo link.

## Driving it with browse.mjs

Preconditions:

- `doctor.sh` exits with 0.
- Nobody is logged in.

- **Open the page.** Open `/` and wait for the API response. Run `page.waitForResponse(r => r.url().endsWith('/api/v1/weather'))`, then `page.goto('/')`. The response is 200 with five rows. The heading `Weather Forecast` (level 1) and the button `Login` are visible.
- **Match the data.** For each API row, find its values on the screen. Run `page.getByText(row.summary, { exact: true })` and `page.getByText(`${row.temperatureF}°`, { exact: true })`. Each is visible. `getByText('Fahrenheit', { exact: true })` has count 5.
- **Change to Celsius.** Choose `°C`. Run `page.getByRole('button', { name: '°C' }).click()`. `getByText('Celsius', { exact: true })` has count 5, and each `${row.temperatureC}°` is visible.
- **Reload.** After you choose `°C`, run `page.reload()` after `waitForResponse`. `getByText('Celsius', { exact: true })` has count 5, each new `${row.temperatureC}°` is visible, and `page.evaluate(() => localStorage.getItem('weather-temperature-unit'))` is `'celsius'`.
- **Refresh.** Choose `Refresh`. Run `page.getByRole('button', { name: 'Refresh' }).click()` after `waitForResponse`. A new `GET /api/v1/weather` is 200. The cards show its values, and the unit stays Celsius.
- **Proof.** Run `node $S/scripts/browse.mjs $S/recipes/weather-forecast.mjs`. The captures `fahrenheit`, `celsius`, `reloaded`, and `refreshed` show the cards for each state.

## Gotchas

- The data is random at each request. Do not assert a fixed summary or temperature. Compare with the response of the same run.
- Two cards can have the same summary or temperature. Use `.first()` for each value, and count the unit labels for the card count.
- The unit is in `localStorage` key `weather-temperature-unit`. A reload keeps it. A fresh browser profile starts in °F.
- The date on the card uses the browser locale (`Mon, Oct 19` in en-US). The API sends `2026-10-19`.
- `Refresh` is disabled while a request runs. Wait for the response before you choose it again.
- The error and empty states need a failed or empty API. The recipe does not cover them. Do not report them as verified.
