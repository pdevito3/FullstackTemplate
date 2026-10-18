// Feature: weather-forecast. Anonymous visitor. See features/weather-forecast.md.
export default async ({ page, step, capture, expect }) => {
  const forecast = await step('open / and receive GET /api/v1/weather', async () => {
    const res = page.waitForResponse((r) => r.url().endsWith('/api/v1/weather') && r.request().method() === 'GET')
    await page.goto('/')
    const r = await res
    expect(r.status()).toBe(200)
    return r.json()
  })

  await step('page shows the heading and 5 cards in Fahrenheit', async () => {
    await expect(page.getByRole('heading', { name: 'Weather Forecast', level: 1 })).toBeVisible()
    await expect(page.getByRole('button', { name: 'Login' })).toBeVisible()
    expect(forecast).toHaveLength(5)
    await expect(page.getByText('Fahrenheit', { exact: true })).toHaveCount(5)
  })

  await step('each card shows the summary and temperatureF of the API row', async () => {
    for (const f of forecast) {
      await expect(page.getByText(f.summary, { exact: true }).first()).toBeVisible()
      await expect(page.getByText(`${f.temperatureF}°`, { exact: true }).first()).toBeVisible()
    }
  })
  await capture('fahrenheit')

  await step('choose °C: each card shows temperatureC', async () => {
    await page.getByRole('button', { name: '°C' }).click()
    await expect(page.getByText('Celsius', { exact: true })).toHaveCount(5)
    for (const f of forecast) {
      await expect(page.getByText(`${f.temperatureC}°`, { exact: true }).first()).toBeVisible()
    }
  })
  await capture('celsius')

  await step('choose Refresh: a new GET /api/v1/weather replaces the cards', async () => {
    const res = page.waitForResponse((r) => r.url().endsWith('/api/v1/weather') && r.request().method() === 'GET')
    await page.getByRole('button', { name: 'Refresh' }).click()
    const fresh = await (await res).json()
    await expect(page.getByText('Celsius', { exact: true })).toHaveCount(5)
    for (const f of fresh) {
      await expect(page.getByText(`${f.temperatureC}°`, { exact: true }).first()).toBeVisible()
    }
  })
  await capture('refreshed')
}
