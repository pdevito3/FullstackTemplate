import { useRouterState } from "@tanstack/react-router"
import {
  Home01Icon,
  InformationCircleIcon,
  DashboardSquare01Icon,
  ArrowRight01Icon,
} from "@hugeicons/core-free-icons"
import { HugeiconsIcon } from "@hugeicons/react"

import {
  Collapsible,
  CollapsibleContent,
  CollapsibleTrigger,
} from "@/components/ui/collapsible"
import {
  Sidebar,
  SidebarContent,
  SidebarFooter,
  SidebarGroup,
  SidebarGroupContent,
  SidebarGroupLabel,
  SidebarHeader,
  SidebarMenu,
  SidebarMenuButton,
  SidebarMenuItem,
  SidebarMenuSub,
  SidebarMenuSubButton,
  SidebarMenuSubItem,
  SidebarRail,
} from "@/components/ui/sidebar"
import { sidebarMenuButtonVariants } from "@/components/ui/sidebar-menu-button-variants"
import { useSidebar } from "@/components/ui/use-sidebar"
import { ThemeToggle } from "@/components/theme-toggle"

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

const demoItems = [
  {
    title: "Components",
    url: "/components",
  },
  {
    title: "Filter Builder",
    url: "/filter-demo",
  },
] as const

export function AppSidebar() {
  const routerState = useRouterState()
  const currentPath = routerState.location.pathname
  const { setOpenMobile } = useSidebar()

  // On mobile the sidebar is a sheet over the page. Close it when a link is
  // pressed, so the user sees the new page. On desktop the sheet is not used.
  const closeMobile = () => setOpenMobile(false)

  // Check if any demo item is active
  const isDemoActive = demoItems.some((item) => currentPath === item.url || currentPath.startsWith(item.url + "/"))

  return (
    <Sidebar variant="inset" collapsible="icon">
      <SidebarHeader>
        <SidebarMenu>
          <SidebarMenuItem>
            <SidebarMenuButton size="lg" href="/" onPress={closeMobile}>
              <div className="bg-sidebar-primary text-sidebar-primary-foreground flex aspect-square size-8 items-center justify-center rounded-lg">
                <span className="text-xs font-bold">FS</span>
              </div>
              <div className="grid flex-1 text-left text-sm leading-tight">
                <span className="truncate font-semibold">Fullstack</span>
                <span className="truncate text-xs text-muted-foreground">Template</span>
              </div>
            </SidebarMenuButton>
          </SidebarMenuItem>
        </SidebarMenu>
      </SidebarHeader>
      <SidebarContent>
        <SidebarGroup>
          <SidebarGroupLabel>Navigation</SidebarGroupLabel>
          <SidebarGroupContent>
            <SidebarMenu>
              {navItems.map((item) => {
                const isActive = currentPath === item.url
                return (
                  <SidebarMenuItem key={item.title}>
                    <SidebarMenuButton href={item.url} isActive={isActive} tooltip={item.title} onPress={closeMobile}>
                      <HugeiconsIcon icon={item.icon} />
                      <span>{item.title}</span>
                    </SidebarMenuButton>
                  </SidebarMenuItem>
                )
              })}

              {/* Demos submenu */}
              <Collapsible defaultExpanded={isDemoActive} className="group/collapsible">
                <SidebarMenuItem>
                  <CollapsibleTrigger className={sidebarMenuButtonVariants()} data-active={isDemoActive || undefined}>
                    <HugeiconsIcon icon={DashboardSquare01Icon} />
                    <span>Demos</span>
                    <HugeiconsIcon
                      icon={ArrowRight01Icon}
                      className="ml-auto transition-transform duration-200 group-data-[expanded]/collapsible:rotate-90"
                    />
                  </CollapsibleTrigger>
                  <CollapsibleContent>
                    <SidebarMenuSub>
                      {demoItems.map((item) => {
                        const isActive = currentPath === item.url
                        return (
                          <SidebarMenuSubItem key={item.title} isActive={isActive}>
                            <SidebarMenuSubButton href={item.url} isActive={isActive} onPress={closeMobile}>
                              <span>{item.title}</span>
                            </SidebarMenuSubButton>
                          </SidebarMenuSubItem>
                        )
                      })}
                    </SidebarMenuSub>
                  </CollapsibleContent>
                </SidebarMenuItem>
              </Collapsible>
            </SidebarMenu>
          </SidebarGroupContent>
        </SidebarGroup>
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
