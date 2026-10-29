import { expect, type Locator, type Page } from '@playwright/test'
import { workshopPaths } from '@tests/support/examples/workshops-api'
import { workshops } from './workshops.locators'

export function workshopsPage(page: Page) {
  const at = workshops(page)
  const answered = (method: string, path: string) =>
    page.waitForResponse((r) => r.request().method() === method && new URL(r.url()).pathname === path)

  return {
    at,

    async textOverflowPast(text: Locator) {
      const right = await page.evaluate(() => document.documentElement.clientWidth)
      return text.evaluate((node, right) => {
        const range = document.createRange()
        range.selectNodeContents(node)
        return range.getBoundingClientRect().right - right
      }, right)
    },

    async sidewaysOverflowOfTable() {
      return at.grid.evaluate((grid) => grid.parentElement!.scrollWidth - grid.parentElement!.clientWidth)
    },

    async lineCount(text: Locator) {
      return text.evaluate((node) => node.getClientRects().length)
    },

    async pageOverflow() {
      return page.evaluate(() => document.documentElement.scrollWidth - document.documentElement.clientWidth)
    },

    async segmentsCutIn(field: Locator, segments: Locator) {
      const box = await field.evaluate((node) => node.getBoundingClientRect().toJSON() as DOMRect)
      return segments.evaluateAll(
        (nodes, box) =>
          nodes
            .filter((node) => {
              const { top, bottom, left, right } = node.getBoundingClientRect()
              return top < box.top || bottom > box.bottom || left < box.left || right > box.right
            })
            .map((node) => node.textContent),
        box,
      )
    },

    async openList() {
      const settled = answered('GET', workshopPaths.list)
      await page.goto('/workshops')
      await expect(at.listHeading).toBeVisible()
      await settled
    },

    async openDetail(id: string, venueId: string) {
      const settled = Promise.all([answered('GET', workshopPaths.one(id)), answered('GET', workshopPaths.venue(venueId))])
      await page.goto(`/workshops/${id}`)
      await settled
    },

    async openListAt(path: string) {
      const settled = answered('GET', workshopPaths.list)
      await page.goto(path)
      await expect(at.listHeading).toBeVisible()
      await settled
    },

    async goToFirstPage() {
      const settled = answered('GET', workshopPaths.list)
      await at.firstPageLink.click()
      await settled
    },

    async openNewForm(venueId: string) {
      const settled = answered('GET', workshopPaths.venue(venueId))
      await page.goto(`/workshops/new?venueId=${venueId}`)
      await expect(at.newForm).toBeVisible()
      await settled
    },

    async fillNew({ title, start, capacity }: { title: string; start: string; capacity: string }) {
      await at.newTitle.fill(title)
      await at.newStartMonth.click()
      await page.keyboard.type(start)
      await at.newCapacity.fill(capacity)
    },

    async openNewVenueForm() {
      await page.goto('/venues/new')
      await expect(at.venueForm).toBeVisible()
    },

    async addVenueAndOpenNewForm(name: string, venueId: string) {
      await at.venueName.fill(name)
      const settled = Promise.all([
        answered('POST', workshopPaths.venues),
        answered('GET', workshopPaths.venue(venueId)),
        answered('GET', workshopPaths.venues),
      ])
      await at.addVenue.click()
      await settled
      await expect(at.newForm).toBeVisible()
    },

    async pickVenue(name: string) {
      await at.newVenue.fill(name)
      await at.venueOption(name).click()
      await expect(at.newVenue).toHaveValue(name)
    },

    async openEditForm(id: string) {
      const settled = answered('GET', workshopPaths.one(id))
      await page.goto(`/workshops/${id}/edit`)
      await expect(at.editForm).toBeVisible()
      await settled
    },

    async openEditPage(id: string) {
      const settled = answered('GET', workshopPaths.one(id))
      await page.goto(`/workshops/${id}/edit`)
      await expect(at.editHeading).toBeVisible()
      await settled
    },

    async setEditHour(hour: string) {
      await at.editStartHour.click()
      await page.keyboard.type(hour)
    },

    async addAndOpenDetail(id: string, venueId: string) {
      const settled = Promise.all([
        answered('POST', workshopPaths.list),
        answered('GET', workshopPaths.one(id)),
        answered('GET', workshopPaths.venue(venueId)),
      ])
      await at.add.click()
      await settled
    },

    async searchFor(text: string) {
      const settled = answered('GET', workshopPaths.list)
      await at.search.fill(text)
      await settled
    },

    async goToNextPage() {
      const settled = answered('GET', workshopPaths.list)
      await at.nextPage.click()
      await settled
    },

    async openWorkshop(title: string, id: string, venueId: string) {
      const settled = Promise.all([answered('GET', workshopPaths.one(id)), answered('GET', workshopPaths.venue(venueId))])
      await at.titleLink(title).click()
      await expect(at.detailHeading(title)).toBeVisible()
      await settled
    },

    async edit(title: string) {
      await at.editLink(title).click()
      await expect(at.editForm).toBeVisible()
    },

    async renameTo(title: string) {
      await at.title.fill(title)
    },

    async saveRefused(id: string) {
      const settled = answered('PUT', workshopPaths.one(id))
      await at.save.click()
      await settled
    },

    async saveAndOpenDetail(id: string, venueId: string) {
      const settled = Promise.all([
        answered('PUT', workshopPaths.one(id)),
        answered('GET', workshopPaths.one(id)),
        answered('GET', workshopPaths.venue(venueId)),
      ])
      await at.save.click()
      await settled
    },

    async loadLatest(id: string) {
      const settled = answered('GET', workshopPaths.one(id))
      await at.loadLatest.click()
      await settled
    },

    async send(title: string, action: string, path: string) {
      const settled = answered('POST', path)
      await at.actionButton(title, action).click()
      await settled
    },

    async sendWithEnter(title: string, action: string, path: string) {
      const settled = answered('POST', path)
      await at.actionButton(title, action).press('Enter')
      await settled
    },

    async askToCancel(title: string) {
      await at.actionButton(title, 'Cancel workshop').click()
      await expect(at.cancelDialog).toBeVisible()
    },

    async keepWorkshop() {
      await at.keepWorkshop.click()
      await expect(at.cancelDialog).toBeHidden()
    },

    async confirmCancel(path: string) {
      const settled = answered('POST', path)
      await at.confirmCancel.click()
      await settled
    },

    async pressEnter() {
      await page.keyboard.press('Enter')
    },
  }
}
