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
    if (!focused || focused === document.body) {
      focusWithoutTabStop(content.current.querySelector('h1') ?? content.current.closest('main'))
    } else if (!content.current.contains(focused)) {
      announce(announcement.current, pageName ?? '')
    }
  }, [pathname, pageName])

  return { content, announcement }
}

function focusWithoutTabStop(target: HTMLElement | null) {
  if (!target) return
  if (!target.hasAttribute('tabindex')) {
    target.setAttribute('tabindex', '-1')
    target.setAttribute('data-page-focus', '')
    target.addEventListener(
      'blur',
      () => {
        target.removeAttribute('tabindex')
        target.removeAttribute('data-page-focus')
      },
      { once: true },
    )
  }
  target.focus({ preventScroll: true })
}

// A screen reader speaks a live region when its text changes, so the same text again needs an empty frame first.
function announce(region: HTMLElement, text: string) {
  region.textContent = ''
  requestAnimationFrame(() => {
    region.textContent = text
  })
}
