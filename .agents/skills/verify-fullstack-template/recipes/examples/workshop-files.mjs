import { readFileSync } from 'node:fs'

// A real PNG, so the browser decodes it, and a small PDF with known bytes, so the download can be compared
const png = readFileSync(new URL('../../../../../frontend/public/Aspire.png', import.meta.url))
const pdf = Buffer.from(
  '%PDF-1.4\n1 0 obj<</Type/Catalog/Pages 2 0 R>>endobj\n2 0 obj<</Type/Pages/Count 0/Kids[]>>endobj\n' +
    'trailer<</Root 1 0 R>>\n%%EOF\n',
)
const tooLarge = Buffer.alloc(11 * 1024 * 1024, 'a')
// Over the 30 MB request limit of Kestrel, so the BFF refuses it before the Server reads the file
const overTheBodyLimit = Buffer.alloc(31 * 1024 * 1024, 'a')

export default async ({ page, step, capture, settle, expect, login, stack }) => {
  const stamp = Date.now().toString(36)
  const answered = (method, pattern) =>
    page.waitForResponse((r) => r.request().method() === method && pattern.test(new URL(r.url()).pathname))
  const api = (method, path, body) =>
    page.evaluate(
      async ([method, path, body]) => {
        const res = await fetch(path, {
          method,
          headers: { 'X-CSRF': '1', ...(body ? { 'Content-Type': 'application/json' } : {}) },
          body: body ? JSON.stringify(body) : undefined,
        })
        return { status: res.status, body: res.status === 204 ? null : await res.json() }
      },
      [method, path, body],
    )
  const files = page.getByRole('region', { name: 'Files' })
  const row = (fileName) => files.getByRole('list', { name: 'Files' }).getByRole('listitem', { name: fileName, exact: true })
  const upload = async (file) => {
    const chooser = page.waitForEvent('filechooser')
    const posted = answered('POST', /^\/api\/v1\/workshops\/[^/]+\/attachments$/)
    await files.getByRole('button', { name: 'Upload file' }).click()
    await (await chooser).setFiles(file)
    return posted
  }
  const naturalWidth = (image) =>
    image.evaluate(async (node) => {
      await node.decode()
      return node.naturalWidth
    })

  await page.goto('/')
  await login()

  const tenant = await step('start a tenant on the Start a tenant page, as its admin', async () => {
    await page.getByRole('region', { name: 'No tenant' }).getByRole('link', { name: 'Start a tenant' }).click()
    const form = page.getByRole('form', { name: 'Start a tenant' })
    await form.getByLabel('Tenant name', { exact: true }).fill(`Files ${stamp}`)
    const started = answered('POST', /^\/api\/v1\/users\/initiate$/)
    await form.getByRole('button', { name: 'Start a tenant' }).click()
    const res = await started
    expect(res.status()).toBe(201)
    return (await res.json()).tenant
  })

  try {
    // The workshops recipe proves the forms, so this recipe adds its venue and workshop through the API
    const workshop = await step('add a venue and a workshop through the API', async () => {
      const venue = await api('POST', '/api/v1/venues', { name: `Studio ${stamp}`, timeZone: 'America/Chicago' })
      expect(venue.status).toBe(201)
      const added = await api('POST', '/api/v1/workshops', {
        venueId: venue.body.id,
        title: `Glazing ${stamp}`,
        startsAt: new Date(Date.now() + 30 * 24 * 60 * 60 * 1000).toISOString(),
        capacity: 12,
      })
      expect(added.status).toBe(201)
      expect(added.body.allowedActions).toContain('ManageAttachments')
      return added.body
    })

    await step('the detail page shows the Files section with no files', async () => {
      const listed = answered('GET', new RegExp(`^/api/v1/workshops/${workshop.id}/attachments$`))
      await page.goto(`/workshops/${workshop.id}`)
      expect((await listed).status()).toBe(200)
      await expect(files.getByText('This workshop has no files.', { exact: true })).toBeVisible()
    })
    await capture('files-empty')

    const image = await step('upload a PNG: the row shows a thumbnail that the browser loads from storage', async () => {
      const res = await upload({ name: 'kiln.png', mimeType: 'image/png', buffer: png })
      expect(res.status()).toBe(201)
      const body = await res.json()
      expect(body.contentType).toBe('image/png')
      expect(body.sizeBytes).toBe(png.length)
      await expect(page.getByRole('alertdialog').filter({ hasText: 'File uploaded' })).toBeVisible()
      const thumbnail = row(body.fileName).getByRole('button', { name: `View ${body.fileName}` }).getByRole('img')
      await expect(thumbnail).toBeVisible()
      expect(await naturalWidth(thumbnail)).toBeGreaterThan(0)
      const src = new URL(await thumbnail.getAttribute('src'))
      // The browser reads the bytes from storage with a signed URL, not through the BFF
      expect(src.origin).not.toBe(new URL(page.url()).origin)
      expect(src.searchParams.get('X-Amz-Signature')).toBeTruthy()
      expect(src.searchParams.get('response-content-disposition')).toMatch(/^inline;/)
      return body
    })

    const handout = await step('upload a PDF: the row shows a file icon and no thumbnail', async () => {
      const res = await upload({ name: 'handout.pdf', mimeType: 'application/pdf', buffer: pdf })
      expect(res.status()).toBe(201)
      const body = await res.json()
      expect(body.previewUrl).toBeNull()
      await expect(row(body.fileName)).toBeVisible()
      await expect(row(body.fileName).getByRole('button', { name: `View ${body.fileName}` })).toHaveCount(0)
      await expect(row(body.fileName).getByRole('link', { name: `Download ${body.fileName}` })).toHaveAttribute(
        'href',
        body.downloadUrl,
      )
      return body
    })
    await capture('two-files')

    await step('a file over 10 MB is refused with the message of the Server', async () => {
      const res = await upload({ name: 'kiln-video.mp4', mimeType: 'video/mp4', buffer: tooLarge })
      expect(res.status()).toBe(422)
      const body = await res.json()
      expect(body.detail).toBe('A file can be no larger than 10 MB. Choose a smaller file.')
      await expect(page.getByRole('alertdialog').filter({ hasText: body.detail })).toBeVisible()
      await expect(files.getByRole('list', { name: 'Files' }).getByRole('listitem')).toHaveCount(2)
    })
    await capture('too-large')

    // The BFF closes the connection after its 413. In the dev build, the Vite proxy then answers about 1 upload in 6
    // with its own 500, so only the production build, where the browser talks to the BFF, proves this step.
    if (stack.build === 'production')
      await step('a file over the request limit of 30 MB is refused with the problem of the BFF', async () => {
        const res = await upload({ name: 'kiln-film.mp4', mimeType: 'video/mp4', buffer: overTheBodyLimit })
        expect(res.status()).toBe(413)
        // Chromium does not keep the body of this response, so the toast proves the detail. The sweep rewrites the
        // headers of each API answer, so the recipe does not check the content type.
        await expect(page.getByRole('alertdialog').filter({ hasText: 'The file is too large to send. Choose a smaller file.' })).toBeVisible()
        await expect(files.getByRole('list', { name: 'Files' }).getByRole('listitem')).toHaveCount(2)
      })

    await step('View opens the full image in a dialog', async () => {
      await row(image.fileName).getByRole('button', { name: `View ${image.fileName}` }).click()
      const dialog = page.getByRole('dialog', { name: image.fileName })
      await expect(dialog).toBeVisible()
      await settle()
      expect(await naturalWidth(dialog.getByRole('img', { name: image.fileName }))).toBeGreaterThan(0)
    })
    await capture('image-dialog', { fullPage: false })

    await step('Escape closes the dialog and puts the focus back on View', async () => {
      await page.keyboard.press('Escape')
      await expect(page.getByRole('dialog', { name: image.fileName })).toBeHidden()
      await expect(row(image.fileName).getByRole('button', { name: `View ${image.fileName}` })).toBeFocused()
    })

    await step('Download saves the PDF under its name with the bytes that were uploaded', async () => {
      const download = page.waitForEvent('download')
      await row(handout.fileName).getByRole('link', { name: `Download ${handout.fileName}` }).click()
      const saved = await download
      expect(saved.suggestedFilename()).toBe('handout.pdf')
      expect(Buffer.compare(readFileSync(await saved.path()), pdf)).toBe(0)
    })

    await step('Delete asks first, then removes the PDF and moves the focus to the heading', async () => {
      await row(handout.fileName).getByRole('button', { name: `Delete ${handout.fileName}` }).click()
      const dialog = page.getByRole('dialog', { name: `Delete ${handout.fileName}?` })
      await expect(dialog).toBeVisible()
      await settle()
      await capture('delete-dialog', { fullPage: false })
      const deleted = answered('DELETE', new RegExp(`^/api/v1/workshops/${workshop.id}/attachments/${handout.id}$`))
      // The page loads the list again after the delete. Wait for that answer, not for a time, on a slow machine
      const listed = answered('GET', new RegExp(`^/api/v1/workshops/${workshop.id}/attachments$`))
      await dialog.getByRole('button', { name: 'Delete file' }).click()
      expect((await deleted).status()).toBe(204)
      expect((await listed).status()).toBe(200)
      await expect(row(handout.fileName)).toHaveCount(0)
      await expect(page.getByRole('alertdialog').filter({ hasText: 'File deleted' })).toBeVisible()
      await expect(files.getByRole('heading', { name: 'Files' })).toBeFocused()
    })

    await step('storage no longer serves the deleted PDF', async () => {
      const res = await page.request.get(handout.downloadUrl)
      expect(res.status()).toBe(404)
    })

    await step('a reload shows the PNG and not the PDF', async () => {
      const listed = answered('GET', new RegExp(`^/api/v1/workshops/${workshop.id}/attachments$`))
      await page.reload()
      const res = await listed
      const body = await res.json()
      expect(body.map((a) => a.id)).toEqual([image.id])
      const thumbnail = row(image.fileName).getByRole('button', { name: `View ${image.fileName}` }).getByRole('img')
      expect(await naturalWidth(thumbnail)).toBeGreaterThan(0)
      await expect(row(handout.fileName)).toHaveCount(0)
    })
    await capture('after-reload')

    // The restore of browse.mjs puts back the rows but not the objects, so the recipe deletes the file that it kept
    await step('clean up: DELETE the PNG', async () => {
      const res = await api('DELETE', `/api/v1/workshops/${workshop.id}/attachments/${image.id}`)
      expect(res.status).toBe(204)
    })
  } finally {
    await step('clean up: DELETE the tenant', async () => {
      const res = await api('DELETE', `/api/v1/tenants/${tenant.id}`)
      expect(res.status).toBe(204)
    })
  }
}
