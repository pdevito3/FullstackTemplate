import { expect, type Page } from '@playwright/test'
import { failedLoad, nextFrames } from '@tests/support/answers'
import { usersPaths } from '@tests/support/api/users-api'
import { users } from './users.locators'

export function usersPage(page: Page) {
  const at = users(page)
  const answered = (method: string, path: string) =>
    page.waitForResponse((r) => r.request().method() === method && new URL(r.url()).pathname === path)

  const openListAt = async (path: string) => {
    const settled = answered('GET', usersPaths.list)
    await page.goto(path)
    await expect(at.listHeading).toBeVisible()
    await settled
  }

  return {
    at,

    async sidewaysOverflowOfTable() {
      return at.grid.evaluate((grid) => grid.parentElement!.scrollWidth - grid.parentElement!.clientWidth)
    },

    async pageOverflow() {
      return page.evaluate(() => document.documentElement.scrollWidth - document.documentElement.clientWidth)
    },

    openList: () => openListAt('/users'),

    openListAt,

    async goToNextPage() {
      const settled = answered('GET', usersPaths.list)
      await at.nextPage.click()
      await settled
    },

    async goToFirstPage() {
      const settled = answered('GET', usersPaths.list)
      await at.firstPageLink.click()
      await settled
    },

    async openUser(name: string, id: string) {
      const settled = answered('GET', usersPaths.one(id))
      await at.nameLink(name).click()
      await expect(at.detailHeading(name)).toBeVisible()
      await settled
    },

    async openDetail(id: string) {
      const settled = answered('GET', usersPaths.one(id))
      await page.goto(`/users/${id}`)
      await settled
    },

    /** Opens Change roles and turns each role on or off. */
    async toggleRoles(name: string, ...roles: string[]) {
      await at.actionButton(name, 'Change roles').click()
      await expect(at.roleDialog).toBeVisible()
      for (const role of roles) {
        const checkbox = at.roleCheckbox(role)
        const wasChecked = await checkbox.isChecked()
        // The label holds the box that the user sees. React Aria hides the input under it.
        await checkbox.locator('xpath=ancestor::label').click()
        await expect(checkbox).toBeChecked({ checked: !wasChecked })
      }
    },

    async confirmRole(id: string) {
      const settled = answered('PUT', usersPaths.roles(id))
      await at.confirmRole.click()
      await settled
    },

    async confirmRoleRefused(id: string) {
      const settled = Promise.all([answered('PUT', usersPaths.roles(id)), answered('GET', usersPaths.one(id))])
      await at.confirmRole.click()
      await settled
    },

    async confirmRoleRefusedAndFailToReadAgain(id: string) {
      const settled = Promise.all([answered('PUT', usersPaths.roles(id)), failedLoad(page, usersPaths.one(id))])
      await at.confirmRole.click()
      await settled
      await nextFrames(page)
    },

    async keepRole() {
      await at.keepRole.click()
      await expect(at.roleDialog).toBeHidden()
    },

    async askToDelete(name: string) {
      await at.actionButton(name, 'Delete').click()
      await expect(at.deleteDialog).toBeVisible()
    },

    async keepUser() {
      await at.keepUser.click()
      await expect(at.deleteDialog).toBeHidden()
    },

    async confirmDeleteAndOpenList(id: string) {
      const settled = Promise.all([answered('DELETE', usersPaths.one(id)), answered('GET', usersPaths.list)])
      await at.confirmDelete.click()
      await settled
    },

    async confirmDeleteRefused(id: string) {
      const settled = answered('DELETE', usersPaths.one(id))
      await at.confirmDelete.click()
      await settled
    },
  }
}
