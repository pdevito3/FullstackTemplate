import type { Page } from '@playwright/test'

export type ThemeChoice = 'Light mode' | 'Dark mode' | 'System theme'

export const themeToggle = (page: Page) => ({
  heading: page.getByRole('heading', { name: 'Component Library' }),
  choice: (name: ThemeChoice) => page.getByRole('button', { name, exact: true }),
})
