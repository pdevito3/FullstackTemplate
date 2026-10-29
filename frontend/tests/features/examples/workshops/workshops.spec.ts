import { as } from '@tests/support/auth/actors'
import { aProblem } from '@tests/support/builders/problem-details-builder'
import { aVenue } from '@tests/support/examples/venue-builder'
import { aWorkshop } from '@tests/support/examples/workshop-builder'
import { workshopPaths, workshopsApi } from '@tests/support/examples/workshops-api'
import { expect, test } from '@tests/support/fixtures'
import { useDefaultFontSize } from '@tests/support/screen'
import { workshopsPage } from './workshops.tasks'

test.describe('the workshops', () => {
  test.use({ actor: as.user('Ana Admin') })

  const venue = aVenue().named('Main Hall')

  test('opens a workshop from the list, and keeps the user on the form after a stale save', async ({ page, api }) => {
    const workshop = aWorkshop().titled('Intro to Kubb').at(venue)
    const { id, venueId } = workshop.build()
    api.mock(workshopsApi.listReturns([workshop]), workshopsApi.oneIs(workshop), workshopsApi.venueIs(venue))
    const workshops = workshopsPage(page)
    await workshops.openList()
    await workshops.openWorkshop('Intro to Kubb', id, venueId)
    await workshops.edit('Intro to Kubb')
    await workshops.renameTo('Kubb in depth')
    const savedByOther = workshop.savedAgain({ title: 'Kubb for teams' })
    api.mock(workshopsApi.updateFails(id, aProblem().staleVersion()), workshopsApi.oneIs(savedByOther))

    await workshops.saveRefused(id)

    await expect(workshops.at.staleAlert).toBeVisible()
    await expect(workshops.at.title).toHaveValue('Kubb in depth')
    expect(api.last('put', workshopPaths.one(id))?.body).toMatchObject({ title: 'Kubb in depth', version: 1 })

    await workshops.loadLatest(id)

    await expect(page).toHaveURL(`/workshops/${id}/edit`)
    await expect(workshops.at.staleAlert).toBeHidden()
    await expect(workshops.at.title).toHaveValue('Kubb for teams')

    await workshops.renameTo('Kubb for teams, part 2')
    const saved = savedByOther.savedAgain({ title: 'Kubb for teams, part 2' })
    api.mock(workshopsApi.updateSaves(saved), workshopsApi.oneIs(saved))
    await workshops.saveAndOpenDetail(id, venueId)

    await expect(workshops.at.detailHeading('Kubb for teams, part 2')).toBeVisible()
    expect(api.last('put', workshopPaths.one(id))?.body).toMatchObject({ version: 2 })
  })

  const now = new Date('2026-08-16T07:00:00Z')
  const harbor = aVenue().named('Harbor Hall').inZone('Pacific/Pago_Pago')

  test('adds a workshop with the start that the user types, read in the time zone of the venue that the user picks', async ({ page, api }) => {
    await page.clock.setFixedTime(now)
    const workshop = aWorkshop().titled('Intro to Kubb').at(harbor)
    const { id } = workshop.build()
    api.mock(
      workshopsApi.venueListReturns([venue, harbor]),
      workshopsApi.venueIs(venue),
      workshopsApi.venueIs(harbor),
      workshopsApi.createSaves(workshop),
      workshopsApi.oneIs(workshop),
    )
    const workshops = workshopsPage(page)
    await workshops.openNewForm(venue.build().id)
    await expect(workshops.at.newStartZone('Chicago')).toBeVisible()
    await workshops.fillNew({ title: 'Intro to Kubb', start: '081620260930A', capacity: '12' })
    await workshops.pickVenue('Harbor Hall')

    await expect(workshops.at.newStartZone('Pago Pago')).toBeVisible()
    await workshops.addAndOpenDetail(id, harbor.build().id)

    await expect(workshops.at.detailHeading('Intro to Kubb')).toBeVisible()
    const body = api.last('post', workshopPaths.list)?.body as { startsAt: string }
    expect(body).toMatchObject({ venueId: harbor.build().id, title: 'Intro to Kubb', capacity: 12 })
    expect(new Date(body.startsAt).getTime()).toBe(Date.UTC(2026, 7, 16, 20, 30))
  })

  test('refuses a start in the hour that the clocks skip at the venue, and does not add it', async ({ page, api }) => {
    api.mock(workshopsApi.venueListReturns([venue]), workshopsApi.venueIs(venue))
    const workshops = workshopsPage(page)
    await workshops.openNewForm(venue.build().id)
    await workshops.fillNew({ title: 'Intro to Kubb', start: '031420270230A', capacity: '12' })

    await workshops.at.add.click()

    await expect(workshops.at.newFormMessage(
      'This time does not exist in Chicago on that day, because the clocks go forward. Choose another time.',
    )).toBeVisible()
    expect(api.last('post', workshopPaths.list)).toBeUndefined()
  })

  test('shows the start in the time zone of the venue, in the list and on the detail', async ({ page, api }) => {
    await page.clock.setFixedTime(now)
    const workshop = aWorkshop().titled('Intro to Kubb').at(harbor).startingAt('2026-08-16T07:00:00+00:00').withCapacity(12)
    api.mock(workshopsApi.listReturns([workshop]), workshopsApi.oneIs(workshop), workshopsApi.venueIs(harbor))
    const workshops = workshopsPage(page)
    await workshops.openList()

    await expect(workshops.at.row('Intro to Kubb')).toContainText('Aug 15, 2026, 8:00 PM GMT-11')

    await workshops.openWorkshop('Intro to Kubb', workshop.build().id, harbor.build().id)

    await expect(workshops.at.details).toContainText('Aug 15, 2026, 8:00 PM GMT-11')
    await expect(workshops.at.details).toContainText('12')
    await expect(workshops.at.details).toContainText('Harbor Hall')
  })

  test('edits the start in the time zone of the venue', async ({ page, api }) => {
    await page.clock.setFixedTime(now)
    const workshop = aWorkshop().titled('Intro to Kubb').at(harbor).startingAt('2026-08-16T07:00:00+00:00')
    const { id } = workshop.build()
    api.mock(workshopsApi.oneIs(workshop), workshopsApi.venueIs(harbor), workshopsApi.updateSaves(workshop.savedAgain()))
    const workshops = workshopsPage(page)
    await workshops.openEditForm(id)
    await expect(workshops.at.editStartZone('Pago Pago')).toBeVisible()

    await workshops.setEditHour('9')
    await workshops.saveAndOpenDetail(id, harbor.build().id)

    const body = api.last('put', workshopPaths.one(id))?.body as { startsAt: string }
    expect(new Date(body.startsAt).getTime()).toBe(Date.UTC(2026, 7, 16, 8, 0))
  })

  test('schedules a draft, and then shows only the actions that the API allows', async ({ page, api }) => {
    const draft = aWorkshop().titled('Intro to Kubb').at(venue)
    const { id, venueId } = draft.build()
    api.mock(workshopsApi.oneIs(draft), workshopsApi.venueIs(venue))
    const workshops = workshopsPage(page)
    await workshops.openDetail(id, venueId)
    api.mock(workshopsApi.actionMoves('schedule', draft.inStatus('Scheduled')))

    await workshops.send('Intro to Kubb', 'Schedule', workshopPaths.action(id, 'schedule'))

    await expect(workshops.at.status('Scheduled')).toBeVisible()
    await expect(workshops.at.actionButton('Intro to Kubb', 'Mark completed')).toBeVisible()
    await expect(workshops.at.actionButton('Intro to Kubb', 'Cancel workshop')).toBeVisible()
    await expect(workshops.at.actionButton('Intro to Kubb', 'Schedule')).toHaveCount(0)
    await expect(workshops.at.editLink('Intro to Kubb')).toHaveCount(0)
  })

  test('asks before it cancels a workshop, and Keep workshop sends nothing', async ({ page, api }) => {
    const draft = aWorkshop().titled('Intro to Kubb').at(venue)
    const { id, venueId } = draft.build()
    api.mock(workshopsApi.oneIs(draft), workshopsApi.venueIs(venue))
    const workshops = workshopsPage(page)
    await workshops.openDetail(id, venueId)

    await workshops.askToCancel('Intro to Kubb')
    await workshops.keepWorkshop()

    expect(api.all('post', workshopPaths.action(id, 'cancel'))).toHaveLength(0)
    await expect(workshops.at.actionButton('Intro to Kubb', 'Cancel workshop')).toBeFocused()

    api.mock(workshopsApi.actionMoves('cancel', draft.inStatus('Cancelled')))
    await workshops.askToCancel('Intro to Kubb')
    await workshops.confirmCancel(workshopPaths.action(id, 'cancel'))

    await expect(workshops.at.status('Cancelled')).toBeVisible()
    await expect(workshops.at.cancelDialog).toBeHidden()
    await expect(workshops.at.detailHeading('Intro to Kubb')).toBeFocused()
  })

  test('shows no actions to a caller that the API allows none', async ({ page, api }) => {
    const workshop = aWorkshop().titled('Intro to Kubb').at(venue).seenByReader()
    api.mock(workshopsApi.oneIs(workshop), workshopsApi.venueIs(venue))

    const workshops = workshopsPage(page)
    await workshops.openDetail(workshop.build().id, workshop.build().venueId)

    await expect(workshops.at.detailHeading('Intro to Kubb')).toBeVisible()
    await expect(workshops.at.actions('Intro to Kubb')).toHaveCount(0)
  })

  test('puts the search and the page in the URL, and sends them to the API', async ({ page, api }) => {
    const workshops = workshopsPage(page)
    api.mock(workshopsApi.listReturns([aWorkshop()], { total: 25 }))
    await workshops.openList()

    api.mock(workshopsApi.listReturns([aWorkshop().titled('Kubb 11')], { total: 25, pageNumber: 2 }))
    await workshops.goToNextPage()

    await expect(page).toHaveURL('/workshops?page=2')
    expect(api.last('get', workshopPaths.list)?.query.get('pageNumber')).toBe('2')

    api.mock(workshopsApi.listReturns([aWorkshop().titled('Kubb basics')], { total: 1 }))
    await workshops.searchFor('Kubb')

    await expect(page).toHaveURL('/workshops?search=Kubb')
    expect(api.last('get', workshopPaths.list)?.query.get('search')).toBe('Kubb')
    expect(api.last('get', workshopPaths.list)?.query.get('pageNumber')).toBe('1')
    await expect(workshops.at.row('Kubb basics')).toBeVisible()
  })

  test('shows an empty page past the end of the list, with a link to the first page', async ({ page, api }) => {
    api.mock(workshopsApi.listReturns([], { total: 25, pageNumber: 5 }))
    const workshops = workshopsPage(page)
    await workshops.openListAt('/workshops?search=Kubb&page=5')

    await expect(workshops.at.emptyPage).toContainText('There are no workshops on page 5.')

    api.mock(workshopsApi.listReturns([aWorkshop().titled('Kubb basics')], { total: 25 }))
    await workshops.goToFirstPage()

    await expect(page).toHaveURL('/workshops?search=Kubb')
    await expect(workshops.at.row('Kubb basics')).toBeVisible()
  })

  test('opens the new workshop form with no venue and no toast when the venue of the link does not exist', async ({ page, api }) => {
    const missingId = aVenue().build().id
    api.mock(workshopsApi.venueListReturns([venue]), workshopsApi.venueFails(missingId, aProblem().notFound('The venue does not exist.')))
    const workshops = workshopsPage(page)

    await workshops.openNewForm(missingId)

    await expect(workshops.at.newVenue).toHaveValue('')
    await expect(workshops.at.newStartWithNoZone).toBeVisible()
    await expect(workshops.at.toasts).toHaveCount(0)

    await workshops.fillNew({ title: 'Intro to Kubb', start: '081620260930A', capacity: '12' })
    await workshops.at.add.click()

    await expect(workshops.at.newFormMessage('Pick a venue.')).toBeVisible()
    expect(api.last('post', workshopPaths.list)).toBeUndefined()
  })

  test('tells a caller who cannot manage workshops why a draft has no edit form', async ({ page, api }) => {
    const draft = aWorkshop().titled('Intro to Kubb').at(venue).seenByReader()
    api.mock(workshopsApi.oneIs(draft))
    const workshops = workshopsPage(page)

    await workshops.openEditPage(draft.build().id)

    await expect(workshops.at.editRefusal('You cannot manage workshops. Ask a tenant admin for access.')).toBeVisible()
    await expect(workshops.at.editForm).toHaveCount(0)
  })

  test('tells the caller that a scheduled workshop cannot be changed', async ({ page, api }) => {
    const scheduled = aWorkshop().titled('Intro to Kubb').at(venue).inStatus('Scheduled')
    api.mock(workshopsApi.oneIs(scheduled))
    const workshops = workshopsPage(page)

    await workshops.openEditPage(scheduled.build().id)

    await expect(workshops.at.editRefusal('A scheduled workshop cannot be changed.')).toBeVisible()
    await expect(workshops.at.editForm).toHaveCount(0)
  })
})

