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

  test('adds a workshop with the start that the user types', async ({ page, api }) => {
    const workshop = aWorkshop().titled('Intro to Kubb').at(venue)
    const { id, venueId } = workshop.build()
    api.mock(workshopsApi.venueListReturns([venue]), workshopsApi.venueIs(venue), workshopsApi.createSaves(workshop), workshopsApi.oneIs(workshop))
    const workshops = workshopsPage(page)
    await workshops.openNewForm(venueId)
    await workshops.fillNew({ title: 'Intro to Kubb', start: '060120270930A', capacity: '12' })

    await workshops.addAndOpenDetail(id, venueId)

    await expect(workshops.at.detailHeading('Intro to Kubb')).toBeVisible()
    const body = api.last('post', workshopPaths.list)?.body as { startsAt: string }
    expect(body).toMatchObject({ venueId, title: 'Intro to Kubb', capacity: 12 })
    expect(new Date(body.startsAt).getTime()).toBe(Date.UTC(2027, 5, 1, 9, 30))
  })

  test('shows the details that the API gives, in the time zone of the browser', async ({ page, api }) => {
    const workshop = aWorkshop().titled('Intro to Kubb').at(venue).startingAt('2026-06-01T15:00:00+00:00').withCapacity(12)
    api.mock(workshopsApi.oneIs(workshop), workshopsApi.venueIs(venue))

    await workshopsPage(page).openDetail(workshop.build().id, workshop.build().venueId)

    const { details } = workshopsPage(page).at
    await expect(details).toContainText('Jun 1, 2026, 3:00 PM')
    await expect(details).toContainText('12')
    await expect(details).toContainText('Main Hall')
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
