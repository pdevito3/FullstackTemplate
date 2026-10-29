import { ArrowRight01Icon, DashboardSquare01Icon } from "@hugeicons/core-free-icons"
import { HugeiconsIcon } from "@hugeicons/react"

import { Collapsible, CollapsibleContent, CollapsibleTrigger } from "@/components/ui/collapsible"
import {
  SidebarMenuItem,
  SidebarMenuSub,
  SidebarMenuSubItem,
  SidebarMenuSubLink,
} from "@/components/ui/sidebar"
import { sidebarMenuButtonVariants } from "@/components/ui/sidebar-menu-button-variants"

const exampleItems = [
  {
    title: "Weather",
    url: "/weather",
  },
  {
    title: "Components",
    url: "/components",
  },
  {
    title: "Filter Builder",
    url: "/filter-demo",
  },
] as const

interface ExampleNavProps {
  currentPath: string
  onNavigate: () => void
}

export function ExampleNav({ currentPath, onNavigate }: ExampleNavProps) {
  const isExampleActive = exampleItems.some((item) => currentPath === item.url || currentPath.startsWith(item.url + "/"))

  return (
    <Collapsible defaultExpanded={isExampleActive} className="group/collapsible">
      <SidebarMenuItem>
        <CollapsibleTrigger className={sidebarMenuButtonVariants()} data-active={isExampleActive || undefined}>
          <HugeiconsIcon icon={DashboardSquare01Icon} />
          <span>Examples</span>
          <HugeiconsIcon
            icon={ArrowRight01Icon}
            className="ml-auto transition-transform duration-200 group-data-[expanded]/collapsible:rotate-90"
          />
        </CollapsibleTrigger>
        <CollapsibleContent>
          <SidebarMenuSub>
            {exampleItems.map((item) => {
              const isActive = currentPath === item.url
              return (
                <SidebarMenuSubItem key={item.title} isActive={isActive}>
                  <SidebarMenuSubLink to={item.url} isActive={isActive} onPress={onNavigate}>
                    <span>{item.title}</span>
                  </SidebarMenuSubLink>
                </SidebarMenuSubItem>
              )
            })}
          </SidebarMenuSub>
        </CollapsibleContent>
      </SidebarMenuItem>
    </Collapsible>
  )
}