test.describe('the workshops of a signed-in user with no tenant', () => {
  test.use({ actor: as.userWithNoTenant('Ada Lovelace') })

  test('shows the way to start a tenant in place of the list, asks for no list, and shows no New workshop or New venue', async ({ page, api }) => {
    api.mock(workshopsApi.listReturns([]))
    const workshops = workshopsPage(page)

    await page.goto('/workshops')

    await expect(workshops.at.startTenantFirst).toBeVisible()
    expect(api.last('get', workshopPaths.list)).toBeUndefined()
    await expect(workshops.at.startTenantLink).toHaveAttribute('href', '/start-tenant')
    await expect(workshops.at.emptyList).toHaveCount(0)
    await expect(workshops.at.newWorkshopLink).toHaveCount(0)
    await expect(workshops.at.newVenueLink).toHaveCount(0)
  })
})

test.describe('the focus after an action that removes the focused button', () => {
  test.use({ actor: as.user('Ana Admin') })

  const venue = aVenue().named('Main Hall')

  test('Add venue opens the new workshop form with the focus on its heading', async ({ page, api }) => {
    api.mock(workshopsApi.venueCreateSaves(venue), workshopsApi.venueIs(venue), workshopsApi.venueListReturns([venue]))
    const workshops = workshopsPage(page)
    await workshops.openNewVenueForm()

    await workshops.addVenueAndOpenNewForm('Main Hall', venue.build().id)

    await expect(workshops.at.newVenue).toHaveValue('Main Hall')
    await expect(workshops.at.newHeading).toBeFocused()
  })

  test('Add workshop opens the detail with the focus on its heading', async ({ page, api }) => {
    const workshop = aWorkshop().titled('Intro to Kubb').at(venue)
    api.mock(
      workshopsApi.venueListReturns([venue]),
      workshopsApi.venueIs(venue),
      workshopsApi.createSaves(workshop),
      workshopsApi.oneIs(workshop),
    )
    const workshops = workshopsPage(page)
    await workshops.openNewForm(venue.build().id)
    await workshops.fillNew({ title: 'Intro to Kubb', start: '081620260930A', capacity: '12' })

    await workshops.addAndOpenDetail(workshop.build().id, venue.build().id)

    await expect(workshops.at.detailHeading('Intro to Kubb')).toBeFocused()
  })

  test('Save opens the detail with the focus on its heading', async ({ page, api }) => {
    const workshop = aWorkshop().titled('Intro to Kubb').at(venue)
    const saved = workshop.savedAgain({ title: 'Kubb in depth' })
    const { id, venueId } = workshop.build()
    api.mock(workshopsApi.oneIs(workshop), workshopsApi.venueIs(venue))
    const workshops = workshopsPage(page)
    await workshops.openEditForm(id)
    await workshops.renameTo('Kubb in depth')
    api.mock(workshopsApi.updateSaves(saved), workshopsApi.oneIs(saved))

    await workshops.saveAndOpenDetail(id, venueId)

    await expect(workshops.at.detailHeading('Kubb in depth')).toBeFocused()
  })

  test('a submit with two errors puts the focus on the first of them in the form order', async ({ page, api }) => {
    api.mock(workshopsApi.venueListReturns([venue]), workshopsApi.venueIs(venue))
    const workshops = workshopsPage(page)
    await page.goto('/workshops/new')
    await expect(workshops.at.newForm).toBeVisible()
    await workshops.at.newCapacity.fill('12')

    await workshops.at.add.click()

    await expect(workshops.at.newFormMessage('Pick a venue.')).toBeVisible()
    await expect(workshops.at.newFormMessage('Enter a title.')).toBeVisible()
    await expect(workshops.at.newVenue).toBeFocused()

    await workshops.pickVenue('Main Hall')
    await workshops.at.newTitle.fill('Intro to Kubb')
    await workshops.at.newCapacity.fill('0')

    await workshops.at.add.click()

    await expect(workshops.at.newFormMessage('Enter a start.')).toBeVisible()
    await expect(workshops.at.newFormMessage('Use 1 or more.')).toBeVisible()
    await expect(workshops.at.newStartMonth).toBeFocused()
    expect(api.last('post', workshopPaths.list)).toBeUndefined()
  })

  test('Load the latest puts the focus on the first field that the latest save changed', async ({ page, api }) => {
    const workshop = aWorkshop().titled('Intro to Kubb').at(venue)
    const { id } = workshop.build()
    api.mock(workshopsApi.oneIs(workshop), workshopsApi.venueIs(venue))
    const workshops = workshopsPage(page)
    await workshops.openEditForm(id)
    await workshops.renameTo('Kubb in depth')
    api.mock(workshopsApi.updateFails(id, aProblem().staleVersion()), workshopsApi.oneIs(workshop.savedAgain({ title: 'Kubb for teams' })))
    await workshops.saveRefused(id)

    await workshops.loadLatest(id)

    await expect(workshops.at.staleAlert).toBeHidden()
    await expect(workshops.at.title).toHaveValue('Kubb for teams')
    await expect(workshops.at.title).toBeFocused()
  })

  test('a refused action puts the focus on the heading when the reload takes its button away', async ({ page, api }) => {
    const draft = aWorkshop().titled('Intro to Kubb').at(venue)
    const { id, venueId } = draft.build()
    api.mock(workshopsApi.oneIs(draft), workshopsApi.venueIs(venue))
    const workshops = workshopsPage(page)
    await workshops.openDetail(id, venueId)
    api.mock(
      workshopsApi.actionFails(id, 'schedule', aProblem().invalid({ status: ['A cancelled workshop cannot be scheduled.'] })),
      workshopsApi.oneIs(draft.inStatus('Cancelled')),
    )

    await workshops.sendWithEnter('Intro to Kubb', 'Schedule', workshopPaths.action(id, 'schedule'))

    await expect(workshops.at.status('Cancelled')).toBeVisible()
    await expect(workshops.at.actionButton('Intro to Kubb', 'Schedule')).toHaveCount(0)
    await expect(workshops.at.detailHeading('Intro to Kubb')).toBeFocused()
  })

  test('Schedule puts the focus on the heading, so a second Enter sends no other action', async ({ page, api }) => {
    const draft = aWorkshop().titled('Intro to Kubb').at(venue)
    const { id, venueId } = draft.build()
    api.mock(workshopsApi.oneIs(draft), workshopsApi.venueIs(venue))
    const workshops = workshopsPage(page)
    await workshops.openDetail(id, venueId)
    api.mock(workshopsApi.actionMoves('schedule', draft.inStatus('Scheduled')))

    await workshops.sendWithEnter('Intro to Kubb', 'Schedule', workshopPaths.action(id, 'schedule'))

    await expect(workshops.at.status('Scheduled')).toBeVisible()
    await expect(workshops.at.detailHeading('Intro to Kubb')).toBeFocused()

    await workshops.pressEnter()

    await expect(workshops.at.detailHeading('Intro to Kubb')).toBeFocused()
    expect(api.all('post', workshopPaths.action(id, 'cancel'))).toHaveLength(0)
    expect(api.all('post', workshopPaths.action(id, 'complete'))).toHaveLength(0)
  })
})

