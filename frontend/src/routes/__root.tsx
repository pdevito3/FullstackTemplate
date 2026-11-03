import { useEffect, useRef } from 'react'
import { useQuery } from '@tanstack/react-query'
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
import { Button } from '@/components/ui/button'
import { login, returnUrlOf } from '@/domain/auth/apis/auth-actions'
import { authClaimsQuery } from '@/domain/auth/apis/get-user'
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
  const claims = useQuery(authClaimsQuery)

  // A failed refetch keeps the data of the last answer, and that answer still decides.
  if (claims.data === null) return <GoToLogin />
  if (claims.data) return <Layout />
  // A refetch with no data sets the query back to pending and clears the error, so the count of errors keeps the alert.
  if (claims.errorUpdateCount > 0) {
    return <SessionCheckFailed isRetrying={claims.isFetching} onRetry={() => void claims.refetch()} />
  }
  return null
}

function GoToLogin() {
  useEffect(() => {
    login(returnUrlOf(window.location))
  }, [])
  return null
}

function SessionCheckFailed({ isRetrying, onRetry }: { isRetrying: boolean; onRetry: () => void }) {
  return (
    <main className="flex min-h-svh items-center justify-center p-4">
      <div role="alert" className="flex max-w-md flex-col items-start gap-3 rounded-lg border border-destructive/20 bg-destructive/10 p-4 text-destructive">
        <p>We could not check whether you are signed in. Try again in a moment.</p>
        <Button variant="outline" isPending={isRetrying} onPress={onRetry}>Try Again</Button>
      </div>
    </main>
  )
}

function Layout() {
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
        <p ref={announcement} role="status" data-testid="page-announcement" className="sr-only" />
        {/* The default container is a footer, which is a contentinfo landmark. */}
        <TanStackRouterDevtools position="top-right" containerElement="div" />
      </SidebarProvider>
    </HrefRouterProvider>
  )
}

function usePageFocusAfterNavigation(pathname: string | undefined, pageName: string | undefined) {
  const content = useRef<HTMLDivElement>(null)
  const announcement = useRef<HTMLParagraphElement>(null)
  const pendingAnnouncement = useRef<ReturnType<typeof setTimeout>>(undefined)
  const lastPathname = useRef(pathname)

  useEffect(() => {
    const previous = lastPathname.current
    lastPathname.current = pathname
    // The first load leaves the focus where the browser puts it.
    if (previous === undefined || previous === pathname || !content.current || !announcement.current) return
    clearTimeout(pendingAnnouncement.current)
    announcement.current.textContent = ''
    const focused = document.activeElement
    if (!focused || focused === document.body) {
      return focusStartOfPage(content.current)
    } else {
      const region = announcement.current
      // A screen reader speaks a live region when its text changes. The empty text must reach the accessibility tree
      // first, so the same page name again is a change. One frame is too short for that.
      pendingAnnouncement.current = setTimeout(() => {
        region.textContent = pageName ?? ''
      }, 150)
    }
  }, [pathname, pageName])

  return { content, announcement }
}

// A page that loads its data shows its heading after the navigation. The main landmark holds the focus until then.
function focusStartOfPage(content: HTMLElement) {
  const heading = content.querySelector('h1')
  if (heading) {
    focusWithoutTabStop(heading)
    return
  }
  const main = content.closest('main')
  focusWithoutTabStop(main)
  const observer = new MutationObserver(() => {
    if (document.activeElement !== main) return observer.disconnect()
    const lateHeading = content.querySelector('h1')
    if (!lateHeading) return
    observer.disconnect()
    focusWithoutTabStop(lateHeading)
  })
  observer.observe(content, { childList: true, subtree: true })
  return () => observer.disconnect()
}

function focusWithoutTabStop(target: HTMLElement | null) {
  if (!target) return
  if (!target.hasAttribute('tabindex')) {
    target.setAttribute('tabindex', '-1')
    target.setAttribute('data-page-focus', '')
    const removeTabStop = () => {
      target.removeAttribute('tabindex')
      target.removeAttribute('data-page-focus')
      target.removeEventListener('blur', handleBlur)
      target.removeEventListener('pointerdown', removeTabStop)
    }
    // A switch to another tab blurs the target, but the target keeps the focus for the return.
    const handleBlur = () => {
      if (document.activeElement !== target) removeTabStop()
    }
    target.addEventListener('blur', handleBlur)
    // A press inside the target must not give it the focus again.
    target.addEventListener('pointerdown', removeTabStop)
  }
  target.focus({ preventScroll: true })
}
