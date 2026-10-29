export default async ({ page, context, step, capture, settle, expect, login }) => {
  const stamp = Date.now().toString(36)
  const venueName = `Hall ${stamp}`
  const title = `Kubb ${stamp}`
  const answered = (method, pattern) =>
    page.waitForResponse((r) => r.request().method() === method && pattern.test(new URL(r.url()).pathname))
  const actions = (name) => page.getByRole('group', { name: `Actions for ${name}` })
  const zone = 'Pacific/Pago_Pago'
  const [year, month, day] = new Intl.DateTimeFormat('en-CA', { timeZone: zone })
    .format(Date.now() + 30 * 24 * 60 * 60 * 1000)
    .split('-')
  const startsAt = new Date(Date.UTC(year, month - 1, day, 20, 30)).toISOString()
  const shownStart = () =>
    page.evaluate(
      ([iso, timeZone]) =>
        new Date(iso).toLocaleString(undefined, {
          month: 'short',
          day: 'numeric',
          year: 'numeric',
          hour: 'numeric',
          minute: '2-digit',
          timeZone,
          timeZoneName: 'short',
        }),
      [startsAt, zone],
    )

  await step('log in and start a tenant on the Start a tenant page, as its admin', async () => {
    await page.goto('/')
    await login()
    await page.getByRole('region', { name: 'No tenant' }).getByRole('link', { name: 'Start a tenant' }).click()
    const form = page.getByRole('form', { name: 'Start a tenant' })
    await form.getByLabel('Tenant name', { exact: true }).fill(`Verify ${stamp}`)
    const started = answered('POST', /^\/api\/v1\/users\/initiate$/)
    await form.getByRole('button', { name: 'Start tenant' }).click()
    const res = await started
    expect(res.status()).toBe(201)
    expect((await res.json()).user.role).toBe('Admin')
  })

  await step('Examples, Workshops: the list opens', async () => {
    const list = answered('GET', /^\/api\/v1\/workshops$/)
    await page.getByRole('button', { name: 'Examples' }).click()
    await page.getByRole('link', { name: 'Workshops', exact: true }).click()
    expect((await list).status()).toBe(200)
    await expect(page.getByRole('heading', { name: 'Workshops', exact: true })).toBeVisible()
  })
  await capture('empty-list')

  const venue = await step('New venue: the venue is added and picked on the new workshop form', async () => {
    await page.getByRole('main').getByRole('link', { name: 'New venue' }).click()
    await page.getByRole('textbox', { name: 'Name' }).fill(venueName)
    await page.getByRole('textbox', { name: 'Time zone' }).fill(zone)
    const added = answered('POST', /^\/api\/v1\/venues$/)
    await page.getByRole('button', { name: 'Add venue' }).click()
    const res = await added
    expect(res.status()).toBe(201)
    const body = await res.json()
    expect(body.timeZone).toBe(zone)
    await expect(page).toHaveURL(new RegExp(`/workshops/new\\?venueId=${body.id}$`))
    await expect(page.getByRole('combobox', { name: 'Venue' })).toHaveValue(body.name)
    return body
  })
  await capture('venue-picked')

  let workshop = await step('Add workshop: the start is 09:30 at the venue, and the detail page shows the draft', async () => {
    await page.getByRole('textbox', { name: 'Title' }).fill(title)
    await page.getByRole('group', { name: 'Starts at' }).getByRole('spinbutton').first().click()
    await page.keyboard.type(`${month}${day}${year}0930`)
    await page.keyboard.type('A')
    await page.getByRole('spinbutton', { name: 'Capacity' }).fill('12')
    const added = answered('POST', /^\/api\/v1\/workshops$/)
    await page.getByRole('button', { name: 'Add workshop' }).click()
    const res = await added
    expect(res.status()).toBe(201)
    expect(new Date(res.request().postDataJSON().startsAt).toISOString()).toBe(startsAt)
    const body = await res.json()
    expect(body.venueTimeZone).toBe(zone)
    await expect(page).toHaveURL(new RegExp(`/workshops/${body.id}$`))
    await expect(page.getByRole('heading', { name: body.title })).toBeVisible()
    await expect(page.getByText('Draft', { exact: true })).toBeVisible()
    return body
  })

  await step('detail: the start in the zone of the venue, the capacity, and the venue of the API', async () => {
    const details = page.getByRole('region', { name: 'Details' })
    await expect(details).toContainText(await shownStart())
    await expect(details).toContainText(String(workshop.capacity))
    await expect(details).toContainText(venue.name)
    expect(workshop.allowedActions).toEqual(['Update', 'Schedule', 'Cancel'])
  })
  await capture('detail-draft')

  workshop = await step('Edit, Save: the PUT sends the loaded version and the detail shows the title', async () => {
    await actions(workshop.title).getByRole('link', { name: 'Edit' }).click()
    await page.getByRole('textbox', { name: 'Title' }).fill(`${title} edited`)
    const saved = answered('PUT', /^\/api\/v1\/workshops\/[^/]+$/)
    await page.getByRole('button', { name: 'Save' }).click()
    const res = await saved
    expect(res.status()).toBe(200)
    expect(res.request().postDataJSON().version).toBe(workshop.version)
    const body = await res.json()
    expect(body.version).toBeGreaterThan(workshop.version)
    await expect(page.getByRole('heading', { name: body.title })).toBeVisible()
    return body
  })

  const otherTab = await step('a save after another tab saved: 409, the alert, and the text stays', async () => {
    await actions(workshop.title).getByRole('link', { name: 'Edit' }).click()
    await expect(page.getByRole('textbox', { name: 'Title' })).toHaveValue(workshop.title)

    const other = await context.newPage()
    await other.goto(`/workshops/${workshop.id}/edit`)
    await other.getByRole('textbox', { name: 'Title' }).fill(`${title} from the other tab`)
    const otherSave = other.waitForResponse((r) => r.request().method() === 'PUT')
    await other.getByRole('button', { name: 'Save' }).click()
    const saved = await otherSave
    expect(saved.status()).toBe(200)
    const savedBody = await saved.json()
    await other.close()

    await page.getByRole('textbox', { name: 'Title' }).fill(`${title} from this tab`)
    const refused = answered('PUT', /^\/api\/v1\/workshops\/[^/]+$/)
    await page.getByRole('button', { name: 'Save' }).click()
    const res = await refused
    expect(res.status()).toBe(409)
    expect((await res.json()).type).toBe('urn:fullstacktemplate:problem:stale-version')
    await expect(page.getByRole('alert').filter({ hasText: 'Someone else saved this workshop after you opened it.' })).toBeVisible()
    await expect(page.getByRole('textbox', { name: 'Title' })).toHaveValue(`${title} from this tab`)
    return savedBody
  })
  await capture('stale-alert')

  await step('Load the latest: the form has the title of the other tab, on the same URL', async () => {
    const loaded = answered('GET', /^\/api\/v1\/workshops\/[^/]+$/)
    await page.getByRole('button', { name: 'Load the latest' }).click()
    expect((await loaded).status()).toBe(200)
    await expect(page).toHaveURL(new RegExp(`/workshops/${workshop.id}/edit$`))
    await expect(page.getByRole('textbox', { name: 'Title' })).toHaveValue(otherTab.title)
    await expect(page.getByRole('alert').filter({ hasText: 'Someone else saved' })).toHaveCount(0)
  })
  await capture('latest-loaded')

  await step('Schedule: the status is Scheduled, and the buttons are the allowed actions', async () => {
    const opened = answered('GET', /^\/api\/v1\/workshops\/[^/]+$/)
    await page.getByRole('link', { name: 'Back to the workshop' }).click()
    const current = await (await opened).json()
    expect(current.allowedActions).toContain('Schedule')
    const scheduled = answered('POST', /\/schedule$/)
    await actions(otherTab.title).getByRole('button', { name: 'Schedule' }).click()
    const res = await scheduled
    expect(res.status()).toBe(200)
    const body = await res.json()
    expect(body.status).toBe('Scheduled')
    expect(body.allowedActions).toEqual(['Cancel', 'Complete'])
    await expect(page.getByText('Scheduled', { exact: true })).toBeVisible()
    await expect(actions(otherTab.title).getByRole('button', { name: 'Cancel workshop' })).toBeVisible()
    await expect(actions(otherTab.title).getByRole('button', { name: 'Mark completed' })).toBeVisible()
    await expect(actions(otherTab.title).getByRole('button', { name: 'Schedule' })).toHaveCount(0)
    await expect(actions(otherTab.title).getByRole('link', { name: 'Edit' })).toHaveCount(0)
  })
  await capture('scheduled')

  await step('Cancel workshop asks first: Keep workshop sends nothing, and the focus goes back to the button', async () => {
    const cancelButton = actions(otherTab.title).getByRole('button', { name: 'Cancel workshop' })
    const dialog = page.getByRole('dialog', { name: 'Cancel this workshop?' })
    let sent = 0
    const count = (r) => r.method() === 'POST' && /\/cancel$/.test(new URL(r.url()).pathname) && sent++
    page.on('request', count)
    await cancelButton.click()
    await expect(dialog).toBeVisible()
    await capture('cancel-dialog')
    await dialog.getByRole('button', { name: 'Keep workshop' }).click()
    await expect(dialog).toBeHidden()
    await expect(cancelButton).toBeFocused()
    page.off('request', count)
    expect(sent).toBe(0)
  })

  await step('search: the URL and the request have the search, and the row shows', async () => {
    await page.goto('/workshops')
    await settle()
    const searched = page.waitForResponse(
      (r) => new URL(r.url()).pathname === '/api/v1/workshops' && new URL(r.url()).searchParams.get('search') === stamp,
    )
    await page.getByRole('searchbox', { name: 'Search workshops' }).fill(stamp)
    expect((await searched).status()).toBe(200)
    await expect(page).toHaveURL(new RegExp(`/workshops\\?search=${stamp}$`))
    await expect(page.getByRole('link', { name: otherTab.title })).toBeVisible()
    await expect(page.getByRole('row').filter({ hasText: otherTab.title })).toContainText(await shownStart())
  })
  await capture('search')
}
