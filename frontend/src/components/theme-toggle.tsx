import { Sun03Icon, Moon02Icon, ComputerIcon } from "@hugeicons/core-free-icons"
import { HugeiconsIcon } from "@hugeicons/react"
import { useTheme } from "@/components/theme-provider"
import { cn } from "@/lib/utils"

export function ThemeToggle() {
  const { theme, setTheme } = useTheme()

  return (
    <div className="inline-flex items-center rounded-lg border bg-muted p-0.5">
      <button
        onClick={() => setTheme("light")}
        className={cn(
          "inline-flex items-center justify-center rounded-md px-2 py-1 text-sm transition-colors",
          theme === "light"
            ? "bg-background text-foreground shadow-sm"
            : "text-muted-foreground hover:text-foreground"
        )}
        aria-label="Light mode"
      >
        <HugeiconsIcon icon={Sun03Icon} className="size-4" />
      </button>
      <button
        onClick={() => setTheme("dark")}
        className={cn(
          "inline-flex items-center justify-center rounded-md px-2 py-1 text-sm transition-colors",
          theme === "dark"
            ? "bg-background text-foreground shadow-sm"
            : "text-muted-foreground hover:text-foreground"
        )}
        aria-label="Dark mode"
      >
        <HugeiconsIcon icon={Moon02Icon} className="size-4" />
      </button>
      <button
        onClick={() => setTheme("system")}
        className={cn(
          "inline-flex items-center justify-center rounded-md px-2 py-1 text-sm transition-colors",
          theme === "system"
            ? "bg-background text-foreground shadow-sm"
            : "text-muted-foreground hover:text-foreground"
        )}
        aria-label="System theme"
      >
        <HugeiconsIcon icon={ComputerIcon} className="size-4" />
      </button>
    </div>
  )
}
