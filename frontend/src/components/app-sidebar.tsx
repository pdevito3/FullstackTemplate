import { useMatchRoute, useRouterState } from "@tanstack/react-router"
import {
  Home01Icon,
  InformationCircleIcon,
} from "@hugeicons/core-free-icons"
import { HugeiconsIcon } from "@hugeicons/react"

import {
  Sidebar,
  SidebarContent,
  SidebarFooter,
  SidebarGroup,
  SidebarGroupContent,
  SidebarGroupLabel,
  SidebarHeader,
  SidebarMenu,
  SidebarMenuItem,
  SidebarMenuLink,
  SidebarRail,
} from "@/components/ui/sidebar"
import { useSidebar } from "@/components/ui/use-sidebar"
import { ThemeToggle } from "@/components/theme-toggle"
import { currentNavItem } from "@/components/current-nav-item"
// example:start
import { ExampleNav } from "@/examples/example-nav"
import { exampleNavUrls } from "@/examples/example-nav-items"
// example:end

const appName = "Fullstack Template"
const appInitials = appName
  .split(/[ .-]/)
  .map((word) => word[0])
  .join("")
  .slice(0, 2)
  .toUpperCase()

const navItems = [
  {
    title: "Home",
    url: "/",
    icon: Home01Icon,
  },
  {
    title: "About",
    url: "/about",
    icon: InformationCircleIcon,
  },
] as const

const navUrls = [
  ...navItems.map((item) => item.url),
  // example:start
  ...exampleNavUrls,
  // example:end
]

export function AppSidebar() {
  const routerState = useRouterState()
  const currentPath = routerState.location.pathname
  const matchRoute = useMatchRoute()
  const current = currentNavItem(navUrls, (to, fuzzy) => matchRoute({ to, fuzzy }) !== false)
  const { setOpenMobile } = useSidebar()

  // On mobile the sidebar is a sheet over the page. Close it when a link is
  // pressed, so the user sees the new page. On desktop the sheet is not used.
  const closeMobile = () => setOpenMobile(false)

  return (
    <Sidebar variant="inset" collapsible="icon">
      <SidebarHeader>
        <SidebarMenu>
          <SidebarMenuItem>
            <SidebarMenuLink size="lg" to="/" onPress={closeMobile}>
              <div className="bg-sidebar-primary text-sidebar-primary-foreground flex aspect-square size-8 items-center justify-center rounded-lg">
                <span className="text-xs font-bold">{appInitials}</span>
              </div>
              <div className="grid flex-1 text-left text-sm leading-tight">
                <span className="line-clamp-2 font-semibold group-data-[collapsible=icon]:sr-only">{appName}</span>
              </div>
            </SidebarMenuLink>
          </SidebarMenuItem>
        </SidebarMenu>
      </SidebarHeader>
      <SidebarContent>
        <nav aria-label="Main">
          <SidebarGroup>
            <SidebarGroupLabel>Navigation</SidebarGroupLabel>
            <SidebarGroupContent>
              <SidebarMenu>
                {navItems.map((item) => {
                  return (
                    <SidebarMenuItem key={item.title}>
                      <SidebarMenuLink
                        to={item.url}
                        current={current?.url === item.url ? current.current : undefined}
                        tooltip={item.title}
                        onPress={closeMobile}
                      >
                        <HugeiconsIcon icon={item.icon} />
                        <span>{item.title}</span>
                      </SidebarMenuLink>
                    </SidebarMenuItem>
                  )
                })}

                {/* example:start */}
                <ExampleNav currentPath={currentPath} current={current} onNavigate={closeMobile} />
                {/* example:end */}
              </SidebarMenu>
            </SidebarGroupContent>
          </SidebarGroup>
        </nav>
      </SidebarContent>
      <SidebarFooter>
        <SidebarMenu>
          <SidebarMenuItem>
            <div className="flex items-center justify-center p-2 group-data-[collapsible=icon]:p-0">
              <ThemeToggle />
            </div>
          </SidebarMenuItem>
        </SidebarMenu>
      </SidebarFooter>
      <SidebarRail />
    </Sidebar>
  )
}
