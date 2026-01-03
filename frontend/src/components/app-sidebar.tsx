import { Link, useRouterState } from "@tanstack/react-router"
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
]

const demoItems = [
  {
    title: "Components",
    url: "/components",
  },
  // Add more demo routes here
  // {
  //   title: "Examples",
  //   url: "/examples",
  // },
  // {
  //   title: "Playground",
  //   url: "/playground",
  // },
]

export function AppSidebar() {
  const routerState = useRouterState()
  const currentPath = routerState.location.pathname

  // Check if any demo item is active
  const isDemoActive = demoItems.some((item) => currentPath === item.url || currentPath.startsWith(item.url + "/"))

  return (
    <Sidebar variant="inset" collapsible="icon">
      <SidebarHeader>
        <SidebarMenu>
          <SidebarMenuItem>
            <SidebarMenuButton size="lg" render={<Link to="/" />}>
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
                    <SidebarMenuButton
                      render={<Link to={item.url} />}
                      isActive={isActive}
                    >
                      <HugeiconsIcon icon={item.icon} />
                      <span>{item.title}</span>
                    </SidebarMenuButton>
                  </SidebarMenuItem>
                )
              })}

              {/* Demos submenu */}
              <Collapsible defaultOpen={isDemoActive} className="group/collapsible">
                <SidebarMenuItem>
                  <CollapsibleTrigger className="ring-sidebar-ring hover:bg-sidebar-accent hover:text-sidebar-accent-foreground active:bg-sidebar-accent active:text-sidebar-accent-foreground data-[state=open]:hover:bg-sidebar-accent data-[state=open]:hover:text-sidebar-accent-foreground gap-2 rounded-md p-2 text-left text-sm transition-[width,height,padding] group-has-data-[sidebar=menu-action]/menu-item:pr-8 group-data-[collapsible=icon]:size-8! group-data-[collapsible=icon]:p-2! focus-visible:ring-2 peer/menu-button flex w-full items-center overflow-hidden outline-hidden [&>span:last-child]:truncate [&_svg]:size-4 [&_svg]:shrink-0 h-8 data-[active]:bg-sidebar-accent data-[active]:text-sidebar-accent-foreground data-[active]:font-medium" data-active={isDemoActive || undefined}>
                    <HugeiconsIcon icon={DashboardSquare01Icon} />
                    <span>Demos</span>
                    <HugeiconsIcon
                      icon={ArrowRight01Icon}
                      className="ml-auto transition-transform group-data-[state=open]/collapsible:rotate-90"
                    />
                  </CollapsibleTrigger>
                  <CollapsibleContent>
                    <SidebarMenuSub>
                      {demoItems.map((item) => {
                        const isActive = currentPath === item.url
                        return (
                          <SidebarMenuSubItem key={item.title}>
                            <SidebarMenuSubButton
                              render={<Link to={item.url} />}
                              isActive={isActive}
                            >
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
