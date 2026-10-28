import type { Column } from "@tanstack/react-table"
import { ArrowDown01Icon, ArrowUp01Icon, ArrowUpDownIcon, ViewOffSlashIcon } from "@hugeicons/core-free-icons"
import { HugeiconsIcon } from "@hugeicons/react"

import { cn } from "@/lib/utils"
import { Button } from "@/components/ui/button"
import {
  DropdownMenu,
  DropdownMenuItem,
  DropdownMenuSeparator,
  DropdownMenuTrigger,
} from "@/components/ui/dropdown-menu"

interface DataTableColumnHeaderProps<TData, TValue>
  extends React.HTMLAttributes<HTMLDivElement> {
  column: Column<TData, TValue>
  title: string
}

function DataTableColumnHeader<TData, TValue>({
  column,
  title,
  className,
}: DataTableColumnHeaderProps<TData, TValue>) {
  if (!column.getCanSort()) {
    return <div className={cn(className)}>{title}</div>
  }

  return (
    <div className={cn("flex items-center space-x-2", className)}>
      <DropdownMenuTrigger>
        <Button variant="ghost" size="sm" className="-ml-3 h-8">
          <span>{title}</span>
          {column.getIsSorted() === "desc" ? (
            <HugeiconsIcon icon={ArrowDown01Icon} className="size-4" />
          ) : column.getIsSorted() === "asc" ? (
            <HugeiconsIcon icon={ArrowUp01Icon} className="size-4" />
          ) : (
            <HugeiconsIcon icon={ArrowUpDownIcon} className="size-4" />
          )}
        </Button>
        <DropdownMenu placement="bottom start" className="w-auto">
          <DropdownMenuItem textValue="Asc" onAction={() => column.toggleSorting(false)}>
            <HugeiconsIcon icon={ArrowUp01Icon} className="size-3.5 text-muted-foreground/70" />
            Asc
          </DropdownMenuItem>
          <DropdownMenuItem textValue="Desc" onAction={() => column.toggleSorting(true)}>
            <HugeiconsIcon icon={ArrowDown01Icon} className="size-3.5 text-muted-foreground/70" />
            Desc
          </DropdownMenuItem>
          <DropdownMenuSeparator />
          <DropdownMenuItem textValue="Hide" onAction={() => column.toggleVisibility(false)}>
            <HugeiconsIcon icon={ViewOffSlashIcon} className="size-3.5 text-muted-foreground/70" />
            Hide
          </DropdownMenuItem>
        </DropdownMenu>
      </DropdownMenuTrigger>
    </div>
  )
}

export { DataTableColumnHeader }
