import type { Row } from "@tanstack/react-table"
import { MoreHorizontalCircle01Icon } from "@hugeicons/core-free-icons"
import { HugeiconsIcon } from "@hugeicons/react"

import { Button } from "@/components/ui/button"
import {
  DropdownMenu,
  DropdownMenuItem,
  DropdownMenuSeparator,
  DropdownMenuTrigger,
} from "@/components/ui/dropdown-menu"

interface DataTableRowActionsProps<TData> {
  row: Row<TData>
  onEdit?: (row: TData) => void
  onDelete?: (row: TData) => void
  onView?: (row: TData) => void
  children?: React.ReactNode
}

function DataTableRowActions<TData>({
  row,
  onEdit,
  onDelete,
  onView,
  children,
}: DataTableRowActionsProps<TData>) {
  return (
    <DropdownMenuTrigger>
      <Button variant="ghost" className="flex h-8 w-8 p-0">
        <HugeiconsIcon icon={MoreHorizontalCircle01Icon} className="h-4 w-4" />
        <span className="sr-only">Open menu</span>
      </Button>
      <DropdownMenu placement="bottom end" className="w-[160px]">
        {onView && (
          <DropdownMenuItem onAction={() => onView(row.original)}>
            View
          </DropdownMenuItem>
        )}
        {onEdit && (
          <DropdownMenuItem onAction={() => onEdit(row.original)}>
            Edit
          </DropdownMenuItem>
        )}
        {children}
        {onDelete && (
          <>
            <DropdownMenuSeparator />
            <DropdownMenuItem
              variant="destructive"
              onAction={() => onDelete(row.original)}
            >
              Delete
            </DropdownMenuItem>
          </>
        )}
      </DropdownMenu>
    </DropdownMenuTrigger>
  )
}

export { DataTableRowActions }
