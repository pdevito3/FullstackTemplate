import { as } from '@tests/support/auth/actors'
import { aProblem } from '@tests/support/builders/problem-details-builder'
import { aVenue } from '@tests/support/examples/venue-builder'
import { aWorkshopAttachment } from '@tests/support/examples/workshop-attachment-builder'
import { aWorkshop } from '@tests/support/examples/workshop-builder'
import { workshopPaths, workshopsApi } from '@tests/support/examples/workshops-api'
import { expect, test } from '@tests/support/fixtures'
import { workshopsPage } from './workshops.tasks'

test.describe('the files of a workshop', () => {
  test.use({ actor: as.user('Ana Admin') })

  const venue = aVenue().named('Main Hall')
  const workshop = aWorkshop().titled('Glazing basics').at(venue)
  const { id, venueId } = workshop.build()
  const photo = aWorkshopAttachment().named('kiln.png', 'image/png').ofSize(1_572_864)
  const handout = aWorkshopAttachment().named('handout.pdf', 'application/pdf').ofSize(24_576)

  test('shows an image as a thumbnail that opens the full image, and another file as an icon with a download link', async ({ page, api }) => {
    api.mock(workshopsApi.oneIs(workshop), workshopsApi.venueIs(venue), workshopsApi.attachmentsReturn(workshop, [photo, handout]))
    const workshops = workshopsPage(page)

    await workshops.openDetailWithFiles(id, venueId)

    await expect(workshops.at.fileRow('kiln.png')).toContainText('1.5 MB')
    await expect(workshops.at.fileRow('handout.pdf')).toContainText('24 kB')
    expect(await workshops.imageLoaded('kiln.png')).toBeGreaterThan(0)
    await expect(workshops.at.viewImage('handout.pdf')).toHaveCount(0)
    await expect(workshops.at.downloadLink('handout.pdf')).toHaveAttribute('href', handout.build().downloadUrl)

    await workshops.viewImage('kiln.png')

    await expect(workshops.at.fullImage('kiln.png')).toHaveAttribute('src', photo.build().previewUrl!)
  })

  test('keeps the full image open when the list loads again with new URLs before the old ones expire', async ({ page, api }) => {
    const firstSigning = photo.signedUntil('2026-05-20T15:15:00Z')
    const secondSigning = photo.signedUntil('2026-05-20T15:30:00Z')
    api.mock(workshopsApi.oneIs(workshop), workshopsApi.venueIs(venue), workshopsApi.attachmentsReturn(workshop, [firstSigning]))
    const workshops = workshopsPage(page)
    await workshops.startTheClockAt(new Date('2026-05-20T15:00:00Z'))
    await workshops.openDetailWithFiles(id, venueId)
    await workshops.viewImage('kiln.png')
    api.mock(workshopsApi.attachmentsReturn(workshop, [secondSigning]))

    await workshops.letTimePassUntilTheFilesLoadAgain(id, 14 * 60_000)

    await expect(workshops.at.imageDialog('kiln.png')).toBeVisible()
    await expect(workshops.at.fullImage('kiln.png')).toHaveAttribute('src', secondSigning.build().previewUrl!)
  })

  test('uploads a file as a multipart form and shows it in the list', async ({ page, api }) => {
    api.mock(workshopsApi.oneIs(workshop), workshopsApi.venueIs(venue), workshopsApi.attachmentsReturn(workshop))
    const workshops = workshopsPage(page)
    await workshops.openDetailWithFiles(id, venueId)
    await expect(workshops.at.noFiles).toBeVisible()
    const notes = aWorkshopAttachment().named('notes.txt', 'text/plain')
    api.mock(workshopsApi.attachmentUploads(workshop, notes), workshopsApi.attachmentsReturn(workshop, [notes]))

    await workshops.upload(id, { name: 'notes.txt', mimeType: 'text/plain', buffer: Buffer.from('Bring an apron.') })

    await expect(workshops.at.fileRow('notes.txt')).toBeVisible()
    await expect(workshops.at.toast('File uploaded')).toBeVisible()
    const upload = api.last('post', workshopPaths.attachments(id))
    expect(upload?.headers.get('content-type')).toMatch(/^multipart\/form-data; boundary=/)
    expect(upload?.body).toContain('name="file"; filename="notes.txt"')
    expect(upload?.body).toContain('Bring an apron.')
  })

  test('shows the refusal of the Server when a file is too large', async ({ page, api }) => {
    const tooLarge = 'A file can be no larger than 10 MB. Choose a smaller file.'
    api.mock(
      workshopsApi.oneIs(workshop),
      workshopsApi.venueIs(venue),
      workshopsApi.attachmentsReturn(workshop),
      workshopsApi.attachmentUploadFails(workshop, aProblem().invalid({ sizeBytes: [tooLarge] }).saying(tooLarge)),
    )
    const workshops = workshopsPage(page)
    await workshops.openDetailWithFiles(id, venueId)

    await workshops.uploadRefused(id, { name: 'video.mp4', mimeType: 'video/mp4', buffer: Buffer.from('frames') })

    await expect(workshops.at.toast(tooLarge)).toBeVisible()
    await expect(workshops.at.noFiles).toBeVisible()
    await expect(workshops.at.uploadFile).toBeEnabled()
  })

  test('deletes a file after the user confirms, and moves the focus to the heading', async ({ page, api }) => {
    api.mock(workshopsApi.oneIs(workshop), workshopsApi.venueIs(venue), workshopsApi.attachmentsReturn(workshop, [photo, handout]))
    const workshops = workshopsPage(page)
    await workshops.openDetailWithFiles(id, venueId)
    api.mock(workshopsApi.attachmentDeletes(workshop, handout), workshopsApi.attachmentsReturn(workshop, [photo]))

    await workshops.deleteFile(id, handout.build().id, 'handout.pdf')

    await expect(workshops.at.fileRow('handout.pdf')).toHaveCount(0)
    await expect(workshops.at.fileRow('kiln.png')).toBeVisible()
    await expect(workshops.at.toast('File deleted')).toBeVisible()
    await expect(workshops.at.filesHeading).toBeFocused()
  })

  test('a reader sees the files with no upload and no delete', async ({ page, api }) => {
    const seenByReader = workshop.seenByReader()
    api.mock(workshopsApi.oneIs(seenByReader), workshopsApi.venueIs(venue), workshopsApi.attachmentsReturn(seenByReader, [handout]))
    const workshops = workshopsPage(page)

    await workshops.openDetailWithFiles(id, venueId)

    await expect(workshops.at.downloadLink('handout.pdf')).toBeVisible()
    await expect(workshops.at.uploadFile).toHaveCount(0)
    await expect(workshops.at.deleteFile('handout.pdf')).toHaveCount(0)
  })
})
