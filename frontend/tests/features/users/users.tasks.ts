import { expect, type Page } from '@playwright/test'
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

    async pickRole(name: string, role: string) {
      await at.actionButton(name, 'Change role').click()
      await expect(at.roleDialog).toBeVisible()
      await at.roleSelect.click()
      await at.roleOption(role).click()
      await expect(at.roleSelect).toHaveAccessibleName(`${role} Role`)
    },

    async confirmRole(id: string) {
      const settled = answered('PUT', usersPaths.role(id))
      await at.confirmRole.click()
      await settled
    },

    async confirmRoleRefused(id: string) {
      const settled = Promise.all([answered('PUT', usersPaths.role(id)), answered('GET', usersPaths.one(id))])
      await at.confirmRole.click()
      await settled
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