const longName = 'Maximilianbartholomewalexandervanderwesthuizenmontgomerysmythesinclairfitzgeraldworthingtonhughes'

test.describe('a workshop with a long title at a venue with a long name, on a 320 px phone', () => {
  test.use({ actor: as.user('Ana Admin'), viewport: { width: 320, height: 640 } })

  test('the detail wraps the title and each value in the window', async ({ page, api }) => {
    const venue = aVenue().named(longName)
    const workshop = aWorkshop().titled(longName).at(venue)
    api.mock(workshopsApi.oneIs(workshop), workshopsApi.venueIs(venue))
    const workshops = workshopsPage(page)

    await workshops.openDetail(workshop.build().id, venue.build().id)

    await expect(workshops.at.details).toContainText(longName)
    expect(await workshops.pageOverflow()).toBe(0)
    expect(await workshops.textOverflowPast(workshops.at.detailHeading(longName))).toBeLessThanOrEqual(0)
    expect(await workshops.textOverflowPast(workshops.at.details)).toBeLessThanOrEqual(0)
  })
})

test.describe('the workshop list on a 320 px phone', () => {
  test.use({ actor: as.user('Ana Admin'), viewport: { width: 320, height: 640 } })

  test('shows the title, the start, the capacity, and the status of each row with no sideways scroll', async ({ page, api }) => {
    const venue = aVenue().named('Main Hall')
    api.mock(
      workshopsApi.listReturns([
        aWorkshop().titled('Intro to Kubb for product teams').at(venue).withCapacity(120),
        aWorkshop().titled(longName).at(venue).inStatus('Scheduled'),
      ]),
    )
    const workshops = workshopsPage(page)

    await workshops.openList()

    expect(await workshops.sidewaysOverflowOfTable()).toBe(0)
    expect(await workshops.pageOverflow()).toBe(0)
    await expect(workshops.at.row('Intro to Kubb for product teams')).toContainText('Jun 1, 2026, 10:00 AM CDT, capacity 120')
    expect(await workshops.textOverflowPast(workshops.at.rowStatus('Intro to Kubb for product teams', 'Draft'))).toBeLessThanOrEqual(0)
    expect(await workshops.textOverflowPast(workshops.at.rowStatus(longName, 'Scheduled'))).toBeLessThanOrEqual(0)
    expect(await workshops.lineCount(workshops.at.titleLink('Intro to Kubb for product teams'))).toBeLessThanOrEqual(2)
  })
})

