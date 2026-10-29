import { useEffect, useRef } from 'react'
import {
  createRootRoute,
  Outlet,
  useRouterState,
} from '@tanstack/react-router'
import { TanStackRouterDevtools } from '@tanstack/react-router-devtools'
import {
  SidebarInset,
  SidebarProvider,
  SidebarTrigger,
} from '@/components/ui/sidebar'
import { AppSidebar } from '@/components/app-sidebar'
import { NoTenantNotice } from '@/domain/membership/components/no-tenant-notice'
import { HrefRouterProvider } from '@/components/ui/link'
import { Separator } from '@/components/ui/separator'
import {
  Breadcrumb,
  BreadcrumbItem,
  BreadcrumbList,
  BreadcrumbPage,
} from '@/components/ui/breadcrumb'

declare module '@tanstack/react-router' {
  interface StaticDataRouteOption {
    /** The name of the page in the breadcrumb of the layout header. Each page route sets it. */
    breadcrumb?: string
  }
}

export const Route = createRootRoute({
  component: RootComponent,
})

function RootComponent() {
  const routerState = useRouterState()
  const page = routerState.matches[routerState.matches.length - 1]
  const isNotFound = routerState.matches.some((match) => match.globalNotFound)
  const pageName = isNotFound ? 'Not Found' : page?.staticData.breadcrumb
  const { content, announcement } = usePageFocusAfterNavigation(routerState.resolvedLocation?.pathname, pageName)

  return (
    <HrefRouterProvider>
      <SidebarProvider>
        <AppSidebar />
        <SidebarInset>
          <header className="flex h-16 shrink-0 items-center gap-2 transition-[width,height] ease-linear group-has-data-[collapsible=icon]/sidebar-wrapper:h-12">
            <div className="flex min-w-0 items-center gap-2 px-4">
              <SidebarTrigger className="-ml-1" />
              <Separator
                orientation="vertical"
                className="mr-2 aria-[orientation=vertical]:h-4 aria-[orientation=vertical]:self-center"
              />
              <Breadcrumb>
                <BreadcrumbList>
                  <BreadcrumbItem>
                    <BreadcrumbPage>{pageName}</BreadcrumbPage>
                  </BreadcrumbItem>
                </BreadcrumbList>
              </Breadcrumb>
            </div>
          </header>
          <NoTenantNotice />
          <div ref={content} className="flex flex-1 flex-col gap-4 p-4 pt-0">
            <Outlet />
          </div>
        </SidebarInset>
        <p ref={announcement} role="status" data-slot="page-announcement" className="sr-only" />
        {/* The default container is a footer, which is a contentinfo landmark. */}
        <TanStackRouterDevtools position="top-right" containerElement="div" />
      </SidebarProvider>
    </HrefRouterProvider>
  )
}

/**
 * After a navigation, a screen reader must hear the new page. A link in the page unmounts with the page, so the focus
 * goes to its level 1 heading, or to the main landmark. A link that stays, such as a sidebar link, keeps the focus, and
 * the status region says the name of the page. The location resolves after the lazy route loads, when the new page is
 * on the screen.
 */
function usePageFocusAfterNavigation(pathname: string | undefined, pageName: string | undefined) {
  const content = useRef<HTMLDivElement>(null)
  const announcement = useRef<HTMLParagraphElement>(null)
  const lastPathname = useRef(pathname)

  useEffect(() => {
    const previous = lastPathname.current
    lastPathname.current = pathname
    // The first load leaves the focus where the browser puts it.
    if (previous === undefined || previous === pathname || !content.current || !announcement.current) return
    const focused = document.activeElement
    const focusLost = !focused || focused === document.body
    const focusOutsidePage = !focusLost && !content.current.contains(focused)
    // An empty region between two announcements makes the second one a change, also for the same page name.
    announcement.current.textContent = focusOutsidePage ? (pageName ?? '') : ''
    if (focusLost) focusPageStart(content.current)
  }, [pathname, pageName])

  return { content, announcement }
}

/** The target gets a tab index only while it has the focus, so that a later click on the page does not focus it. */
function focusPageStart(content: HTMLElement) {
  const target = content.querySelector('h1') ?? content.closest('main')
  if (!target) return
  if (!target.hasAttribute('tabindex')) {
    target.setAttribute('tabindex', '-1')
    target.addEventListener('blur', () => target.removeAttribute('tabindex'), { once: true })
  }
  target.focus({ preventScroll: true })
}
