// Feature: weather-forecast. Anonymous visitor. See features/weather-forecast.md.
export default async ({ page, step, capture, settle, expect }) => {
  const isForecast = (r) => new URL(r.url()).pathname === '/api/v1/weather' && r.request().method() === 'GET'
  const unitKey = { Fahrenheit: 'temperatureF', Celsius: 'temperatureC' }
  const expectCards = async (rows, unit) => {
    await expect(page.getByText(unit, { exact: true })).toHaveCount(rows.length)
    for (const f of rows) {
      await expect(page.getByText(f.summary, { exact: true }).first()).toBeVisible()
      await expect(page.getByText(`${f[unitKey[unit]]}°`, { exact: true }).first()).toBeVisible()
    }
  }
  const daysLabel = (n) => `${n} ${n === 1 ? 'day' : 'days'}`
  const isDays = (n) => (r) => isForecast(r) && new URL(r.url()).searchParams.get('days') === String(n)
  const openPicker = async () => {
    await page.getByRole('button', { name: /Days/ }).click()
    await expect(page.getByRole('option', { name: daysLabel(5), exact: true })).toBeVisible()
    await settle()
  }
  const pickDays = async (n) => {
    const [res] = await Promise.all([
      page.waitForResponse(isDays(n)),
      page.getByRole('option', { name: daysLabel(n), exact: true }).click(),
    ])
    expect(res.status()).toBe(200)
    return res.json()
  }
  const isHome = (search) => (u) => u.pathname === '/' && u.search === search

  const forecast = await step('open / and receive GET /api/v1/weather', async () => {
    const [r] = await Promise.all([page.waitForResponse(isForecast), page.goto('/')])
    expect(r.status()).toBe(200)
    await expect(page).toHaveURL((u) => u.pathname === '/' && u.search === '')
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
    const [res] = await Promise.all([page.waitForResponse(isForecast), page.getByRole('button', { name: 'Refresh' }).click()])
    const fresh = await res.json()
    await expect(page.getByText('Celsius', { exact: true })).toHaveCount(5)
    for (const f of fresh) {
      await expect(page.getByText(`${f.temperatureC}°`, { exact: true }).first()).toBeVisible()
    }
  })
  await capture('refreshed')

  await step('open the Days picker: the list shows 1 to 14 days', async () => {
    await openPicker()
    await expect(page.getByRole('option')).toHaveCount(14)
  })
  await capture('days-open', { fullPage: false })

  await step('choose 3 days: GET days=3 returns 3 rows, the cards match them, the URL is /?days=3', async () => {
    const rows = await pickDays(3)
    expect(rows).toHaveLength(3)
    await expectCards(rows, 'Celsius')
    await expect(page).toHaveURL(isHome('?days=3'))
  })
  await capture('three-days')

  await step('choose 14 days: GET days=14 returns 14 rows, the cards match them, the URL is /?days=14', async () => {
    await openPicker()
    const rows = await pickDays(14)
    expect(rows).toHaveLength(14)
    await expectCards(rows, 'Celsius')
    await expect(page).toHaveURL(isHome('?days=14'))
  })
  await capture('fourteen-days')

  await step('reload keeps 14 days: the new GET days=14 matches the cards in Fahrenheit', async () => {
    const [res] = await Promise.all([page.waitForResponse(isDays(14)), page.reload()])
    const rows = await res.json()
    expect(rows).toHaveLength(14)
    await expectCards(rows, 'Fahrenheit')
    await expect(page).toHaveURL(isHome('?days=14'))
  })
  await capture('after-reload')

  await step('choose 5 days again: 5 cards match the rows and the URL is / without days', async () => {
    await openPicker()
    const rows = await pickDays(5)
    expect(rows).toHaveLength(5)
    await expectCards(rows, 'Fahrenheit')
    await expect(page).toHaveURL(isHome(''))
  })
  await capture('five-days-again')

  await step('a bad days value falls back to 5 cards and the URL loses it', async () => {
    await page.goto('/?days=99')
    await expect(page.getByText('Fahrenheit', { exact: true })).toHaveCount(5)
    await expect(page).toHaveURL(isHome(''))
  })
}
