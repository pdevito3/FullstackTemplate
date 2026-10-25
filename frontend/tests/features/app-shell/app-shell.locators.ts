import type { Page } from '@playwright/test'

export const appShell = (page: Page) => ({
  aboutText: page.getByText('This is a demo application built with .NET Aspire and React'),
})
