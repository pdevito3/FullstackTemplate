import { as } from '@tests/support/auth/actors'
import { aProblem } from '@tests/support/builders/problem-details-builder'
import { aVenue } from '@tests/support/examples/venue-builder'
import { aWorkshop } from '@tests/support/examples/workshop-builder'
import { workshopPaths, workshopsApi } from '@tests/support/examples/workshops-api'
import { expect, test } from '@tests/support/fixtures'
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

  // At 07:00 UTC on August 16, it is August 15 in Pacific/Pago_Pago and August 16 in UTC and in Pacific/Kiritimati
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
    await expect(workshops.at.newStartZone('America/Chicago')).toBeVisible()
    await workshops.fillNew({ title: 'Intro to Kubb', start: '081620260930A', capacity: '12' })
    await workshops.pickVenue('Harbor Hall')

    await expect(workshops.at.newStartZone('Pacific/Pago Pago')).toBeVisible()
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

    await expect(workshops.at.newForm.getByText(
      'This time does not exist in America/Chicago on that day, because the clocks go forward. Choose another time.',
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
    await expect(workshops.at.editStartZone('Pacific/Pago Pago')).toBeVisible()

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
})
