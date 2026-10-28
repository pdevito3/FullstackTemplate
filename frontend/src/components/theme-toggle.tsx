import { Sun03Icon, Moon02Icon, ComputerIcon } from "@hugeicons/core-free-icons"
import { HugeiconsIcon } from "@hugeicons/react"
import { Button } from "react-aria-components"

import { useTheme } from "@/components/theme-context"
import { cn } from "@/lib/utils"

export function ThemeToggle() {
  const { theme, setTheme } = useTheme()

  return (
    <div className="inline-flex items-center rounded-lg border bg-muted p-0.5 group-data-[collapsible=icon]:flex-col">
      <Button
        onPress={() => setTheme("light")}
        className={cn(
          "inline-flex items-center justify-center rounded-md px-2 py-1 text-sm group-data-[collapsible=icon]:px-1 transition-colors",
          theme === "light"
            ? "bg-background text-foreground shadow-sm"
            : "text-muted-foreground hover:text-foreground"
        )}
        aria-label="Light mode"
      >
        <HugeiconsIcon icon={Sun03Icon} className="size-4" />
      </Button>
      <Button
        onPress={() => setTheme("dark")}
        className={cn(
          "inline-flex items-center justify-center rounded-md px-2 py-1 text-sm group-data-[collapsible=icon]:px-1 transition-colors",
          theme === "dark"
            ? "bg-background text-foreground shadow-sm"
            : "text-muted-foreground hover:text-foreground"
        )}
        aria-label="Dark mode"
      >
        <HugeiconsIcon icon={Moon02Icon} className="size-4" />
      </Button>
      <Button
        onPress={() => setTheme("system")}
        className={cn(
          "inline-flex items-center justify-center rounded-md px-2 py-1 text-sm group-data-[collapsible=icon]:px-1 transition-colors",
          theme === "system"
            ? "bg-background text-foreground shadow-sm"
            : "text-muted-foreground hover:text-foreground"
        )}
        aria-label="System theme"
      >
        <HugeiconsIcon icon={ComputerIcon} className="size-4" />
      </Button>
    </div>
  )
}
