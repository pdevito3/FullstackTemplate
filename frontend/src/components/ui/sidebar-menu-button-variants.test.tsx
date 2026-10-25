import { expect, test } from 'vitest'
import { page } from 'vitest/browser'
import { render } from 'vitest-browser-react'

import {
  Sidebar,
  SidebarContent,
  SidebarMenu,
  SidebarMenuButton,
  SidebarMenuItem,
  SidebarProvider,
} from '@/components/ui/sidebar'
import { sidebarMenuButtonVariants } from '@/components/ui/sidebar-menu-button-variants'

test('a large menu trigger styled with sidebarMenuButtonVariants has the padding of a large SidebarMenuButton in a collapsed icon sidebar', async () => {
  await page.viewport(1280, 800)
  const screen = await render(
    <SidebarProvider defaultOpen={false}>
      <Sidebar collapsible="icon">
        <SidebarContent>
          <SidebarMenu>
            <SidebarMenuItem>
              <SidebarMenuButton size="lg">Workspace</SidebarMenuButton>
            </SidebarMenuItem>
            <SidebarMenuItem>
              <button type="button" className={sidebarMenuButtonVariants({ size: 'lg' })}>
                Demos
              </button>
            </SidebarMenuItem>
          </SidebarMenu>
        </SidebarContent>
      </Sidebar>
    </SidebarProvider>,
  )
  const menuButtonPadding = getComputedStyle(screen.getByRole('button', { name: 'Workspace' }).element()).padding
  const triggerPadding = getComputedStyle(screen.getByRole('button', { name: 'Demos' }).element()).padding

  expect(menuButtonPadding).toBe('0px')
  expect(triggerPadding).toBe(menuButtonPadding)
})
