// Feature: secure-weather. The JWT that the BFF holds reaches the server. See features/secure-weather.md.
export default async ({ page, step, capture, expect, login }) => {
  await step('anonymous: no API mode button, GET /api/v1/weather/secure is 401', async () => {
    await page.goto('/')
    await expect(page.getByRole('button', { name: 'Login' })).toBeVisible()
    await expect(page.getByRole('button', { name: 'Public API' })).toHaveCount(0)
    const status = await page.evaluate(async () => (await fetch('/api/v1/weather/secure', { headers: { 'X-CSRF': '1' } })).status)
    expect(status).toBe(401)
  })

  const { name } = await step('log in', () => login())
  // The greeting also shows the name, so count only the badges on the cards.
  const userBadges = page.locator('[data-slot="badge"]').filter({ hasText: name })
  await capture('public-mode')

  const forecast = await step('choose Public API: it becomes Secure API and GET /api/v1/weather/secure is 200', async () => {
    const res = page.waitForResponse((r) => r.url().endsWith('/api/v1/weather/secure'))
    await page.getByRole('button', { name: 'Public API' }).click()
    const r = await res
    expect(r.status()).toBe(200)
    await expect(page.getByRole('button', { name: 'Secure API' })).toBeVisible()
    return r.json()
  })

  await step('each card shows the requestedBy badge from the server', async () => {
    expect(forecast).toHaveLength(5)
    for (const f of forecast) expect(f.requestedBy).toBe(name)
    await expect(page.getByText('Showing authenticated data')).toBeVisible()
    await expect(userBadges).toHaveCount(5)
    for (const f of forecast) {
      await expect(page.getByText(`${f.temperatureF}°`, { exact: true }).first()).toBeVisible()
    }
  })
  await capture('secure-mode')

  await step('choose Secure API: it goes back to Public API without badges', async () => {
    await page.getByRole('button', { name: 'Secure API' }).click()
    await expect(page.getByRole('button', { name: 'Public API' })).toBeVisible()
    await expect(page.getByText('Showing authenticated data')).toHaveCount(0)
    await expect(userBadges).toHaveCount(0)
  })
}
