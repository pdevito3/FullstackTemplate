import type { Page } from '@playwright/test'

export type ThemeChoice = 'Light mode' | 'Dark mode' | 'System theme'

export const themeToggle = (page: Page) => ({
  heading: page.getByRole('heading', { level: 1, name: 'About' }),
  choice: (name: ThemeChoice) => page.getByRole('button', { name, exact: true }),
})