test.describe('the start field on a 390 px phone with a 24 px default font', () => {
  test.use({ actor: as.user('Ana Admin'), viewport: { width: 390, height: 844 } })

  const venue = aVenue().named('Main Hall')

  test('the new form shows each segment of the start inside the field', async ({ page, api }) => {
    api.mock(workshopsApi.venueListReturns([venue]), workshopsApi.venueIs(venue))
    const workshops = workshopsPage(page)
    await useDefaultFontSize(page, 24)

    await workshops.openNewForm(venue.build().id)

    await expect(workshops.at.newStartSegments).toHaveCount(6)
    expect(await workshops.segmentsCutIn(workshops.at.newStart, workshops.at.newStartSegments)).toEqual([])
  })

  test('the edit form shows each segment of the start inside the field', async ({ page, api }) => {
    const workshop = aWorkshop().titled('Intro to Kubb').at(venue)
    api.mock(workshopsApi.oneIs(workshop), workshopsApi.venueIs(venue))
    const workshops = workshopsPage(page)
    await useDefaultFontSize(page, 24)

    await workshops.openEditForm(workshop.build().id)

    await expect(workshops.at.editStartSegments).toHaveCount(6)
    expect(await workshops.segmentsCutIn(workshops.at.editStart, workshops.at.editStartSegments)).toEqual([])
  })
})
